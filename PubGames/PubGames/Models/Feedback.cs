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
	/// <summary>The subject's name, as the picker shows it.</summary>
	public string Label => Feedback.CategoryLabel(Category);

	/// <summary>The subject's name, as the picker shows it.</summary>
	public override string ToString() => Label;

	/// <summary>Every subject, in the order the picker shows them.</summary>
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

	/// <summary>The feedback's unique id.</summary>
	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	/// <summary>The account of the player who sent it.</summary>
	public string FromUid { get; set; } = string.Empty;

	/// <summary>The sender's email, for the admins to see who it is.</summary>
	public string FromEmail { get; set; } = string.Empty;

	/// <summary>The sender's name.</summary>
	public string FromName { get; set; } = string.Empty;

	/// <summary>What it's about: a bug, a typo, a mistake in the rules ...</summary>
	public FeedbackCategory Category { get; set; }

	/// <summary>The game it's about; null when it's not about a game.</summary>
	public string? GameId { get; set; }

	/// <summary>The name of the game it's about, as it was when sent.</summary>
	public string? GameName { get; set; }

	/// <summary>The sender's explanation.</summary>
	public string Text { get; set; } = string.Empty;

	/// <summary>When it was sent (UTC).</summary>
	public DateTime SentAt { get; set; } = DateTime.UtcNow;

	/// <summary>Where the admins are with it.</summary>
	public FeedbackStatus Status { get; set; } = FeedbackStatus.New;

	/// <summary>The admin's answer, shown to the sender.</summary>
	public string? Reply { get; set; }

	/// <summary>The admin who last changed the status or reply.</summary>
	public string? HandledBy { get; set; }

	/// <summary>When an admin last changed its status or reply (UTC).</summary>
	public DateTime? UpdatedAt { get; set; }

	/// <summary>What a subject is called in the app.</summary>
	public static string CategoryLabel(FeedbackCategory category) => category switch
	{
		FeedbackCategory.Bug => L.T("Bug - something doesn't work"),
		FeedbackCategory.Typo => L.T("Typo"),
		FeedbackCategory.RulesMistake => L.T("Mistake in a game's rules"),
		FeedbackCategory.ScoringProblem => L.T("Scoring doesn't add up"),
		FeedbackCategory.Idea => L.T("Idea or wish"),
		_ => L.T("Other")
	};

	/// <summary>What a status is called in the app.</summary>
	public static string StatusLabel(FeedbackStatus status) => status switch
	{
		FeedbackStatus.InProgress => L.T("Being worked on"),
		FeedbackStatus.Resolved => L.T("Resolved"),
		FeedbackStatus.Declined => L.T("Won't be changed"),
		_ => L.T("New")
	};

	/// <summary>The subject, as the app shows it.</summary>
	public string CategoryText => CategoryLabel(Category);

	/// <summary>The status, as the app shows it.</summary>
	public string StatusText => StatusLabel(Status);

	/// <summary>Still to be handled: new, or being worked on.</summary>
	public bool IsOpen => Status is FeedbackStatus.New or FeedbackStatus.InProgress;

	/// <summary>Whether it's about a game.</summary>
	public bool HasGame => !string.IsNullOrEmpty(GameId);

	/// <summary>"Game: Kings cup" or "Not about a game".</summary>
	public string GameText => HasGame ? L.F("Game: {0}", GameName) : L.T("Not about a game");

	/// <summary>Whether an admin wrote a reply.</summary>
	public bool HasReply => !string.IsNullOrWhiteSpace(Reply);

	/// <summary>"Jan · jan@example.com": who sent it.</summary>
	public string FromLabel => string.IsNullOrWhiteSpace(FromName) ? FromEmail : $"{FromName} · {FromEmail}";

	/// <summary>When it was sent, in the phone's time: "27 Oct 2026, 14:05".</summary>
	public string SentLabel => SentAt.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.CurrentCulture);
}
