using PubGames.Models;
using PubGames.Views;

namespace PubGames;

public partial class AppShell : Shell
{
	static AppShell()
	{
		// Pages pushed on top of a tab with parameters: the game-night flow
		// (choose game > rules > scoreboard) and the admin create/edit page.
		// Static so a new shell after each sign-in doesn't re-register them.
		Routing.RegisterRoute(nameof(GameLibraryPage), typeof(GameLibraryPage));
		Routing.RegisterRoute(nameof(GameRulesPage), typeof(GameRulesPage));
		Routing.RegisterRoute(nameof(ScoreboardPage), typeof(ScoreboardPage));
		Routing.RegisterRoute(nameof(AdminCreateGamePage), typeof(AdminCreateGamePage));
	}

	/// <param name="me">
	/// The user's team membership: admins and moderators get the Admin tab,
	/// only admins its Team page; regular players (null) get neither.
	/// </param>
	public AppShell(TeamMember? me)
	{
		InitializeComponent();
		AdminTab.IsVisible = me is not null;
		TeamContent.IsVisible = me?.HasFullRights == true;
	}
}
