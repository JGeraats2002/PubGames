using PubGames.Views;

namespace PubGames;

public partial class AppShell : Shell
{
	static AppShell()
	{
		// Extra routes so we can push pages with parameters (e.g. scoreboard for a
		// specific session, or the game library filtered by category) on top of
		// the tab that's currently showing, instead of only swapping tabs.
		// Static so a new shell after each sign-in doesn't re-register them.
		Routing.RegisterRoute(nameof(ScoreboardPage), typeof(ScoreboardPage));
		Routing.RegisterRoute(nameof(GameLibraryPage), typeof(GameLibraryPage));
		Routing.RegisterRoute(nameof(HostCreateGamePage), typeof(HostCreateGamePage));
		Routing.RegisterRoute(nameof(HostTeamPage), typeof(HostTeamPage));
	}

	/// <param name="canHost">App admins and host team members get the Host tab; regular players don't.</param>
	public AppShell(bool canHost)
	{
		InitializeComponent();
		HostTab.IsVisible = canHost;
	}
}
