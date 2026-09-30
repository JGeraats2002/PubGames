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

	/// <summary>The scoring type's settings (step, min, max, ...) as JSON; use ScoringSettings.</summary>
	public string? ScoringSettingsText { get; set; }

	[Ignore]
	public ScoringSettings ScoringSettings
	{
		get => ScoringSettings.FromJson(ScoringSettingsText);
		set => ScoringSettingsText = value.ToJson();
	}

	[Ignore]
	public string ScoringLabel => ScoringType == ScoringType.PlusMinus
		? $"{ScoringTypeOption.LabelFor(ScoringType)} ({ScoringSettings.Summary})"
		: $"{ScoringTypeOption.LabelFor(ScoringType)} ({ScoringSettings.ResultSummary})";

	public bool IsPaid { get; set; }

	/// <summary>Price in euros, e.g. 0.50m. Ignored when IsPaid is false. Admin-set, not fixed by the platform.</summary>
	public decimal Price { get; set; }

	/// <summary>GameCategory ids, comma-separated for SQLite; use CategoryIds. Every game needs at least one.</summary>
	public string CategoryIdsText { get; set; } = string.Empty;

	[Ignore]
	public List<string> CategoryIds
	{
		get => CategoryIdsText.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
		set => CategoryIdsText = string.Join(',', value.Distinct());
	}

	/// <summary>Category names for display, filled in by whoever shows the game (names live on GameCategory).</summary>
	[Ignore]
	public string CategoryLabel { get; set; } = string.Empty;

	[Ignore]
	public bool HasNoCategory => string.IsNullOrEmpty(CategoryIdsText);

	/// <summary>Which team owns/manages this game.</summary>
	public string TeamId { get; set; } = string.Empty;

	/// <summary>Which admin/moderator account authored it.</summary>
	public string CreatedByUserId { get; set; } = string.Empty;

	public GameStatus Status { get; set; } = GameStatus.Draft;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime? PublishedAt { get; set; }

	[Ignore]
	public string StatusLabel => Status switch
	{
		GameStatus.Published => "Published",
		GameStatus.Deleted => "Deleted",
		GameStatus.Draft => "Draft",
		// Games from before review requests existed.
		_ => "Waiting for review"
	};

	[Ignore]
	public string PriceLabel => IsPaid ? Price.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("nl-NL")) : "Free";
}

/// <summary>
/// Stored as a number on the phone, so the order must not change. The two
/// Pending* values are no longer used: moderators' proposals are review
/// requests now and don't change the game itself.
/// </summary>
public enum GameStatus
{
	Draft,
	PendingPublishApproval,
	Published,
	PendingDeletionApproval,
	Deleted
}
