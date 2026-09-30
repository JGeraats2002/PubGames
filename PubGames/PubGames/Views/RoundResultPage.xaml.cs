using PubGames.ViewModels;

namespace PubGames.Views;

public partial class RoundResultPage : ContentPage
{
	private readonly RoundResultViewModel _vm;

	public RoundResultPage(RoundResultViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_vm.AskExtraSubgame = tie => DisplayAlertAsync("It's a tie",
			$"{tie} Play an extra subgame to decide?", "Play extra subgame", "End in a tie");
		_vm.Saved += async (_, finished) =>
		{
			if (!finished)
			{
				// Back to the scoreboard, which reloads for the next subgame.
				await Shell.Current.GoToAsync("..");
				return;
			}
			// Replace this page with the final result.
			await Shell.Current.GoToAsync($"../{nameof(StandingsPage)}", new Dictionary<string, object>
			{
				["sessionId"] = _vm.SessionId!,
				["final"] = true
			});
		};
	}

	private void OnRowTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is ResultRow { IsPicking: true } row)
			_vm.Toggle(row);
	}

	/// <summary>− moves towards 1st place.</summary>
	private void OnBetterPlaceClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: ResultRow row })
			_vm.ChangePlace(row, -1);
	}

	private void OnWorsePlaceClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: ResultRow row })
			_vm.ChangePlace(row, +1);
	}

	private async void OnConfirmClicked(object? sender, EventArgs e) => await _vm.ConfirmAsync();
}
