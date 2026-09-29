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
		const string yes = "  [YES] ";
		const string no = "  [NO]  ";
		var b = new StringBuilder();

		b.AppendLine("Hi,");
		b.AppendLine();
		b.AppendLine($"{adminName} has made you a moderator on Pub Games. As a moderator you help keep the game library full and up to date: you propose new games, changes to existing games and deletions, and an admin reviews each proposal before players see anything.");
		b.AppendLine();
		b.AppendLine("This email explains what you can do and how to do it.");
		b.AppendLine();

		b.AppendLine("HOW IT WORKS: REVIEW REQUESTS");
		b.AppendLine("-----------------------------");
		b.AppendLine("Nothing you do changes a game right away. Every proposal is sent to an admin as a \"review request\". There are four kinds:");
		b.AppendLine("  - New game: a game that isn't in the library yet.");
		b.AppendLine("  - Changes to a game: a new version of an existing game.");
		b.AppendLine("  - Deletion: removing a game from every player's library.");
		b.AppendLine("  - New category: a new way to group games, like \"Party\".");
		b.AppendLine();
		b.AppendLine("Each request has a status:");
		b.AppendLine("  - Waiting for review: an admin hasn't decided yet. You can still change it or withdraw it.");
		b.AppendLine("  - Approved: the admin accepted it and players now see the result.");
		b.AppendLine("  - Rejected: the admin didn't accept it and tells you why, so you can fix it and submit it again.");
		b.AppendLine();
		b.AppendLine("The admin's decision - with the reason, if it's rejected - arrives as a message in your Inbox (Admin tab > Inbox).");
		b.AppendLine();
		b.AppendLine("Until an admin approves, players keep seeing the current version of the game.");
		b.AppendLine();

		b.AppendLine("WHAT YOU CAN AND CAN'T DO");
		b.AppendLine("-------------------------");
		b.AppendLine(yes + "See every game in the admin library.");
		b.AppendLine(yes + "Propose new games, changes to games, deletions and new categories.");
		b.AppendLine(yes + "Send short messages to the admins, and reply to messages you receive.");
		b.AppendLine(yes + "Upload cover images and pictures for the rules.");
		b.AppendLine(yes + "Follow your requests, and change or withdraw them while they're waiting for review.");
		b.AppendLine(no + "Publish, change or delete games directly - an admin always reviews first.");
		b.AppendLine(no + "Review requests or manage the team. That's for admins only.");
		b.AppendLine();

		b.AppendLine("GETTING STARTED");
		b.AppendLine("---------------");
		b.AppendLine("1. Open the Pub Games app on your Android phone.");
		b.AppendLine($"2. Sign in with Google using exactly this address: {member.Email}");
		b.AppendLine("   Already signed in with it? Close the app completely and open it again.");
		b.AppendLine("3. An extra tab appears at the bottom of the screen: \"Admin\". Everything you do as a moderator happens there, on the Library page. The other tabs work as before, so you can still play games yourself.");
		b.AppendLine();

		b.AppendLine("PROPOSING A NEW GAME");
		b.AppendLine("--------------------");
		b.AppendLine("1. Admin tab > Library > tap \"+ Propose a new game\".");
		b.AppendLine("2. Fill in:");
		b.AppendLine("   - Game name: how the game shows up in the library.");
		b.AppendLine("   - Cover image (optional): tap \"Upload cover image\" and pick a photo. Players see it in the library and above the rules.");
		b.AppendLine("   - Pricing: leave it free, or tick \"This game is paid\" and enter the one-time price per player.");
		b.AppendLine("   - Scoring type: how points are kept on the scoreboard during the game.");
		b.AppendLine("   - Categories: tap every category the game belongs in (at least one), e.g. Pub and Cards. Players find games by choosing a category.");
		b.AppendLine("   - How to play: type the rules the way you'd explain them at the table.");
		b.AppendLine("3. Pictures in the rules: tap in the text where the picture should go, then tap \"Add picture here\". The picture appears right there in the editor, exactly where players will see it. Tap the X on a picture to remove it.");
		b.AppendLine("4. Tap \"Submit for review\".");
		b.AppendLine();

		b.AppendLine("PROPOSING CHANGES TO A GAME");
		b.AppendLine("---------------------------");
		b.AppendLine("1. Admin tab > Library > tap the game.");
		b.AppendLine("2. Change what you want, then tap \"Submit changes for review\".");
		b.AppendLine("   The admin sees exactly what you changed and a preview of the new version.");
		b.AppendLine();

		b.AppendLine("REQUESTING A DELETION");
		b.AppendLine("---------------------");
		b.AppendLine("1. Admin tab > Library > tap the game.");
		b.AppendLine("2. Tap \"Request deletion\" at the bottom and confirm. The game stays playable until an admin approves.");
		b.AppendLine();

		b.AppendLine("PROPOSING A NEW CATEGORY");
		b.AppendLine("------------------------");
		b.AppendLine("1. Admin tab > Categories > tap \"+ Propose a new category\".");
		b.AppendLine("2. Type the name and tap \"Submit for review\". Once approved, an admin chooses which games belong in it.");
		b.AppendLine();

		b.AppendLine("FOLLOWING YOUR REQUESTS");
		b.AppendLine("-----------------------");
		b.AppendLine("- Admin tab > Library shows \"My requests waiting for review\" at the top.");
		b.AppendLine("- Tap one to change it (submitting again replaces it) or to withdraw it.");
		b.AppendLine("- When an admin decides, you get a message in your Inbox. For a rejected game or change, tap \"Edit and submit again\" in that message to fix it and send it back.");
		b.AppendLine();

		b.AppendLine("YOUR INBOX");
		b.AppendLine("----------");
		b.AppendLine("- Admin tab > Inbox holds review decisions and messages from admins. The Library shows a banner when something new arrives.");
		b.AppendLine("- Each message has \"Reply\" and \"Delete\". Keep a message by simply leaving it there.");
		b.AppendLine("- Tap \"+ New message\" to send a short message (up to 500 characters) to the admins.");
		b.AppendLine();

		b.AppendLine("GOOD TO KNOW");
		b.AppendLine("------------");
		b.AppendLine("- Submitting requests needs an internet connection. Playing games works offline.");
		b.AppendLine("- Once approved, players see the result the next time their library refreshes.");
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
		b.AppendLine("- The Admin tab disappears the next time you open the app. You can no longer propose new games, changes or deletions.");
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
