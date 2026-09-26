using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

public partial class GameLibraryViewModel : ObservableObject
{
	private readonly ILocalDatabaseService _local;
	private List<PubGame> _allGames = new();

	// TODO: replace with real account id from auth state.
	private const string CurrentAccountId = "local-account";

	public ObservableCollection<PubGame> VisibleGames { get; } = new();

	[ObservableProperty]
	private string searchText = string.Empty;

	/// <summary>null/empty = "All" category selected.</summary>
	[ObservableProperty]
	private string? activeCategoryFilter;

	public GameLibraryViewModel(ILocalDatabaseService local)
	{
		_local = local;
	}

	public async Task LoadAsync()
	{
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
