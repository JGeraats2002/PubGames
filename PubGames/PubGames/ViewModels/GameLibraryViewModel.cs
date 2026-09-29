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

	/// <summary>Filter buttons: "All" plus every category that has games. Selecting one shows only its games.</summary>
	public ObservableCollection<CategoryChoice> CategoryFilters { get; } = new();

	/// <summary>Stands for "no filter" in CategoryFilters.</summary>
	private static readonly GameCategory AllCategories = new() { Id = string.Empty, Name = "All" };

	/// <summary>Shown when the cloud couldn't be reached and the list is the last-downloaded copy.</summary>
	[ObservableProperty]
	private string syncMessage = string.Empty;

	private string CurrentAccountId => _auth.AccountId;

	public ObservableCollection<PubGame> VisibleGames { get; } = new();

	[ObservableProperty]
	private string searchText = string.Empty;

	/// <summary>Id of the selected category; empty = "All".</summary>
	[ObservableProperty]
	private string activeCategoryFilter = string.Empty;

	public GameLibraryViewModel(ILocalDatabaseService local, IAuthService auth, ICloudSyncService cloud, ICategoryService categories)
	{
		_local = local;
		_auth = auth;
		_cloud = cloud;
		_categories = categories;
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
		_allGames = await _local.GetGamesAsync();
		var categories = await _categories.GetCachedAsync();
		foreach (var game in _allGames)
			game.CategoryLabel = _categories.LabelFor(game.CategoryIds, categories);

		// Only categories that have games, so a filter never shows an empty list.
		var used = _allGames.SelectMany(g => g.CategoryIds).ToHashSet();
		if (!used.Contains(ActiveCategoryFilter))
			ActiveCategoryFilter = string.Empty;
		CategoryFilters.Clear();
		foreach (var category in categories.Where(c => used.Contains(c.Id)).Prepend(AllCategories))
			CategoryFilters.Add(new CategoryChoice { Category = category, IsSelected = category.Id == ActiveCategoryFilter });

		ApplyFilters();
	}

	partial void OnSearchTextChanged(string value) => ApplyFilters();

	partial void OnActiveCategoryFilterChanged(string value)
	{
		foreach (var filter in CategoryFilters)
			filter.IsSelected = filter.Category.Id == value;
		ApplyFilters();
	}

	[RelayCommand]
	private void SelectCategory(CategoryChoice filter) => ActiveCategoryFilter = filter.Category.Id;

	private void ApplyFilters()
	{
		VisibleGames.Clear();

		foreach (var game in _allGames)
		{
			if (!string.IsNullOrWhiteSpace(SearchText) &&
			    !game.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
				continue;

			if (ActiveCategoryFilter.Length > 0 && !game.CategoryIds.Contains(ActiveCategoryFilter))
				continue;

			VisibleGames.Add(game);
		}
	}

	public async Task<bool> IsOwnedAsync(PubGame game) =>
		!game.IsPaid || await _local.IsGameOwnedAsync(CurrentAccountId, game.Id);
}
