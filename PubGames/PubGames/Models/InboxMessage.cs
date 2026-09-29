using System.Globalization;
using SQLite;

namespace PubGames.Models;

public enum MessageKind
{
	/// <summary>Written by an admin or moderator.</summary>
	Message,

	/// <summary>Sent automatically when an admin approves or rejects a moderator's review request.</summary>
	ReviewDecision
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

	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	/// <summary>Lower-case email of the recipient, or AllAdmins.</summary>
	[Indexed]
	public string ToEmail { get; set; } = string.Empty;

	public string FromEmail { get; set; } = string.Empty;

	public string FromName { get; set; } = string.Empty;

	public string Text { get; set; } = string.Empty;

	public DateTime SentAt { get; set; } = DateTime.UtcNow;

	public bool IsRead { get; set; }

	public MessageKind Kind { get; set; } = MessageKind.Message;

	/// <summary>For review decisions: the request it's about, so a rejected one can be edited and submitted again.</summary>
	public string? ReviewRequestId { get; set; }

	/// <summary>A rejected new game or change: the message offers "Edit and submit again".</summary>
	public bool OffersResubmit { get; set; }

	[Ignore]
	public string FromLabel => string.IsNullOrEmpty(FromName) ? FromEmail : FromName;

	[Ignore]
	public string Heading => Kind == MessageKind.ReviewDecision
		? $"Review decision from {FromLabel}"
		: ToEmail == AllAdmins ? $"{FromLabel} to all admins" : $"From {FromLabel}";

	[Ignore]
	public string SentLabel => SentAt.ToLocalTime().ToString("d MMM, HH:mm", CultureInfo.CurrentCulture);

	[Ignore]
	public bool IsUnread => !IsRead;
}
