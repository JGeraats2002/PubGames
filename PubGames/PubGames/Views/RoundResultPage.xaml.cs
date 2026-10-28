using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// The result of a subgame: filled in from the scores, to check and change, with a duel for ties when the
/// game wants that.
/// </summary>
public partial class RoundResultPage : ContentPage
{
	private readonly RoundResultViewModel _vm;

	/// <summary>
	/// Sets up the result page: its view model, the ⓘ help, and the questions it may ask (extra subgame,
	/// duel) and where it goes once saved.
	/// </summary>
	public RoundResultPage(RoundResultViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.RoundResult(_vm.Mode, _vm.LowestWins, _vm.Settings));
		_vm.AskExtraSubgame = tie => DisplayAlertAsync(L.T("It's a tie"),
			L.F("{0} Play an extra subgame to decide?", tie), L.T("Play extra subgame"), L.T("End in a tie"));
		_vm.PlayDuel = setup => DuelPage.PlayAsync(this, setup);
		_vm.Inform = (title, message) => DisplayAlertAsync(title, message, "OK");
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

	/// <summary>Marks the tapped player as winner or loser, or takes the mark off.</summary>
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

	/// <summary>Moves a player one place down.</summary>
	private void OnWorsePlaceClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: ResultRow row })
			_vm.ChangePlace(row, +1);
	}

	/// <summary>Saves the result and goes on.</summary>
	private async void OnConfirmClicked(object? sender, EventArgs e) => await _vm.ConfirmAsync();
}
