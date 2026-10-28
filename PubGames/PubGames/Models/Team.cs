using SQLite;

namespace PubGames.Models;

/// <summary>An admin team. Can have several admins (promotion adds a peer, not a replacement).</summary>
public class Team
{
	/// <summary>The team's unique id.</summary>
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	/// <summary>The team's name.</summary>
	public string Name { get; set; } = string.Empty;
}

/// <summary>What a team member may do: admins everything, moderators propose changes for review.</summary>
public enum TeamRole
{
	Admin,
	Moderator
}

/// <summary>
/// One person's membership within a team, keyed by the Google account email
/// an admin added them with (they may not have signed in yet, so there's no
/// uid). Mirrors the cloud "team" collection, which is what firestore.rules
/// check. The role decides everything: admins change games directly,
/// moderators submit review requests for an admin to approve.
/// </summary>
public class TeamMember
{
	/// <summary>Lower-case, so it matches however the person typed it when signing in.</summary>
	[PrimaryKey]
	public string Email { get; set; } = string.Empty;

	/// <summary>The team the member is on.</summary>
	[Indexed]
	public string TeamId { get; set; } = string.Empty;

	/// <summary>Optional friendlier name for the Team page; the email is shown when empty.</summary>
	public string DisplayName { get; set; } = string.Empty;

	/// <summary>The member's name, or their email without a name.</summary>
	[Ignore]
	public string Label => string.IsNullOrEmpty(DisplayName) ? Email : DisplayName;

	/// <summary>Admin or moderator.</summary>
	public TeamRole Role { get; set; } = TeamRole.Moderator;

	/// <summary>The language the member uses the app in (en, nl, ...; see AppLanguage); empty when not known yet.</summary>
	public string Language { get; set; } = string.Empty;

	/// <summary>The member's language, or null when not known yet.</summary>
	[Ignore]
	public Services.AppLanguageInfo? LanguageInfo => Services.AppLanguage.Find(Language);

	/// <summary>The flag of the member's language (see AppLanguageInfo.Flag); null when not known.</summary>
	[Ignore]
	public string? LanguageFlag => LanguageInfo?.Flag;

	/// <summary>The member's language is known, so its flag shows.</summary>
	[Ignore]
	public bool HasLanguage => LanguageInfo is not null;

	/// <summary>The member's language by name, for the flag's description and the search.</summary>
	[Ignore]
	public string LanguageName => LanguageInfo?.Name ?? string.Empty;

	/// <summary>When the member was added (UTC).</summary>
	public DateTime AddedAt { get; set; } = DateTime.UtcNow;

	/// <summary>
	/// Admins listed in AuthConfig.AdminEmails. They aren't stored in the team
	/// collection and can't be removed or demoted from inside the app.
	/// </summary>
	[Ignore]
	public bool IsBuiltInAdmin { get; set; }

	/// <summary>Whether the member is an admin.</summary>
	public bool IsAdmin => Role == TeamRole.Admin;

	// What the person looking at the Team page may do with this member (set by AdminTeamViewModel).
	[Ignore]
	public bool CanPromote { get; set; }

	/// <summary>Whether the user may make this admin a moderator (only the owner).</summary>
	[Ignore]
	public bool CanDemote { get; set; }

	/// <summary>Whether the user may remove this member from the team.</summary>
	[Ignore]
	public bool CanRemove { get; set; }

	/// <summary>Whether the user can do anything with this member: the buttons show.</summary>
	[Ignore]
	public bool HasActions => CanPromote || CanDemote || CanRemove;

	/// <summary>"Admin" or "Moderator".</summary>
	[Ignore]
	public string RoleName => Role == TeamRole.Admin ? L.T("Admin") : L.T("Moderator");

	/// <summary>An email in one form (no spaces, lower case), so the same address always matches.</summary>
	public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
