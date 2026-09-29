using PubGames.Models;
using PubGames.Views;

namespace PubGames;

public partial class AppShell : Shell
{
	/// <summary>Admin tab > Requests page, from anywhere (tab bar / tab / page routes in AppShell.xaml).</summary>
	public const string RequestsRoute = "//main/admin/requests";

	/// <summary>Admin tab > Inbox page, from anywhere.</summary>
	public const string InboxRoute = "//main/admin/inbox";

	static AppShell()
	{
		// Pages pushed on top of a tab with parameters: the game-night flow
		// (choose game > rules > scoreboard), the create/edit page and the
		// review of one request.
		// Static so a new shell after each sign-in doesn't re-register them.
		Routing.RegisterRoute(nameof(GameLibraryPage), typeof(GameLibraryPage));
		Routing.RegisterRoute(nameof(GameRulesPage), typeof(GameRulesPage));
		Routing.RegisterRoute(nameof(ScoreboardPage), typeof(ScoreboardPage));
		Routing.RegisterRoute(nameof(AdminCreateGamePage), typeof(AdminCreateGamePage));
		Routing.RegisterRoute(nameof(ReviewRequestPage), typeof(ReviewRequestPage));
		Routing.RegisterRoute(nameof(CategoryGamesPage), typeof(CategoryGamesPage));
		Routing.RegisterRoute(nameof(SessionPlayersPage), typeof(SessionPlayersPage));
	}

	/// <param name="me">
	/// The user's team membership: admins and moderators get the Admin tab,
	/// only admins its Requests and Team pages; regular players (null) get none.
	/// </param>
	public AppShell(TeamMember? me)
	{
		InitializeComponent();
		AdminTab.IsVisible = me is not null;
		RequestsContent.IsVisible = me?.IsAdmin == true;
		TeamContent.IsVisible = me?.IsAdmin == true;
	}
}
