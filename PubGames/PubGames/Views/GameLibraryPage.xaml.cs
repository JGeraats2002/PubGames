using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// Players tab &gt; Choose a game: every game, with search and filters; paid games show a lock until they're
/// unlocked.
/// </summary>
public partial class GameLibraryPage : ContentPage
{
	private readonly GameLibraryViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public GameLibraryPage(GameLibraryViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.GameLibrary);
	}

	/// <summary>
	/// Every time the page shows: the games are loaded fresh, and a pass about to end (or just ended) is
	/// mentioned once a day.
	/// </summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
		if (_vm.PassNotice() is { } notice)
			await DisplayAlertAsync(L.T("Pub Games Pass"), notice, "OK");
	}

	/// <summary>Opens the filters (number of players, categories).</summary>
	private async void OnFilterClicked(object? sender, EventArgs e) => await FilterPage.ShowAsync(Navigation, _vm.Filters);

	/// <summary>
	/// Opens the game's rules - also for a paid game that isn't unlocked, which can be unlocked there - when
	/// the number of players fits.
	/// </summary>
	private async void OnGameTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not PubGame game) return;

		// Every game stays in the list, but it only opens with a number of players it's made for.
		if (game.PlayerCountProblem(_vm.Players.Count) is { } problem)
		{
			await DisplayAlertAsync(L.T("Not the right number of players"), problem, "OK");
			return;
		}

		// A paid game that isn't unlocked opens too: its rules can be read first; Unlock is on that page.
		await Shell.Current.GoToAsync(nameof(GameRulesPage), new ShellNavigationQueryParameters
		{
			["game"] = game,
			["players"] = _vm.Players
		});
	}
}
