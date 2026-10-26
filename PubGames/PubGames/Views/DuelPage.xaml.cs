using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// The duel popup, shown as a modal page. Before the duel counts the players
/// are asked whether the score is right. Use PlayAsync.
/// </summary>
public partial class DuelPage : ContentPage
{
	private readonly DuelViewModel _vm;
	private readonly TaskCompletionSource<Dictionary<string, int>?> _result = new();

	private DuelPage(DuelViewModel vm)
	{
		InitializeComponent();
		// The page ends above the keyboard, so the box being typed in is never behind it.
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Default, SafeAreaRegions.Default, SafeAreaRegions.Default, SafeAreaRegions.SoftInput);
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

	private void OnMinusClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: DuelRow row })
			_vm.Adjust(row, -1);
	}

	private void OnPlusClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: DuelRow row })
			_vm.Adjust(row, +1);
	}

	private async void OnDoneClicked(object? sender, EventArgs e)
	{
		if (_vm.Scores() is not { } scores) return;
		if (await DisplayAlertAsync("Is this score correct?", _vm.ScoreLines, "Yes, it's correct", "No, change it"))
			_result.TrySetResult(scores);
	}

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
