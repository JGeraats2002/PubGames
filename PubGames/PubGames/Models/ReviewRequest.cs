using System.Globalization;
using SQLite;

namespace PubGames.Models;

/// <summary>What an admin is asked to do.</summary>
public enum ReviewRequestType
{
	/// <summary>Add a game that doesn't exist yet (from a moderator).</summary>
	NewGame,

	/// <summary>Replace the content of an existing game (from a moderator).</summary>
	GameChanges,

	/// <summary>Remove a game from every player's library (from a moderator).</summary>
	Deletion,

	/// <summary>Add a category (from a moderator).</summary>
	NewCategory,

	/// <summary>
	/// Pick the games that belong in a category that was just added. Created
	/// automatically for admins whenever a category is added.
	/// </summary>
	AddGamesToCategory
}

public enum ReviewStatus
{
	/// <summary>No admin has decided yet. A moderator can still change or withdraw their request.</summary>
	WaitingForReview,
	Approved,
	Rejected
}

/// <summary>
/// Something an admin has to review. Moderators never change games or
/// categories directly: a new game, changes to a game, a deletion or a new
/// category are all submitted as a review request, and players keep seeing
/// the current version until an admin approves it. For new games and changes,
/// the full proposed version travels with the request (the Proposed* fields),
/// so the admin sees exactly what would go live. The decision reaches the
/// moderator as a message in their Inbox. Stored in the cloud "reviewRequests"
/// collection.
/// </summary>
public class ReviewRequest
{
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	/// <summary>The game this is about; for a new game, the id it gets once approved. Empty for category requests.</summary>
	[Indexed]
	public string GameId { get; set; } = string.Empty;

	/// <summary>
	/// The category this is about: for a new category, the id it gets once
	/// approved; for adding games to a category, that category.
	/// </summary>
	public string CategoryId { get; set; } = string.Empty;

	[Indexed]
	public string TeamId { get; set; } = string.Empty;

	public ReviewRequestType Type { get; set; }

	/// <summary>What the request is about as shown on cards: the game's or category's name (the proposed one for new items and changes).</summary>
	public string SubjectName { get; set; } = string.Empty;

	public string SubmittedByUserId { get; set; } = string.Empty;

	/// <summary>Checked by firestore.rules: requests can only be submitted in your own name.</summary>
	[Indexed]
	public string SubmittedByEmail { get; set; } = string.Empty;

	public string SubmittedByName { get; set; } = string.Empty;

	public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

	public ReviewStatus Status { get; set; } = ReviewStatus.WaitingForReview;

	public string? ReviewedByUserId { get; set; }

	public DateTime? ReviewedAt { get; set; }

	/// <summary>Why an admin rejected it; also sent to the moderator's Inbox.</summary>
	public string ReviewNote { get; set; } = string.Empty;

	// The proposed version of the game (new games and changes; empty otherwise).
	// ProposedName is also the proposed category name for NewCategory.
	public string ProposedName { get; set; } = string.Empty;
	public string ProposedRulesText { get; set; } = string.Empty;
	public string? ProposedCoverImageUrl { get; set; }
	public ScoringType ProposedScoringType { get; set; } = ScoringType.PointTally;
	public bool ProposedIsPaid { get; set; }
	public decimal ProposedPrice { get; set; }
	public string ProposedCategoryIdsText { get; set; } = string.Empty;

	[Ignore]
	public string SubmitterLabel => string.IsNullOrEmpty(SubmittedByName) ? SubmittedByEmail : SubmittedByName;

	[Ignore]
	public string TypeLabel => TypeLabelFor(Type);

	[Ignore]
	public string StatusLabel => Status switch
	{
		ReviewStatus.WaitingForReview => "Waiting for review",
		ReviewStatus.Approved => "Approved",
		_ => string.IsNullOrWhiteSpace(ReviewNote) ? "Rejected" : $"Rejected: {ReviewNote}"
	};

	[Ignore]
	public bool IsWaitingForReview => Status == ReviewStatus.WaitingForReview;

	/// <summary>New games and changes carry a full proposed game that can be edited and resubmitted.</summary>
	[Ignore]
	public bool HasGameProposal => Type is ReviewRequestType.NewGame or ReviewRequestType.GameChanges;

	/// <summary>Submitted by a moderator (the other kind is created automatically for admins).</summary>
	[Ignore]
	public bool IsFromModerator => Type != ReviewRequestType.AddGamesToCategory;

	[Ignore]
	public string Summary => $"{TypeLabel} · \"{SubjectName}\"";

	[Ignore]
	public string SubmittedLabel => IsFromModerator
		? $"Submitted by {SubmitterLabel} on {SubmittedAt.ToLocalTime().ToString("d MMM, HH:mm", CultureInfo.CurrentCulture)}"
		: $"Category added on {SubmittedAt.ToLocalTime().ToString("d MMM, HH:mm", CultureInfo.CurrentCulture)} - choose which games belong in it";

	public static string TypeLabelFor(ReviewRequestType type) => type switch
	{
		ReviewRequestType.NewGame => "New game",
		ReviewRequestType.GameChanges => "Changes to a game",
		ReviewRequestType.Deletion => "Deletion",
		ReviewRequestType.NewCategory => "New category",
		_ => "Add games to category"
	};

	/// <summary>Copies a game's content into the proposal.</summary>
	public void SetProposal(PubGame game)
	{
		ProposedName = game.Name;
		ProposedRulesText = game.RulesText;
		ProposedCoverImageUrl = game.CoverImageUrl;
		ProposedScoringType = game.ScoringType;
		ProposedIsPaid = game.IsPaid;
		ProposedPrice = game.Price;
		ProposedCategoryIdsText = game.CategoryIdsText;
		SubjectName = game.Name;
	}

	/// <summary>Puts the proposed content onto a game (what approving does).</summary>
	public void ApplyProposalTo(PubGame game)
	{
		game.Name = ProposedName;
		game.RulesText = ProposedRulesText;
		game.CoverImageUrl = ProposedCoverImageUrl;
		game.ScoringType = ProposedScoringType;
		game.IsPaid = ProposedIsPaid;
		game.Price = ProposedIsPaid ? ProposedPrice : 0m;
		game.CategoryIdsText = ProposedCategoryIdsText;
	}

	/// <summary>The proposal as a game, for previews: the current game with the proposal applied, or a new one.</summary>
	public PubGame ProposedGame(PubGame? current)
	{
		var game = new PubGame
		{
			Id = GameId,
			TeamId = TeamId,
			CreatedByUserId = current?.CreatedByUserId ?? SubmittedByUserId,
			CreatedAt = current?.CreatedAt ?? SubmittedAt,
			Status = current?.Status ?? GameStatus.Draft,
			PublishedAt = current?.PublishedAt
		};
		ApplyProposalTo(game);
		return game;
	}
}
