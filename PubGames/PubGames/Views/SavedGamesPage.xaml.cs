using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// Players tab &gt; Saved games: unfinished and finished games on this phone, to continue, look at or delete.
/// </summary>
public partial class SavedGamesPage : ContentPage
{
	private readonly SavedGamesViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public SavedGamesPage(SavedGamesViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.SavedGames);
	}

	/// <summary>Every time the page shows: the saved games are loaded fresh.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	/// <summary>Selecting: tick or untick. Otherwise: continue the game, or see how it ended.</summary>
	private async void OnRowTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not SavedGameRow row) return;
		if (_vm.IsSelecting)
		{
			row.IsSelected = !row.IsSelected;
			return;
		}
		await Shell.Current.GoToAsync(nameof(ScoreboardPage), new Dictionary<string, object> { ["sessionId"] = row.Game.Session.Id });
	}

	/// <summary>Shows the unfinished games.</summary>
	private void OnUnfinishedTabClicked(object? sender, EventArgs e) => _vm.ShowsFinished = false;

	/// <summary>Shows the finished games.</summary>
	private void OnFinishedTabClicked(object? sender, EventArgs e) => _vm.ShowsFinished = true;

	/// <summary>Starts or stops choosing games to delete.</summary>
	private void OnSelectClicked(object? sender, EventArgs e) => _vm.ToggleSelecting();

	/// <summary>Selects every game on the page.</summary>
	private void OnSelectAllClicked(object? sender, EventArgs e) => _vm.SelectAll(true);

	/// <summary>Selects no game.</summary>
	private void OnDeselectAllClicked(object? sender, EventArgs e) => _vm.SelectAll(false);

	/// <summary>Deletes the selected games, after asking.</summary>
	private async void OnDeleteClicked(object? sender, EventArgs e)
	{
		var count = _vm.SelectedCount;
		if (await DisplayAlertAsync(count == 1 ? L.T("Delete this game?") : L.F("Delete {0} games?", count),
			L.T("They're removed from this phone. Results that already count in the stats stay there."), L.T("Delete"), L.T("Keep")))
			await _vm.DeleteSelectedAsync();
	}
}
