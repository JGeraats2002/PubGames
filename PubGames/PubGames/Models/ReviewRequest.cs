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
	AddGamesToCategory,

	/// <summary>Add a main category (from a moderator).</summary>
	NewCategoryGroup,

	// Changes to what's there, from a moderator. CategoryId is the category or main category it's about.

	/// <summary>Give a category another name (ProposedName).</summary>
	RenameCategory,

	/// <summary>File a category under another main category (ProposedGroupId).</summary>
	MoveCategory,

	/// <summary>Delete a category, which also takes it off every game.</summary>
	DeleteCategory,

	/// <summary>Give a main category another name (ProposedName).</summary>
	RenameCategoryGroup,

	/// <summary>Delete a main category together with its categories.</summary>
	DeleteCategoryGroup
}

/// <summary>Where a review request is: waiting, approved or rejected.</summary>
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
/// (main) category are all submitted as a review request, and players keep seeing
/// the current version until an admin approves it. For new games and changes,
/// the full proposed version travels with the request (the Proposed* fields),
/// so the admin sees exactly what would go live. The decision reaches the
/// moderator as a message in their Inbox. Stored in the cloud "reviewRequests"
/// collection.
/// </summary>
public class ReviewRequest
{
	/// <summary>The request's unique id.</summary>
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	/// <summary>The game this is about; for a new game, the id it gets once approved. Empty for category requests.</summary>
	[Indexed]
	public string GameId { get; set; } = string.Empty;

	/// <summary>
	/// The category this is about: for a new category, the id it gets once
	/// approved; for adding games to a category, that category. For a new
	/// main category, the id that one gets once approved.
	/// </summary>
	public string CategoryId { get; set; } = string.Empty;

	/// <summary>The team it belongs to.</summary>
	[Indexed]
	public string TeamId { get; set; } = string.Empty;

	/// <summary>What it asks: a new game, changes, deletion, a category ...</summary>
	public ReviewRequestType Type { get; set; }

	/// <summary>What the request is about as shown on cards: the game's or category's name (the proposed one for new items and changes).</summary>
	public string SubjectName { get; set; } = string.Empty;

	/// <summary>The account of who sent it.</summary>
	public string SubmittedByUserId { get; set; } = string.Empty;

	/// <summary>Checked by firestore.rules: requests can only be submitted in your own name.</summary>
	[Indexed]
	public string SubmittedByEmail { get; set; } = string.Empty;

	/// <summary>The name of who sent it.</summary>
	public string SubmittedByName { get; set; } = string.Empty;

	/// <summary>When it was sent (UTC).</summary>
	public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

	/// <summary>Waiting, approved or rejected.</summary>
	public ReviewStatus Status { get; set; } = ReviewStatus.WaitingForReview;

	/// <summary>The admin who decided, or null while it's waiting.</summary>
	public string? ReviewedByUserId { get; set; }

	/// <summary>When an admin decided (UTC), or null while it's waiting.</summary>
	public DateTime? ReviewedAt { get; set; }

	/// <summary>Why an admin rejected it; also sent to the moderator's Inbox.</summary>
	public string ReviewNote { get; set; } = string.Empty;

	// The proposed version of the game (new games and changes; empty otherwise).
	// ProposedName is also the proposed name for NewCategory and NewCategoryGroup.
	public string ProposedName { get; set; } = string.Empty;

	/// <summary>NewCategory: the main category it should be filed under.</summary>
	public string ProposedGroupId { get; set; } = string.Empty;
	/// <summary>The proposed rules, with their pictures.</summary>
	public string ProposedRulesText { get; set; } = string.Empty;
	/// <summary>The proposed cover picture, or null.</summary>
	public string? ProposedCoverImageUrl { get; set; }
	/// <summary>The proposed scoring type.</summary>
	public ScoringType ProposedScoringType { get; set; } = ScoringType.PointTally;
	/// <summary>The proposed scoring settings, as JSON.</summary>
	public string? ProposedScoringSettingsText { get; set; }
	/// <summary>Whether the game would be paid.</summary>
	public bool ProposedIsPaid { get; set; }
	/// <summary>The proposed price.</summary>
	public decimal ProposedPrice { get; set; }
	/// <summary>The proposed categories' ids, comma-separated.</summary>
	public string ProposedCategoryIdsText { get; set; } = string.Empty;
	/// <summary>The proposed minimum number of players, or null for none.</summary>
	public int? ProposedMinPlayers { get; set; }
	/// <summary>The proposed maximum number of players, or null for none.</summary>
	public int? ProposedMaxPlayers { get; set; }

	/// <summary>Who sent it: their name, or their email without a name.</summary>
	[Ignore]
	public string SubmitterLabel => string.IsNullOrEmpty(SubmittedByName) ? SubmittedByEmail : SubmittedByName;

