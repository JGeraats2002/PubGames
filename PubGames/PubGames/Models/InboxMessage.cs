using System.Globalization;
using SQLite;

namespace PubGames.Models;

/// <summary>
/// What kind of message it is: written by someone, or sent by the app with a review decision.
/// </summary>
public enum MessageKind
{
	/// <summary>Written by an admin or moderator.</summary>
	Message,

	/// <summary>Sent automatically when an admin approves or rejects a moderator's review request.</summary>
	ReviewDecision,

	/// <summary>
	/// Sent by the cloud (functions/index.js, notifyTranslationChecks) to the team members who use a language:
	/// a game's translation into it is new or changed and waits to be checked.
	/// </summary>
	TranslationCheck,

	/// <summary>A tip about a draft, from someone who tried it, to its owner.</summary>
	Tip,

	/// <summary>Sent by the cloud (announceStaleDrafts): a draft nobody worked on for 3 days can be taken over.</summary>
	DraftTakeover,

	/// <summary>A report about an admin or moderator who broke the agreements, to the owners.</summary>
	Report
}

/// <summary>What a message is about, for the Inbox's filter; the first ones matter most.</summary>
public enum InboxCategory
{
	Report,
	Review,
	Draft,
	Translation,
	Tip,
	Message
}

/// <summary>What can be wrong when someone reports an admin or moderator: a stable key and its label.</summary>
public sealed record ReportCategory(string Key, string Label)
{
	/// <summary>Every kind of report, in the order they're offered.</summary>
	public static List<ReportCategory> All =>
	[
		new("Price", L.T("Wrong price")),
		new("Content", L.T("Offensive or unsuitable content")),
		new("Copied", L.T("Copied texts, pictures or brand names")),
		new("Drinking", L.T("Forced or excessive drinking")),
		new("Rights", L.T("Misuse of admin or moderator rights")),
		new("Review", L.T("Unfair review of requests")),
		new("Behaviour", L.T("Rude messages or harassment")),
		new("Other", L.T("Something else"))
	];

	/// <summary>The label of a saved key; the key itself when it isn't known.</summary>
	public static string LabelFor(string? key) => All.FirstOrDefault(c => c.Key == key)?.Label ?? key ?? string.Empty;
}

/// <summary>
/// A short message in someone's Inbox (Admin tab > Inbox), between admins and
/// moderators. The recipient keeps it or deletes it. Stored in the cloud
/// "messages" collection.
/// </summary>
public class InboxMessage
{
	/// <summary>Messages are meant to be short; firestore.rules enforces the same limit.</summary>
	public const int MaxLength = 500;

	/// <summary>
	/// Recipient for "all admins" - moderators can't see who the admins are. It's one
	/// shared message: when one admin deletes it, it's gone for every admin.
	/// </summary>
	public const string AllAdmins = "admins";

	/// <summary>The message's unique id.</summary>
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	/// <summary>Lower-case email of the recipient, or AllAdmins.</summary>
	[Indexed]
	public string ToEmail { get; set; } = string.Empty;

	/// <summary>The sender's email.</summary>
	public string FromEmail { get; set; } = string.Empty;

	/// <summary>The sender's name.</summary>
	public string FromName { get; set; } = string.Empty;

	/// <summary>What the message says.</summary>
	public string Text { get; set; } = string.Empty;

	/// <summary>When it was sent (UTC).</summary>
	public DateTime SentAt { get; set; } = DateTime.UtcNow;

	/// <summary>Whether the recipient has seen it.</summary>
	public bool IsRead { get; set; }

	/// <summary>A message written by someone, or a review decision.</summary>
	public MessageKind Kind { get; set; } = MessageKind.Message;

	/// <summary>For review decisions: the request it's about, so a rejected one can be edited and submitted again.</summary>
	public string? ReviewRequestId { get; set; }

	/// <summary>A rejected new game or change: the message offers "Edit and submit again".</summary>
	public bool OffersResubmit { get; set; }

	/// <summary>For a translation check: the game it's about.</summary>
	public string? GameId { get; set; }

	/// <summary>For a translation check: the game's name, as it was when the message was sent.</summary>
	public string? GameName { get; set; }

	/// <summary>For a translation check: the language to check (en, nl, ...).</summary>
	public string? Language { get; set; }

	/// <summary>A report: who it's about. A draft to take over: its owner.</summary>
	public string? AboutEmail { get; set; }

	/// <summary>The name of who it's about.</summary>
	public string? AboutName { get; set; }

