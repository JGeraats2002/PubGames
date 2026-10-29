using PubGames.Models;
using PubGames.Views;

namespace PubGames;

/// <summary>
/// The main screens: the tab bar (Players, Admin, Account) and the routes to every page opened from them.
/// </summary>
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
		Routing.RegisterRoute(nameof(RoundResultPage), typeof(RoundResultPage));
		Routing.RegisterRoute(nameof(StandingsPage), typeof(StandingsPage));
		Routing.RegisterRoute(nameof(MyStatsPage), typeof(MyStatsPage));
		Routing.RegisterRoute(nameof(GameStatsPage), typeof(GameStatsPage));
		Routing.RegisterRoute(nameof(SavedGamesPage), typeof(SavedGamesPage));
		Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
		Routing.RegisterRoute(nameof(FeedbackPage), typeof(FeedbackPage));
		Routing.RegisterRoute(nameof(BlockedPlayersPage), typeof(BlockedPlayersPage));
		Routing.RegisterRoute(nameof(SubscriptionPage), typeof(SubscriptionPage));
		Routing.RegisterRoute(nameof(LanguagePage), typeof(LanguagePage));
	}

	/// <param name="me">
	/// The user's team membership: admins and moderators get the Admin tab,
	/// only admins its Requests, Feedback and Team pages; regular players (null) get none.
	/// </param>
	public AppShell(TeamMember? me)
	{
		InitializeComponent();
		AdminTab.IsVisible = me is not null;
		RequestsContent.IsVisible = me?.IsAdmin == true;
		TeamContent.IsVisible = me?.IsAdmin == true;
		FeedbackContent.IsVisible = me?.IsAdmin == true;
	}

	/// <summary>Going to another page (a tab, a page opening or going back): the keyboard closes.</summary>
	protected override void OnNavigating(ShellNavigatingEventArgs args)
	{
		Views.SoftKeyboard.Hide();
		base.OnNavigating(args);
	}
}
