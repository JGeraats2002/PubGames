using PubGames.ViewModels;

namespace PubGames.Views;

public partial class GameRulesPage : ContentPage
{
	private readonly GameRulesViewModel _vm;

	public GameRulesPage(GameRulesViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;

		// "../" replaces the rules page with the scoreboard, so Back from the
		// scoreboard returns to the game list to pick the next game.
		vm.SessionStarted += async (_, sessionId) =>
			await Shell.Current.GoToAsync($"../{nameof(ScoreboardPage)}", new ShellNavigationQueryParameters
			{
				["sessionId"] = sessionId
			});
	}

	private void OnFewerSubgamesClicked(object? sender, EventArgs e) => _vm.ChangeSubgameCount(-1);

	private void OnMoreSubgamesClicked(object? sender, EventArgs e) => _vm.ChangeSubgameCount(+1);
}
