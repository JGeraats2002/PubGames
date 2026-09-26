using SQLite;

namespace PubGames.Models;

/// <summary>A host "team". Can have several head hosts (promotion adds a peer, not a replacement).</summary>
public class HostOrg
{
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	public string Name { get; set; } = string.Empty;
}

public enum HostRole
{
	HeadHost,
	Subhost
}

/// <summary>
/// One user's membership + permissions within a host org. Deletion/publish
/// rights are checked from here at request time, not inferred from Role
/// alone, since a subhost's permissions can be changed at any point by a
/// head host.
/// </summary>
public class HostMember
{
	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }

	[Indexed]
	public string HostOrgId { get; set; } = string.Empty;

	[Indexed]
	public string UserId { get; set; } = string.Empty;

	public HostRole Role { get; set; } = HostRole.Subhost;

	/// <summary>Subhosts default to true; irrelevant once Role == HeadHost (head hosts always can).</summary>
	public bool CanAddGames { get; set; } = true;

	/// <summary>Subhosts default to false; irrelevant once Role == HeadHost (head hosts always can).</summary>
	public bool CanDeleteGames { get; set; }

	public bool HasFullRights => Role == HostRole.HeadHost;
}
