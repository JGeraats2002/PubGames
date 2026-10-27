using System.Globalization;

namespace PubGames.Models;

/// <summary>What a piece of feedback is about, chosen first when sending it.</summary>
public enum FeedbackCategory
{
	Bug,
	Typo,
	RulesMistake,
	ScoringProblem,
	Idea,
	Other
}

/// <summary>Where an admin is with a piece of feedback.</summary>
public enum FeedbackStatus
{
	New,
	InProgress,
	Resolved,
	Declined
}

/// <summary>A choice in the feedback category picker.</summary>
public sealed record FeedbackCategoryOption(FeedbackCategory Category)
{
	public string Label => Feedback.CategoryLabel(Category);

	public override string ToString() => Label;

	public static List<FeedbackCategoryOption> All { get; } =
		Enum.GetValues<FeedbackCategory>().Select(c => new FeedbackCategoryOption(c)).ToList();
}

/// <summary>
/// Feedback from a player to the admins (feedback/{id} in the cloud): what
/// it's about, optionally which game, and the explanation. Admins give it a
/// status and can add a reply, which the sender sees under Settings > Feedback.
/// </summary>
public sealed class Feedback
{
	/// <summary>The explanation is required: at least this many characters.</summary>
	public const int MinLength = 10;

	/// <summary>firestore.rules enforces the same limit.</summary>
	public const int MaxLength = 1000;

	public const int MaxReplyLength = 500;

	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	public string FromUid { get; set; } = string.Empty;

	public string FromEmail { get; set; } = string.Empty;

	public string FromName { get; set; } = string.Empty;

	public FeedbackCategory Category { get; set; }

	/// <summary>The game it's about; null when it's not about a game.</summary>
	public string? GameId { get; set; }

	public string? GameName { get; set; }

	public string Text { get; set; } = string.Empty;

	public DateTime SentAt { get; set; } = DateTime.UtcNow;

	public FeedbackStatus Status { get; set; } = FeedbackStatus.New;

	/// <summary>The admin's answer, shown to the sender.</summary>
	public string? Reply { get; set; }

	/// <summary>The admin who last changed the status or reply.</summary>
	public string? HandledBy { get; set; }

	public DateTime? UpdatedAt { get; set; }

	public static string CategoryLabel(FeedbackCategory category) => category switch
	{
		FeedbackCategory.Bug => "Bug - something doesn't work",
		FeedbackCategory.Typo => "Typo",
		FeedbackCategory.RulesMistake => "Mistake in a game's rules",
		FeedbackCategory.ScoringProblem => "Scoring doesn't add up",
		FeedbackCategory.Idea => "Idea or wish",
		_ => "Other"
	};

	public static string StatusLabel(FeedbackStatus status) => status switch
	{
		FeedbackStatus.InProgress => "Being worked on",
		FeedbackStatus.Resolved => "Resolved",
		FeedbackStatus.Declined => "Won't be changed",
		_ => "New"
	};

	public string CategoryText => CategoryLabel(Category);

	public string StatusText => StatusLabel(Status);

	public bool IsOpen => Status is FeedbackStatus.New or FeedbackStatus.InProgress;

	public bool HasGame => !string.IsNullOrEmpty(GameId);

	public string GameText => HasGame ? $"Game: {GameName}" : "Not about a game";

	public bool HasReply => !string.IsNullOrWhiteSpace(Reply);

	public string FromLabel => string.IsNullOrWhiteSpace(FromName) ? FromEmail : $"{FromName} · {FromEmail}";

	public string SentLabel => SentAt.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.CurrentCulture);
}