	/// <summary>This request's kind, as the app calls it.</summary>
	[Ignore]
	public string TypeLabel => TypeLabelFor(Type);

	/// <summary>The status as the app shows it.</summary>
	[Ignore]
	public string StatusLabel => Status switch
	{
		ReviewStatus.WaitingForReview => "Waiting for review",
		ReviewStatus.Approved => "Approved",
		_ => string.IsNullOrWhiteSpace(ReviewNote) ? "Rejected" : $"Rejected: {ReviewNote}"
	};

	/// <summary>Whether an admin still has to decide.</summary>
	[Ignore]
	public bool IsWaitingForReview => Status == ReviewStatus.WaitingForReview;

	/// <summary>New games and changes carry a full proposed game that can be edited and resubmitted.</summary>
	[Ignore]
	public bool HasGameProposal => Type is ReviewRequestType.NewGame or ReviewRequestType.GameChanges;

	/// <summary>Submitted by a moderator (the other kind is created automatically for admins).</summary>
	[Ignore]
	public bool IsFromModerator => Type != ReviewRequestType.AddGamesToCategory;

	/// <summary>One line: what it asks and about what - New game · "Kings cup".</summary>
	[Ignore]
	public string Summary => $"{TypeLabel} · \"{SubjectName}\"";

	/// <summary>Who sent it and when, as the list shows it.</summary>
	[Ignore]
	public string SubmittedLabel => IsFromModerator
		? $"Submitted by {SubmitterLabel} on {SubmittedAt.ToLocalTime().ToString("d MMM, HH:mm", CultureInfo.CurrentCulture)}"
		: $"Category added on {SubmittedAt.ToLocalTime().ToString("d MMM, HH:mm", CultureInfo.CurrentCulture)} - choose which games belong in it";

	/// <summary>What a kind of request is called in the app.</summary>
	public static string TypeLabelFor(ReviewRequestType type) => type switch
	{
		ReviewRequestType.NewGame => "New game",
		ReviewRequestType.GameChanges => "Changes to a game",
		ReviewRequestType.Deletion => "Deletion",
		ReviewRequestType.NewCategory => "New category",
		ReviewRequestType.NewCategoryGroup => "New main category",
		ReviewRequestType.RenameCategory => "Rename category",
		ReviewRequestType.MoveCategory => "Move category",
		ReviewRequestType.DeleteCategory => "Delete category",
		ReviewRequestType.RenameCategoryGroup => "Rename main category",
		ReviewRequestType.DeleteCategoryGroup => "Delete main category",
		_ => "Add games to category"
	};

	/// <summary>
	/// The games someone else has changes or a deletion waiting for. Until an
	/// admin decides, other moderators can't change those games: two sets of
	/// changes to one game would clash.
	/// </summary>
	/// <param name="waiting">The requests that are waiting for review.</param>
	/// <param name="myEmail">Whose own requests don't count.</param>
	/// <returns>Per game, the request that's waiting for it.</returns>
	public static Dictionary<string, ReviewRequest> GamesTakenByOthers(IEnumerable<ReviewRequest> waiting, string myEmail) =>
		waiting.Where(r => r.IsWaitingForReview
				&& r.Type is ReviewRequestType.GameChanges or ReviewRequestType.Deletion
				&& r.SubmittedByEmail != myEmail)
			.GroupBy(r => r.GameId)
			.ToDictionary(g => g.Key, g => g.First());

	/// <summary>Copies a game's content into the proposal.</summary>
	public void SetProposal(PubGame game)
	{
		ProposedName = game.Name;
		ProposedRulesText = game.RulesText;
		ProposedCoverImageUrl = game.CoverImageUrl;
		ProposedScoringType = game.ScoringType;
		ProposedScoringSettingsText = game.ScoringSettingsText;
		ProposedIsPaid = game.IsPaid;
		ProposedPrice = game.Price;
		ProposedCategoryIdsText = game.CategoryIdsText;
		ProposedMinPlayers = game.MinPlayers;
		ProposedMaxPlayers = game.MaxPlayers;
		SubjectName = game.Name;
	}

	/// <summary>Puts the proposed content onto a game (what approving does).</summary>
	public void ApplyProposalTo(PubGame game)
	{
		game.Name = ProposedName;
		game.RulesText = ProposedRulesText;
		game.CoverImageUrl = ProposedCoverImageUrl;
		game.ScoringType = ProposedScoringType;
		game.ScoringSettingsText = ProposedScoringSettingsText;
		game.IsPaid = ProposedIsPaid;
		game.Price = ProposedIsPaid ? ProposedPrice : 0m;
		game.CategoryIdsText = ProposedCategoryIdsText;
		game.MinPlayers = ProposedMinPlayers;
		game.MaxPlayers = ProposedMaxPlayers;
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