	/// <summary>A report: what kind (a ReportCategory key).</summary>
	public string? ReportCategory { get; set; }

	/// <summary>A report: the page it was made on, and what was on it.</summary>
	public string? PageName { get; set; }

	/// <summary>A draft to take over: the message offers Take over.</summary>
	[Ignore]
	public bool IsDraftTakeover => Kind == MessageKind.DraftTakeover;

	/// <summary>What it's about, for the Inbox's filter.</summary>
	[Ignore]
	public InboxCategory Category => Kind switch
	{
		MessageKind.Report => InboxCategory.Report,
		MessageKind.ReviewDecision => InboxCategory.Review,
		MessageKind.DraftTakeover => InboxCategory.Draft,
		MessageKind.TranslationCheck => InboxCategory.Translation,
		MessageKind.Tip => InboxCategory.Tip,
		_ => InboxCategory.Message
	};

	/// <summary>Reports and review decisions go first and stand out.</summary>
	[Ignore]
	public bool IsHighPriority => Category is InboxCategory.Report or InboxCategory.Review;

	/// <summary>The category's name on the message's badge.</summary>
	[Ignore]
	public string CategoryLabel => LabelFor(Category);

	/// <summary>A category's name, for the badge and the filter.</summary>
	public static string LabelFor(InboxCategory category) => category switch
	{
		InboxCategory.Report => L.T("Report"),
		InboxCategory.Review => L.T("Review decision"),
		InboxCategory.Draft => L.T("Draft to take over"),
		InboxCategory.Translation => L.T("Translation"),
		InboxCategory.Tip => L.T("Tip"),
		_ => L.T("Message")
	};

	/// <summary>A translation to check: the message offers "Check translation" instead of Reply.</summary>
	[Ignore]
	public bool IsTranslationCheck => Kind == MessageKind.TranslationCheck;

	/// <summary>It can be answered (not for messages from the app itself).</summary>
	[Ignore]
	public bool CanReply => !IsTranslationCheck && !IsDraftTakeover;

	/// <summary>The text as shown: a translation check is written in the reader's language.</summary>
	[Ignore]
	public string ShownText => Kind switch
	{
		MessageKind.TranslationCheck => L.F("\"{0}\" is new or has changed. Can you check the {1} translation? Check translation opens it; correct what's needed, tap Translation is fine and save.",
			GameName ?? string.Empty, Services.AppLanguage.Find(Language)?.Name ?? Language ?? string.Empty),
		MessageKind.DraftTakeover => L.F("Nobody has worked on the draft \"{0}\" by {1} for 3 days. Take it over to finish it yourself; {1} gets a message.",
			GameName ?? string.Empty, AboutName ?? AboutEmail ?? string.Empty),
		MessageKind.Report => L.F("About: {0}\nWhat: {1}\nPage: {2}\n\n{3}", AboutName ?? AboutEmail ?? string.Empty,
			Models.ReportCategory.LabelFor(ReportCategory), PageName ?? string.Empty, Text),
		MessageKind.Tip => L.F("About the draft \"{0}\":\n{1}", GameName ?? string.Empty, Text),
		_ => Text
	};

	/// <summary>The sender's name, or their email without a name.</summary>
	[Ignore]
	public string FromLabel => string.IsNullOrEmpty(FromName) ? FromEmail : FromName;

	/// <summary>
	/// The message's heading: who it's from (and to all admins), or that it's a review decision.
	/// </summary>
	[Ignore]
	public string Heading => Kind == MessageKind.TranslationCheck
		? L.T("Translation to check")
		: Kind == MessageKind.DraftTakeover ? L.T("Draft to take over")
		: Kind == MessageKind.Report ? L.F("Report from {0}", FromLabel)
		: Kind == MessageKind.Tip ? L.F("Tip from {0}", FromLabel)
		: Kind == MessageKind.ReviewDecision
		? L.F("Review decision from {0}", FromLabel)
		: ToEmail == AllAdmins ? L.F("{0} to all admins", FromLabel) : L.F("From {0}", FromLabel);

	/// <summary>When it was sent, in the phone's time: "27 Oct, 14:05".</summary>
	[Ignore]
	public string SentLabel => SentAt.ToLocalTime().ToString("d MMM, HH:mm", CultureInfo.CurrentCulture);

	/// <summary>Not seen yet: shows a "New" badge.</summary>
	[Ignore]
	public bool IsUnread => !IsRead;
}
