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
	TranslationCheck
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

	/// <summary>A translation to check: the message offers "Check translation" instead of Reply.</summary>
	[Ignore]
	public bool IsTranslationCheck => Kind == MessageKind.TranslationCheck;

	/// <summary>It can be answered (not for messages from the app itself).</summary>
	[Ignore]
	public bool CanReply => !IsTranslationCheck;

	/// <summary>The text as shown: a translation check is written in the reader's language.</summary>
	[Ignore]
	public string ShownText => IsTranslationCheck
		? L.F("\"{0}\" is new or has changed. Can you check the {1} translation? Check translation opens it; correct what's needed, tap Translation is fine and save.",
			GameName ?? string.Empty, Services.AppLanguage.Find(Language)?.Name ?? Language ?? string.Empty)
		: Text;

	/// <summary>The sender's name, or their email without a name.</summary>
	[Ignore]
	public string FromLabel => string.IsNullOrEmpty(FromName) ? FromEmail : FromName;

	/// <summary>
	/// The message's heading: who it's from (and to all admins), or that it's a review decision.
	/// </summary>
	[Ignore]
	public string Heading => Kind == MessageKind.TranslationCheck
		? L.T("Translation to check")
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
