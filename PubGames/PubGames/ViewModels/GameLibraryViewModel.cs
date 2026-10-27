using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// Step 2 of a game night: pick the game for the players chosen on PlayerEntryPage.
/// </summary>
public partial class GameLibraryViewModel : ObservableObject, IQueryAttributable
{
	/// <summary>Who's playing - handed over from PlayerEntryPage and passed on to the rules page.</summary>
	public List<Player> Players { get; private set; } = new();

	/// <summary>Opened with the players of the game night: keeps them for when a game is chosen.</summary>
	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		// Single-use parameters: absent when navigating back here, so keep what we had.
		if (query.TryGetValue("players", out var p) && p is List<Player> players)
			Players = players;
	}

	private readonly ILocalDatabaseService _local;
	private readonly IAuthService _auth;
	private readonly ICloudSyncService _cloud;
	private readonly ICategoryService _categories;
	private List<PubGame> _allGames = new();

	/// <summary>Category clouds: "All" plus every category that has games; several can be selected.</summary>
	public CategoryFilterSet Filters { get; } = new();

	/// <summary>Shown when the cloud couldn't be reached and the list is the last-downloaded copy.</summary>
	[ObservableProperty]
	private string syncMessage = string.Empty;

	private string CurrentAccountId => _auth.AccountId;

	/// <summary>The games that match the search and filters.</summary>
	public ObservableCollection<PubGame> VisibleGames { get; } = new();

	/// <summary>The search text: only games with it in their name show.</summary>
	[ObservableProperty]
	private string searchText = string.Empty;

	private readonly IStoreService _store;

	/// <summary>
	/// Gets the services it works with - the phone's database, the sign-in, cloud sync, the categories and
	/// the store - from the app (dependency injection).
	/// </summary>
	public GameLibraryViewModel(ILocalDatabaseService local, IAuthService auth, ICloudSyncService cloud, ICategoryService categories, IStoreService store)
	{
		_store = store;
		_local = local;
		_auth = auth;
		_cloud = cloud;
		_categories = categories;
		Filters.Changed += (_, _) => ApplyFilters();
	}

	/// <summary>
	/// Shows the games as saved on the phone straight away, with locks on what isn't owned, then refreshes
	/// games and categories from the cloud.
	/// </summary>
	public async Task LoadAsync()
	{
		await _auth.InitializeAsync();

		// Show the saved copy straight away, then refresh it from the cloud.
		await ShowLocalAsync();

		// What the account owns (bought games, the pass); the list shows a lock and price on the rest.
		await _store.RefreshAsync();
		ShowAccess();

		try
		{
			await _cloud.PullPublishedGamesAsync();
			await _categories.PullAsync();
			await _categories.PullGroupsAsync();
			SyncMessage = string.Empty;
		}
		catch (Exception ex)
		{
			SyncMessage = ex is HttpRequestException
				? "Offline - showing the games saved on this phone."
				: $"Couldn't refresh games: {ex.Message}";
			return;
		}

		await ShowLocalAsync();
	}

	/// <summary>
	/// Shows the games on the phone with their categories, and fills the filters with the categories that
	/// have games.
	/// </summary>
	private async Task ShowLocalAsync()
	{
		_allGames = (await _local.GetGamesAsync()).OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
		var categories = await _categories.GetCachedAsync();

		// Only categories that have games, so a single cloud never shows an empty list.
		var used = _allGames.SelectMany(g => g.CategoryIds).ToHashSet();
		Filters.Show(await _categories.GetCachedGroupsAsync(), categories.Where(c => used.Contains(c.Id)));
		foreach (var game in _allGames)
			game.CategoryLabel = _categories.LabelFor(game.CategoryIds, categories);
		ShowAccess();

		ApplyFilters();
	}

	partial void OnSearchTextChanged(string value) => ApplyFilters();

	/// <summary>Shows the games that match the search and every filter.</summary>
	private void ApplyFilters()
	{
		VisibleGames.Clear();

		foreach (var game in _allGames)
		{
			if (!string.IsNullOrWhiteSpace(SearchText) &&
			    !game.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
				continue;

			if (!Filters.Matches(game))
				continue;

			VisibleGames.Add(game);
		}
	}

	/// <summary>Whether the account may play this game: free, bought, or unlocked by the pass.</summary>
	public bool IsOwned(PubGame game) => _store.Owns(game);

	/// <summary>The lock and price right of every game the account doesn't own yet.</summary>
	public void ShowAccess()
	{
		foreach (var game in _allGames)
			game.AccessLabel = _store.Owns(game) ? string.Empty : $"🔒 {Prices.Format(game.Price)}";
		ApplyFilters();
	}

	/// <summary>
	/// Once a day at most: the pass is about to end without renewing (3 days
	/// or less), or ended in the last week. Null when there's nothing to say.
	/// </summary>
	public string? PassNotice()
	{
		var e = _store.Current;
		if (e.PassRunsUntil is not { } until || e.Renews) return null;
		var left = until - DateTime.UtcNow;
		string? notice = left > TimeSpan.Zero && left <= TimeSpan.FromDays(3)
			? $"Your Pub Games Pass ends on {until.ToLocalTime():d MMMM} and doesn't renew. Account > Pub Games Pass keeps every game unlocked."
			: left <= TimeSpan.Zero && left > TimeSpan.FromDays(-7)
				? "Your Pub Games Pass has ended. Paid games you didn't buy are locked again; Account > Pub Games Pass unlocks them."
				: null;
		if (notice is null) return null;

		var key = $"passNotice:{_auth.AccountId}";
		var today = DateTime.Now.ToString("yyyy-MM-dd");
		if (Preferences.Default.Get(key, string.Empty) == today) return null;
		Preferences.Default.Set(key, today);
		return notice;
	}
}
