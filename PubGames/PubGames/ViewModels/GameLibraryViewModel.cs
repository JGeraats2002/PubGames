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
	private List<PubGame> _allGames = new();

	/// <summary>Shown when the cloud couldn't be reached and the list is the last-downloaded copy.</summary>
	[ObservableProperty]
	private string syncMessage = string.Empty;

	private string CurrentAccountId => _auth.AccountId;

	public ObservableCollection<PubGame> VisibleGames { get; } = new();

	[ObservableProperty]
	private string searchText = string.Empty;

	/// <summary>null/empty = "All" category selected.</summary>
	[ObservableProperty]
	private string? activeCategoryFilter;

	public GameLibraryViewModel(ILocalDatabaseService local, IAuthService auth, ICloudSyncService cloud)
	{
		_local = local;
		_auth = auth;
		_cloud = cloud;
	}

	public async Task LoadAsync()
	{
		await _auth.InitializeAsync();

		// Show the saved copy straight away, then refresh it from the cloud.
		_allGames = await _local.GetGamesAsync();
		ApplyFilters();

		try
		{
			await _cloud.PullPublishedGamesAsync();
			SyncMessage = string.Empty;
		}
		catch (Exception ex)
		{
			SyncMessage = ex is HttpRequestException
				? "Offline - showing the games saved on this phone."
				: $"Couldn't refresh games: {ex.Message}";
			return;
		}

		_allGames = await _local.GetGamesAsync();
		ApplyFilters();
	}

	partial void OnSearchTextChanged(string value) => ApplyFilters();
	partial void OnActiveCategoryFilterChanged(string? value) => ApplyFilters();

	[RelayCommand]
	private void SelectCategory(string? category) => ActiveCategoryFilter = category;

	private async void ApplyFilters()
	{
		VisibleGames.Clear();

		foreach (var game in _allGames)
		{
			if (!string.IsNullOrWhiteSpace(SearchText) &&
			    !game.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
				continue;

			if (!string.IsNullOrWhiteSpace(ActiveCategoryFilter))
			{
				var categories = await _local.GetCategoriesForGameAsync(game.Id);
				if (!categories.Any(c => c.Name.Equals(ActiveCategoryFilter, StringComparison.OrdinalIgnoreCase)))
					continue;
			}

			VisibleGames.Add(game);
		}
	}

	public async Task<bool> IsOwnedAsync(PubGame game) =>
		!game.IsPaid || await _local.IsGameOwnedAsync(CurrentAccountId, game.Id);
}
