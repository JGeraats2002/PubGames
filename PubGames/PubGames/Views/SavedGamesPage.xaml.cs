using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class SavedGamesPage : ContentPage
{
	private readonly SavedGamesViewModel _vm;

	public SavedGamesPage(SavedGamesViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.SavedGames);
	}

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

	private void OnUnfinishedTabClicked(object? sender, EventArgs e) => _vm.ShowsFinished = false;

	private void OnFinishedTabClicked(object? sender, EventArgs e) => _vm.ShowsFinished = true;

	private void OnSelectClicked(object? sender, EventArgs e) => _vm.ToggleSelecting();

	private void OnSelectAllClicked(object? sender, EventArgs e) => _vm.SelectAll(true);

	private void OnDeselectAllClicked(object? sender, EventArgs e) => _vm.SelectAll(false);

	private async void OnDeleteClicked(object? sender, EventArgs e)
	{
		var count = _vm.SelectedCount;
		if (await DisplayAlertAsync(count == 1 ? "Delete this game?" : $"Delete {count} games?",
			"They're removed from this phone. Results that already count in the stats stay there.", "Delete", "Keep"))
			await _vm.DeleteSelectedAsync();
	}
}
