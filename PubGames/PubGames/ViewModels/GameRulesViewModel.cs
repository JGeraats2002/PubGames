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

	/// <summary>The game may be played: Start game and Skip rules show.</summary>
	public bool IsUnlocked => !IsLocked;

	/// <summary>The game whose rules are shown.</summary>
	public PubGame? Game => _game;

	private PubGame? _game;
	private List<Player> _players = new();

	/// <summary>The game's name.</summary>
	[ObservableProperty]
	private string gameName = string.Empty;

	/// <summary>The game's cover picture, or null.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasCover))]
	private string? coverImageRef;

	/// <summary>Whether the game has a cover picture.</summary>
	public bool HasCover => !string.IsNullOrEmpty(CoverImageRef);

	/// <summary>The rules split into text and images, in order.</summary>
	[ObservableProperty]
	private List<RulesBlock> rulesBlocks = new();

	/// <summary>"Playing: Jan, Sanne, Piet".</summary>
	[ObservableProperty]
	private string playersSummary = string.Empty;

	/// <summary>How many players the game is for, e.g. "Players: 2-6" or "Players: Any".</summary>
	[ObservableProperty]
	private string playerCountLabel = string.Empty;

	/// <summary>How many subgames make up the game: 1 unless the players change it (also during the game).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SubgameCountText))]
	private int subgameCount = 1;

	/// <summary>"1 game" or "3 subgames".</summary>
	public string SubgameCountText => SubgameCount == 1 ? L.T("1 game") : L.F("{0} subgames", SubgameCount);

	/// <summary>One subgame more (+1) or fewer (-1); at least 1.</summary>
	public void ChangeSubgameCount(int delta) => SubgameCount = Math.Clamp(SubgameCount + delta, 1, 99);

	/// <summary>Raised with the new session id once it's saved, so the page can open the scoreboard.</summary>
	public event EventHandler<string>? SessionStarted;

	/// <summary>
	/// Gets the services it works with - the phone's database, the sign-in, the game night and the store -
	/// from the app (dependency injection).
	/// </summary>
	public GameRulesViewModel(ILocalDatabaseService local, IAuthService auth, GameNight gameNight, IStoreService store)
	{
		_store = store;
		_local = local;
		_auth = auth;
		_gameNight = gameNight;
	}

	/// <summary>
	/// Opened with a game and its players (and, for Play again, the number of subgames): shows its rules and
	/// whether it's unlocked.
	/// </summary>
	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("game", out var g) && g is PubGame game)
		{
			_game = game;
			GameName = game.Name;
			CoverImageRef = game.CoverImageUrl;
			PlayerCountLabel = L.F("Players: {0}", game.PlayersLabel);
			RulesBlocks = string.IsNullOrWhiteSpace(game.RulesText)
				? [new RulesBlock(L.T("No rules written for this game yet."), null)]
				: RulesBlock.Parse(game.RulesText);
			IsLocked = !_store.Owns(game);
		}

		if (query.TryGetValue("players", out var p) && p is List<Player> players)
		{
			_players = players;
			PlayersSummary = L.T("Playing: ") + string.Join(", ", players.Select(pl => pl.Name));
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

	/// <summary>The game's price as Google Play shows it.</summary>
	public Task<string> PriceAsync() => _game is null ? Task.FromResult(string.Empty) : _store.GamePriceAsync(_game);

	/// <summary>Buys the game with Google Play; afterwards the page shows whether it's unlocked.</summary>
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

	/// <summary>
	/// Starts the game night with these players and number of subgames, and opens the scoreboard.
	/// </summary>
	[RelayCommand]
	private async Task StartGameAsync()
	{
		if (_game is null || IsLocked) return;

		// Seat order follows the order players were added on the first screen.
		var sessionId = await _gameNight.StartSessionAsync(_game, _players, SubgameCount, _auth.AccountId);
		SessionStarted?.Invoke(this, sessionId);
	}
}
