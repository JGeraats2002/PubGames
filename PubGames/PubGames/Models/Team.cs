using SQLite;

namespace PubGames.Models;

/// <summary>An admin team. Can have several admins (promotion adds a peer, not a replacement).</summary>
public class Team
{
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	public string Name { get; set; } = string.Empty;
}

public enum TeamRole
{
	Admin,
	Moderator
}

/// <summary>
/// One person's membership + permissions within a team, keyed by the Google
/// account email an admin added them with (they may not have signed in yet,
/// so there's no uid). Mirrors the cloud "team" collection, which is what
/// firestore.rules check. Deletion/publish rights are checked from here at
/// request time, not inferred from Role alone, since a moderator's
/// permissions can be changed at any point by an admin.
/// </summary>
public class TeamMember
{
	/// <summary>Lower-case, so it matches however the person typed it when signing in.</summary>
	[PrimaryKey]
	public string Email { get; set; } = string.Empty;

	[Indexed]
	public string TeamId { get; set; } = string.Empty;

	/// <summary>Optional friendlier name for the Team page; the email is shown when empty.</summary>
	public string DisplayName { get; set; } = string.Empty;

	[Ignore]
	public string Label => string.IsNullOrEmpty(DisplayName) ? Email : DisplayName;

	public TeamRole Role { get; set; } = TeamRole.Moderator;

	/// <summary>Moderators default to true; irrelevant once Role == Admin (admins always can).</summary>
	public bool CanAddGames { get; set; } = true;

	/// <summary>Moderators default to false; irrelevant once Role == Admin (admins always can).</summary>
	public bool CanDeleteGames { get; set; }

	public DateTime AddedAt { get; set; } = DateTime.UtcNow;

	/// <summary>
	/// Admins listed in AuthConfig.AdminEmails. They aren't stored in the team
	/// collection and can't be removed or demoted from inside the app.
	/// </summary>
	[Ignore]
	public bool IsBuiltInAdmin { get; set; }

	public bool HasFullRights => Role == TeamRole.Admin;

	[Ignore]
	public string RoleName => Role == TeamRole.Admin ? "Admin" : "Moderator";

	public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
