using PubGames.ViewModels;

namespace PubGames.Views;

public partial class GameRulesPage : ContentPage
{
	public GameRulesPage(GameRulesViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;

		// "../" replaces the rules page with the scoreboard, so Back from the
		// scoreboard returns to the game list to pick the next game.
		vm.SessionStarted += async (_, sessionId) =>
			await Shell.Current.GoToAsync($"../{nameof(ScoreboardPage)}", new ShellNavigationQueryParameters
			{
				["sessionId"] = sessionId
			});
	}
}
