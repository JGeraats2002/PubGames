using PubGames.Models;
using PubGames.Services;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// The standings during a game, or the final result with Play again and back to the players.
/// </summary>
public partial class StandingsPage : ContentPage
{
	private readonly StandingsViewModel _vm;
	private readonly GameNight _gameNight;

	/// <summary>Test as player: Play again starts the test over, Quit stops it.</summary>
	private readonly TestRun _testRun;

	/// <summary>
	/// Sets up the standings page with its view model, the ⓘ help, and the game night to start Play again
	/// from.
	/// </summary>
	public StandingsPage(StandingsViewModel vm, GameNight gameNight, TestRun testRun)
	{
		InitializeComponent();
		_testRun = testRun;
		testRun.AttachStopButton(this);
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Standings(_vm.IsFinal));
		_gameNight = gameNight;
	}

	/// <summary>
	/// Stats: back to the scoreboard. Final result: play the same game again
	/// with the same players, or quit to the Players page, which then shows
	/// the players who were playing at the end.
	/// </summary>
	private async void OnDoneClicked(object? sender, EventArgs e)
	{
		if (!_vm.IsFinal)
		{
			await Shell.Current.GoToAsync("..");
			return;
		}

		if (_testRun.IsActive && _vm.AccountId == TestRun.AccountId)
		{
			// A test: nothing is kept. Play again starts the whole test over, at the rules; Quit ends the test.
			var restart = await DisplayAlertAsync(L.T("Test finished"),
				L.T("Play the test again from the start, or stop the test and go back to the game form?"),
				L.T("Play again"), L.T("Quit"));
			if (restart)
				await _testRun.RestartAsync();
			else
				await _testRun.StopAndReturnAsync();
			return;
		}

		var again = _vm.Game is not null && _vm.Lineup.Count > 0 && await DisplayAlertAsync(L.T("Play again?"),
			L.F("Play {0} again with {1}, or quit this game?", _vm.Game.Name, string.Join(", ", _vm.Lineup.Select(p => p.Name))),
			L.T("Play again"), L.T("Quit"));

		if (again)
		{
			// Straight into a new game: same players and seats, same number of subgames, no rules.
			// Back to the scoreboard underneath this page, which switches to the new session
			// (subgame 1, everyone at the starting score) when it appears.
			_gameNight.NextSessionId = await _gameNight.StartSessionAsync(_vm.Game!, _vm.Lineup, _vm.SubgameCount ?? 1, _vm.AccountId);
			await Shell.Current.GoToAsync("..");
			return;
		}

		_gameNight.NextLineup = _vm.Lineup;
		await Shell.Current.GoToAsync("//main/players");
	}
}
