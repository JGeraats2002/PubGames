using PubGames.Services;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class StandingsPage : ContentPage
{
	private readonly StandingsViewModel _vm;
	private readonly GameNight _gameNight;

	public StandingsPage(StandingsViewModel vm, GameNight gameNight)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
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

		var again = _vm.Game is not null && _vm.Lineup.Count > 0 && await DisplayAlertAsync("Play again?",
			$"Play {_vm.Game.Name} again with {string.Join(", ", _vm.Lineup.Select(p => p.Name))}, or quit this game?",
			"Play again", "Quit");

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
