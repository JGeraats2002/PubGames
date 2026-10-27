using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// The duel popup, shown as a modal page. The duel ends by itself; before it
/// counts the players are asked whether the result is right. Use PlayAsync.
/// </summary>
public partial class DuelPage : ContentPage
{
	private readonly DuelViewModel _vm;
	private readonly TaskCompletionSource<Dictionary<string, int>?> _result = new();

	/// <summary>Sets up the duel page: its view model, and the buttons stay above the keyboard.</summary>
	private DuelPage(DuelViewModel vm)
	{
		InitializeComponent();
		// The page ends above the keyboard, so the buttons stay right on top of it.
		KeyboardWatcher.Track(this, covered => Root.Padding = new Thickness(16, 16, 16, 16 + covered));
		BindingContext = _vm = vm;
	}

	/// <summary>Plays a duel on top of `from`: everyone's confirmed duel score by player id, or null when cancelled.</summary>
	public static async Task<Dictionary<string, int>?> PlayAsync(Page from, DuelSetup setup)
	{
		var page = new DuelPage(new DuelViewModel(setup));
		await from.Navigation.PushModalAsync(page);
		var scores = await page._result.Task;
		await from.Navigation.PopModalAsync();
		return scores;
	}

	/// <summary>− for the player whose turn it is; when that ends the duel, the result is checked.</summary>
	private async void OnMinusClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: DuelRow row } && _vm.Adjust(row, -1))
			await FinishAsync();
	}

	/// <summary>+ for the player whose turn it is; when that ends the duel, the result is checked.</summary>
	private async void OnPlusClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: DuelRow row } && _vm.Adjust(row, +1))
			await FinishAsync();
	}

	/// <summary>Ends the turn; when that ends the duel, the result is checked.</summary>
	private async void OnTurnDoneClicked(object? sender, EventArgs e)
	{
		if (_vm.TurnDone())
			await FinishAsync();
	}

	/// <summary>Takes back the last turn.</summary>
	private void OnUndoClicked(object? sender, EventArgs e) => _vm.Undo();

	/// <summary>The duel ended by itself: the players check the result, or take back the last turn if something was entered wrong.</summary>
	private async Task FinishAsync()
	{
		if (await DisplayAlertAsync("Duel over - is this right?", _vm.ScoreLines, "Yes, it's right", "Undo last turn"))
			_result.TrySetResult(_vm.Scores());
		else
			_vm.Undo();
	}

	/// <summary>How this duel is scored and ends, and how to play it - shown over the duel.</summary>
	private async void OnInfoClicked(object? sender, EventArgs e) =>
		await Navigation.PushModalAsync(new InfoPage(Models.Help.Duel(_vm.Explanation), modal: true));

	/// <summary>Stops the duel without a result, after asking.</summary>
	private async void OnCancelClicked(object? sender, EventArgs e)
	{
		if (await DisplayAlertAsync("Stop the duel?", "The tie isn't decided and nothing is saved yet.", "Stop the duel", "Keep playing"))
			_result.TrySetResult(null);
	}

	/// <summary>The phone's back button asks the same as Cancel.</summary>
	protected override bool OnBackButtonPressed()
	{
		OnCancelClicked(this, EventArgs.Empty);
		return true;
	}
}
