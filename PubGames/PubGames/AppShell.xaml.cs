using PubGames.Views;

namespace PubGames;

public partial class AppShell : Shell
{
	static AppShell()
	{
		// Pages pushed on top of a tab with parameters: the game-night flow
		// (choose game → rules → scoreboard) and the host's create/edit page.
		// Static so a new shell after each sign-in doesn't re-register them.
		Routing.RegisterRoute(nameof(GameLibraryPage), typeof(GameLibraryPage));
		Routing.RegisterRoute(nameof(GameRulesPage), typeof(GameRulesPage));
		Routing.RegisterRoute(nameof(ScoreboardPage), typeof(ScoreboardPage));
		Routing.RegisterRoute(nameof(HostCreateGamePage), typeof(HostCreateGamePage));
	}

	/// <param name="canHost">App admins and host team members get the Host tab; regular players don't.</param>
	public AppShell(bool canHost)
	{
		InitializeComponent();
		HostTab.IsVisible = canHost;
	}
}
