using SQLite;

namespace PubGames.Models;

/// <summary>
/// A game in the library. Content (name/rules/images) is authored by a host;
/// categories and scoring type are set at creation; pricing is host-controlled
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

	/// <summary>Price in euros, e.g. 0.50m. Ignored when IsPaid is false. Host-set, not fixed by the platform.</summary>
	public decimal Price { get; set; }

	/// <summary>Which host org owns/manages this game.</summary>
	public string HostOrgId { get; set; } = string.Empty;

	/// <summary>Which specific host/subhost user authored it.</summary>
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
