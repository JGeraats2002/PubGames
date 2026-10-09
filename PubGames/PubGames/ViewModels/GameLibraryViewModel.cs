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

	public ObservableCollection<PubGame> VisibleGames { get; } = new();

	[ObservableProperty]
	private string searchText = string.Empty;

	public GameLibraryViewModel(ILocalDatabaseService local, IAuthService auth, ICloudSyncService cloud, ICategoryService categories)
	{
		_local = local;
		_auth = auth;
		_cloud = cloud;
		_categories = categories;
		Filters.Changed += (_, _) => ApplyFilters();
	}

	public async Task LoadAsync()
	{
		await _auth.InitializeAsync();

		// Show the saved copy straight away, then refresh it from the cloud.
		await ShowLocalAsync();

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

	private async Task ShowLocalAsync()
	{
		_allGames = (await _local.GetGamesAsync()).OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
		var categories = await _categories.GetCachedAsync();

		// Only categories that have games, so a single cloud never shows an empty list.
		var used = _allGames.SelectMany(g => g.CategoryIds).ToHashSet();
		Filters.Show(await _categories.GetCachedGroupsAsync(), categories.Where(c => used.Contains(c.Id)));
		foreach (var game in _allGames)
			game.CategoryLabel = _categories.LabelFor(game.CategoryIds, categories);

		ApplyFilters();
	}

	partial void OnSearchTextChanged(string value) => ApplyFilters();

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

	public async Task<bool> IsOwnedAsync(PubGame game) =>
		!game.IsPaid || await _local.IsGameOwnedAsync(CurrentAccountId, game.Id);
}
