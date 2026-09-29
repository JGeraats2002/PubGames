using SQLite;

namespace PubGames.Models;

/// <summary>
/// A game in the library. Content (name/rules/images) is authored by an admin or moderator;
/// categories and scoring type are set at creation; pricing is admin-controlled
/// per the "only some games are paid" decision.
/// </summary>
public class PubGame
{
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	public string Name { get; set; } = string.Empty;

	/// <summary>Rich-text/markdown explanation of how to play, may reference inline image URLs.</summary>
	public string RulesText { get; set; } = string.Empty;

	public string? CoverImageUrl { get; set; }

	public ScoringType ScoringType { get; set; } = ScoringType.PointTally;

	public bool IsPaid { get; set; }

	/// <summary>Price in euros, e.g. 0.50m. Ignored when IsPaid is false. Admin-set, not fixed by the platform.</summary>
	public decimal Price { get; set; }

	/// <summary>Which team owns/manages this game.</summary>
	public string TeamId { get; set; } = string.Empty;

	/// <summary>Which admin/moderator account authored it.</summary>
	public string CreatedByUserId { get; set; } = string.Empty;

	public GameStatus Status { get; set; } = GameStatus.Draft;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime? PublishedAt { get; set; }
}

public enum GameStatus
{
	Draft,
	PendingPublishApproval,
	Published,
	PendingDeletionApproval,
	Deleted
}
