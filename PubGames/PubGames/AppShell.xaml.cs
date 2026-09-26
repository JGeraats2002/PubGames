using PubGames.Views;

namespace PubGames;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Extra routes so we can push pages with parameters (e.g. scoreboard for a
		// specific session, or the game library filtered by category) on top of
		// the tab that's currently showing, instead of only swapping tabs.
		Routing.RegisterRoute(nameof(ScoreboardPage), typeof(ScoreboardPage));
		Routing.RegisterRoute(nameof(GameLibraryPage), typeof(GameLibraryPage));
		Routing.RegisterRoute(nameof(HostCreateGamePage), typeof(HostCreateGamePage));
		Routing.RegisterRoute(nameof(HostTeamPage), typeof(HostTeamPage));
	}
}
