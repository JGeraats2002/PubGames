using SQLite;

namespace PubGames.Models;

/// <summary>
/// A player profile tied to the logged-in account. Saved players are reused
/// across many game sessions so score history can be tracked per person, per
/// game; a one-off player added mid-game without saving is IsTemporary.
/// </summary>
public class Player
{
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	/// <summary>The account (user) this player profile belongs to / was saved under.</summary>
	public string OwnerAccountId { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string? AvatarInitials { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	/// <summary>
	/// Added to one game only, not to "Saved players". Kept so that game's
	/// scores still have a name.
	/// </summary>
	public bool IsTemporary { get; set; }

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
