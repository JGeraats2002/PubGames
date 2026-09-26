using SQLite;

namespace PubGames.Models;

/// <summary>
/// A saved player profile tied to the logged-in account. Reused across many
/// game sessions so score history can be tracked per person, per game.
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
}
