using SQLite;

namespace PubGames.Models;

/// <summary>
/// A player profile tied to the logged-in account. Saved players are reused
/// across many game sessions so score history can be tracked per person, per
/// game; a one-off player added mid-game without saving is IsTemporary.
/// </summary>
public class Player
{
	/// <summary>The player's unique id.</summary>
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	/// <summary>The account (user) this player profile belongs to / was saved under.</summary>
	public string OwnerAccountId { get; set; } = string.Empty;

	/// <summary>The player's name, as shown in the game.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>The initials shown when there's no picture: "JD".</summary>
	public string? AvatarInitials { get; set; }

	/// <summary>When the player was added (UTC).</summary>
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	/// <summary>
	/// Added to one game only, not to "Saved players". Kept so that game's
	/// scores still have a name.
	/// </summary>
	public bool IsTemporary { get; set; }

	/// <summary>
	/// The account of the person this player is, when added by their personal
	/// code (or the user themselves). Their results then count in their stats.
	/// </summary>
	public string? LinkedAccountId { get; set; }

	/// <summary>The profile picture of a linked player.</summary>
	public string? PhotoRef { get; set; }

	/// <summary>
	/// The user gave a linked player their own name for them (only on this
	/// phone), so it's not replaced by the player's nickname from their profile.
	/// </summary>
	public bool HasCustomName { get; set; }

	/// <summary>Added by their personal code: the game counts in their stats.</summary>
	[Ignore]
	public bool IsLinked => !string.IsNullOrEmpty(LinkedAccountId);

	/// <summary>Whether the player has a profile picture.</summary>
	[Ignore]
	public bool HasPhoto => !string.IsNullOrEmpty(PhotoRef);

	/// <summary>No profile picture: their initials show instead.</summary>
	[Ignore]
	public bool HasNoPhoto => !HasPhoto;

	/// <summary>"Jan de Vries" → "JD", "Sanne" → "SA".</summary>
	public static string InitialsFrom(string name)
	{
		var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length >= 2)
			return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
		var single = parts.Length == 1 ? parts[0] : string.Empty;
		return (single.Length >= 2 ? single[..2] : single).ToUpperInvariant();
	}
}
