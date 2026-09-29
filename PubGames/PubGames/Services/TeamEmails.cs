using System.Text;
using PubGames.Models;

namespace PubGames.Services;

/// <summary>
/// The texts of the emails a team member receives. Written for someone who
/// has never seen the admin side of the app: after reading the "added" email
/// they should know what they may do and exactly how to do it. Keep in sync
/// with the button labels in the Admin pages.
/// </summary>
public static class TeamEmails
{
	/// <summary>Offered in the "remove moderator" popup; the last one asks for a custom text.</summary>
	public static readonly string[] RemovalReasons =
	[
		"No longer active as a moderator",
		"The moderator role is no longer needed",
		"Games didn't meet the Pub Games guidelines",
		"At your own request",
	];

	public const string OtherReason = "Other reason...";

	public static (string Subject, string Body) ModeratorAdded(TeamMember member, string adminName)
	{
		var yes = "  [YES] ";
		var no = "  [NO]  ";
		var b = new StringBuilder();

		b.AppendLine("Hi,");
		b.AppendLine();
		b.AppendLine($"{adminName} has made you a moderator on Pub Games. As a moderator you help keep the game library full and up to date for every player: you add new games and look after the existing ones.");
		b.AppendLine();
		b.AppendLine("This email explains what you can do and how to do it.");
		b.AppendLine();

		b.AppendLine("YOUR PERMISSIONS");
		b.AppendLine("----------------");
		b.AppendLine(yes + "See every game in the admin library, including drafts and games waiting for approval.");
		b.AppendLine(member.CanAddGames
			? yes + "Add new games and edit existing ones. Your changes go live for all players straight away."
			: yes + "Add new games. They are sent to an admin for approval first; players see a game once it's approved.");
		if (!member.CanAddGames)
			b.AppendLine(no + "Edit games that are already published - ask an admin if something needs changing.");
		b.AppendLine(member.CanDeleteGames
			? yes + "Delete games. A deleted game disappears from every player's library."
			: no + "Delete games - ask an admin if a game should go.");
		b.AppendLine(yes + "Upload cover images and pictures for the rules.");
		b.AppendLine(no + "Manage the team (adding or removing moderators, changing permissions) or approve requests. That's for admins only.");
		b.AppendLine();
		b.AppendLine("An admin can change these permissions at any time. You'll see the change the next time you open the app.");
		b.AppendLine();

		b.AppendLine("GETTING STARTED");
		b.AppendLine("---------------");
		b.AppendLine("1. Open the Pub Games app on your Android phone.");
		b.AppendLine($"2. Sign in with Google using exactly this address: {member.Email}");
		b.AppendLine("   Already signed in with it? Close the app completely and open it again.");
		b.AppendLine("3. An extra tab appears at the bottom of the screen: \"Admin\". Everything you do as a moderator happens there. The other tabs work as before, so you can still play games yourself.");
		b.AppendLine();

		b.AppendLine("ADDING A GAME");
		b.AppendLine("-------------");
		b.AppendLine("1. Go to the Admin tab > Library and tap \"+ New game\".");
		b.AppendLine("2. Fill in:");
		b.AppendLine("   - Game name: how the game shows up in the library.");
		b.AppendLine("   - Cover image (optional): tap \"Upload cover image\" and pick a photo. Players see it in the library and above the rules.");
		b.AppendLine("   - Pricing: leave it free, or tick \"This game is paid\" and enter the one-time price per player.");
		b.AppendLine("   - Scoring type: how points are kept on the scoreboard during the game.");
		b.AppendLine("   - How to play: type the rules the way you'd explain them at the table.");
		b.AppendLine("3. Pictures in the rules: tap in the text where the picture should go, then tap \"Add picture here\". The picture appears right there in the editor, exactly where players will see it. Tap the X on a picture to remove it.");
		b.AppendLine(member.CanAddGames
			? "4. Tap \"Publish game\". The game is live in everyone's library immediately."
			: "4. Tap \"Publish game\". The game is sent to an admin for approval. Once approved it's live in everyone's library.");
		b.AppendLine();

		b.AppendLine("EDITING OR DELETING A GAME");
		b.AppendLine("--------------------------");
		b.AppendLine(member.CanAddGames
			? "- Admin tab > Library > tap the game. Change what you want and tap \"Save changes\"."
			: "- Admin tab > Library > tap the game. Games that are still waiting for approval can be changed and sent again with \"Publish game\".");
		b.AppendLine(member.CanDeleteGames
			? "- To delete: open the game and tap \"Delete game\" at the bottom, then confirm."
			: "- Deleting games isn't part of your permissions; ask an admin.");
		b.AppendLine();

		b.AppendLine("GOOD TO KNOW");
		b.AppendLine("------------");
		b.AppendLine("- Adding, editing and deleting games needs an internet connection. Playing games works offline.");
		b.AppendLine("- Players see your changes the next time their library refreshes.");
		b.AppendLine();
		b.AppendLine("Questions? Just reply to this email.");
		b.AppendLine();
		b.AppendLine("Cheers,");
		b.AppendLine(adminName);
		b.AppendLine("Pub Games");

		return ("You're now a moderator on Pub Games", b.ToString());
	}

	public static (string Subject, string Body) MemberRemoved(TeamMember member, string reason, string adminName)
	{
		var role = member.RoleName.ToLowerInvariant();
		var b = new StringBuilder();

		b.AppendLine("Hi,");
		b.AppendLine();
		b.AppendLine($"{adminName} has ended your {role} role on Pub Games.");
		b.AppendLine();
		b.AppendLine($"Reason: {reason}");
		b.AppendLine();
		b.AppendLine("What this means for you:");
		b.AppendLine("- The Admin tab disappears the next time you open the app. You can no longer add, edit or delete games.");
		b.AppendLine("- Games you created stay in the library for everyone to play.");
		b.AppendLine("- You can keep using Pub Games as a player with the same account; nothing changes there.");
		b.AppendLine();
		b.AppendLine($"Thanks for your help as a {role}. If you think this is a mistake or have questions, just reply to this email.");
		b.AppendLine();
		b.AppendLine("Cheers,");
		b.AppendLine(adminName);
		b.AppendLine("Pub Games");

		return ($"Your {role} role on Pub Games has ended", b.ToString());
	}
}
