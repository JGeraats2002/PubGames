using System.Text;
using PubGames.Models;

namespace PubGames.Services;

/// <summary>
/// The texts of the emails a team member receives, always in English. The
/// "added" email holds the agreements (AdminAgreements) and how to get
/// started; how the admin pages work is under their ⓘ buttons. Keep the
/// button names in sync with the app.
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

	/// <summary>
	/// The email to a new moderator: the agreements, the same as on the Agreements page in the app, and how to
	/// get started. How to make a game is not explained here: that's under the ⓘ button in the app.
	/// </summary>
	public static (string Subject, string Body) ModeratorAdded(TeamMember member, string adminName)
	{
		var b = new StringBuilder();

		b.AppendLine("Hi,");
		b.AppendLine();
		b.AppendLine($"{adminName} has made you a moderator on Pub Games. Welcome to the team!");
		b.AppendLine();

		b.AppendLine("WHAT YOU DO");
		b.AppendLine("-----------");
		b.AppendLine("As a team member you create games for the library. Moderators submit new games and changes for review: an admin approves them before players see anything. Admins publish straight away.");
		b.AppendLine("How it works step by step is explained in the app, under the ⓘ button at the top of every admin page.");
		b.AppendLine();

		b.AppendLine("GETTING STARTED");
		b.AppendLine("---------------");
		b.AppendLine("1. Open the Pub Games app on your Android phone.");
		b.AppendLine($"2. Sign in with Google using exactly this address: {member.Email}");
		b.AppendLine("   Already signed in with it? Close the app completely and open it again.");
		b.AppendLine("3. An extra tab appears at the bottom of the screen: \"Admin\". The first time, it opens the agreements below. Read them and tap \"I've read this and agree\" at the end to open the admin pages.");
		b.AppendLine();

		b.AppendLine("THE AGREEMENTS");
		b.AppendLine("--------------");
		b.AppendLine("The same agreements as in the app (Admin tab > Agreements). They apply to admins and moderators alike.");
		b.AppendLine();
		foreach (var section in AdminAgreements.EnglishSections)
		{
			b.AppendLine(section.Heading.ToUpperInvariant());
			b.AppendLine(section.Text);
			b.AppendLine();
		}

		b.AppendLine("Questions? Just reply to this email.");
		b.AppendLine();
		b.AppendLine("Cheers,");
		b.AppendLine(adminName);
		b.AppendLine("Pub Games");

		return ("You're now a moderator on Pub Games", b.ToString());
	}

	/// <summary>The email to a team member who was removed, with the reason.</summary>
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
