using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// Step 3 of a game night: show the rules of the chosen game and let the
/// players choose how many subgames to play. "Skip rules" and "Start game" do
/// the same thing - start a session with the chosen players. Also opened by
/// "Play again" after a game, with the same players and "subgameCount".
/// </summary>
public partial class GameRulesViewModel : ObservableObject, IQueryAttributable
{
	private readonly ILocalDatabaseService _local;
	private readonly IAuthService _auth;
	private readonly GameNight _gameNight;
	private readonly IStoreService _store;

	/// <summary>A paid game the account doesn't own: the rules can be read, but Unlock replaces Skip rules and Start game.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsUnlocked))]
	private bool isLocked;

	public bool IsUnlocked => !IsLocked;

	public PubGame? Game => _game;

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

	/// <summary>How many players the game is for, e.g. "Players: 2-6" or "Players: Any".</summary>
	[ObservableProperty]
	private string playerCountLabel = string.Empty;

	/// <summary>How many subgames make up the game: 1 unless the players change it (also during the game).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SubgameCountText))]
	private int subgameCount = 1;

	public string SubgameCountText => SubgameCount == 1 ? "1 game" : $"{SubgameCount} subgames";

	public void ChangeSubgameCount(int delta) => SubgameCount = Math.Clamp(SubgameCount + delta, 1, 99);

	/// <summary>Raised with the new session id once it's saved, so the page can open the scoreboard.</summary>
	public event EventHandler<string>? SessionStarted;

	public GameRulesViewModel(ILocalDatabaseService local, IAuthService auth, GameNight gameNight, IStoreService store)
	{
		_store = store;
		_local = local;
		_auth = auth;
		_gameNight = gameNight;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("game", out var g) && g is PubGame game)
		{
			_game = game;
			GameName = game.Name;
			CoverImageRef = game.CoverImageUrl;
			PlayerCountLabel = $"Players: {game.PlayersLabel}";
			RulesBlocks = string.IsNullOrWhiteSpace(game.RulesText)
				? [new RulesBlock("No rules written for this game yet.", null)]
				: RulesBlock.Parse(game.RulesText);
			IsLocked = !_store.Owns(game);
		}

		if (query.TryGetValue("players", out var p) && p is List<Player> players)
		{
			_players = players;
			PlayersSummary = "Playing: " + string.Join(", ", players.Select(pl => pl.Name));
		}

		// Play again: the same number of subgames as last time.
		if (query.TryGetValue("subgameCount", out var c) && c is int count)
			SubgameCount = count;
	}

	/// <summary>After buying, a code or the pass: whether the game is unlocked now.</summary>
	public void CheckUnlocked()
	{
		if (_game is not null)
			IsLocked = !_store.Owns(_game);
	}

	public Task<string> PriceAsync() => _game is null ? Task.FromResult(string.Empty) : _store.GamePriceAsync(_game);

	public async Task<(PurchaseOutcome Outcome, string? Problem)> BuyAsync()
	{
		if (_game is null) return (PurchaseOutcome.Cancelled, null);
		var result = await _store.BuyGameAsync(_game);
		CheckUnlocked();
		return result;
	}

	/// <summary>Today's friend code: null when it unlocked the game, otherwise why not.</summary>
	public async Task<string?> RedeemAsync(string code)
	{
		if (_game is null) return null;
		var problem = await _store.RedeemCodeAsync(code, _game);
		CheckUnlocked();
		return problem;
	}

	[RelayCommand]
	private async Task StartGameAsync()
	{
		if (_game is null || IsLocked) return;

		// Seat order follows the order players were added on the first screen.
		var sessionId = await _gameNight.StartSessionAsync(_game, _players, SubgameCount, _auth.AccountId);
		SessionStarted?.Invoke(this, sessionId);
	}
}
