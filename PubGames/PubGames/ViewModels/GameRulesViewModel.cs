using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// Step 3 of a game night: show the rules of the chosen game. "Skip rules" and
/// "Start game" do the same thing - start a session with the chosen players.
/// </summary>
public partial class GameRulesViewModel : ObservableObject, IQueryAttributable
{
	private readonly ILocalDatabaseService _local;
	private readonly IAuthService _auth;

	private PubGame? _game;
	private List<Player> _players = new();

	[ObservableProperty]
	private string gameName = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasCover))]
	private string? coverImageRef;

	public bool HasCover => !string.IsNullOrEmpty(CoverImageRef);

	/// <summary>The rules split into text and images, in order.</summary>
	[ObservableProperty]
	private List<RulesBlock> rulesBlocks = new();

	[ObservableProperty]
	private string playersSummary = string.Empty;

	/// <summary>Raised with the new session id once it's saved, so the page can open the scoreboard.</summary>
	public event EventHandler<string>? SessionStarted;

	public GameRulesViewModel(ILocalDatabaseService local, IAuthService auth)
	{
		_local = local;
		_auth = auth;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("game", out var g) && g is PubGame game)
		{
			_game = game;
			GameName = game.Name;
			CoverImageRef = game.CoverImageUrl;
			RulesBlocks = string.IsNullOrWhiteSpace(game.RulesText)
				? [new RulesBlock("No rules written for this game yet.", null)]
				: RulesBlock.Parse(game.RulesText);
		}

		if (query.TryGetValue("players", out var p) && p is List<Player> players)
		{
			_players = players;
			PlayersSummary = "Playing: " + string.Join(", ", players.Select(pl => pl.Name));
		}
	}

	[RelayCommand]
	private async Task StartGameAsync()
	{
		if (_game is null) return;

		var session = new GameSession
		{
			GameId = _game.Id,
			AccountId = _auth.AccountId
		};
		await _local.SaveSessionAsync(session);

		// Seat order follows the order players were added on the first screen.
		for (var i = 0; i < _players.Count; i++)
		{
			await _local.SaveParticipantAsync(new SessionParticipant
			{
				SessionId = session.Id,
				PlayerId = _players[i].Id,
				Position = i
			});
		}

		SessionStarted?.Invoke(this, session.Id);
	}
}
