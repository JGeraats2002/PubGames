using PubGames.Models;

namespace PubGames.Services;

/// <summary>
/// Short messages between admins and moderators (Admin tab > Inbox), stored in
/// the cloud "messages" collection and cached on the phone. Review decisions
/// arrive here too.
/// </summary>
public interface IMessageService
{
	/// <summary>This phone's copy of the signed-in user's Inbox, newest first.</summary>
	Task<List<InboxMessage>> GetCachedInboxAsync();

	/// <summary>Refreshes the Inbox from the cloud. Throws when offline.</summary>
	Task<List<InboxMessage>> PullInboxAsync();

	/// <param name="toEmail">A team member's email, or InboxMessage.AllAdmins.</param>
	/// <param name="offersResubmit">For a rejected new game or change: show "Edit and submit again".</param>
	Task SendAsync(string toEmail, string text, MessageKind kind = MessageKind.Message, string? reviewRequestId = null, bool offersResubmit = false);

	Task MarkReadAsync(InboxMessage message);

	Task DeleteAsync(InboxMessage message);
}

public class MessageService : IMessageService
{
	private const string Collection = "messages";

	private readonly ILocalDatabaseService _local;
	private readonly FirestoreClient _firestore;
	private readonly IAuthService _auth;
	private readonly ITeamService _team;

	public MessageService(ILocalDatabaseService local, FirestoreClient firestore, IAuthService auth, ITeamService team)
	{
		_local = local;
		_firestore = firestore;
		_auth = auth;
		_team = team;
	}

	private string MyEmail => TeamMember.NormalizeEmail(_auth.CurrentUser?.Email ?? string.Empty);

	/// <summary>Everyone gets their own messages; admins also share the "all admins" ones.</summary>
	private List<string> MyRecipientAddresses() =>
		_team.Me?.IsAdmin == true ? [MyEmail, InboxMessage.AllAdmins] : [MyEmail];

	public Task<List<InboxMessage>> GetCachedInboxAsync() => _local.GetMessagesToAsync(MyRecipientAddresses());

	public async Task<List<InboxMessage>> PullInboxAsync()
	{
		var recipients = MyRecipientAddresses();
		var messages = new List<InboxMessage>();
		foreach (var to in recipients)
			messages.AddRange((await _firestore.WhereEqualAsync(Collection, "toEmail", to)).Select(ToMessage).OfType<InboxMessage>());

		// Deleted elsewhere (by another admin, for shared admin messages).
		var ids = messages.Select(m => m.Id).ToHashSet();
		foreach (var stale in (await _local.GetMessagesToAsync(recipients)).Where(m => !ids.Contains(m.Id)))
			await _local.DeleteMessageAsync(stale.Id);
		foreach (var message in messages)
			await _local.SaveMessageAsync(message);

		return messages.OrderByDescending(m => m.SentAt).ToList();
	}

	public async Task SendAsync(string toEmail, string text, MessageKind kind = MessageKind.Message, string? reviewRequestId = null, bool offersResubmit = false)
	{
		text = text.Trim();
		if (text.Length == 0)
			throw new InvalidOperationException("Write a message first.");
		if (text.Length > InboxMessage.MaxLength)
			throw new InvalidOperationException($"Messages can be {InboxMessage.MaxLength} characters at most.");

		var user = _auth.CurrentUser!;
		var message = new InboxMessage
		{
			ToEmail = toEmail == InboxMessage.AllAdmins ? toEmail : TeamMember.NormalizeEmail(toEmail),
			FromEmail = MyEmail,
			FromName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName,
			Text = text,
			Kind = kind,
			ReviewRequestId = reviewRequestId,
			OffersResubmit = offersResubmit
		};
		await SaveAsync(message);
	}

	public async Task MarkReadAsync(InboxMessage message)
	{
		if (message.IsRead) return;
		message.IsRead = true;
		try
		{
			await SaveAsync(message);
		}
		catch (Exception)
		{
			// Offline: it's read on this phone; the cloud catches up next time it's opened.
			await _local.SaveMessageAsync(message);
		}
	}

	public async Task DeleteAsync(InboxMessage message)
	{
		await _firestore.DeleteAsync(Collection, message.Id);
		await _local.DeleteMessageAsync(message.Id);
	}

	private async Task SaveAsync(InboxMessage m)
	{
		await _firestore.SetAsync(Collection, m.Id, new Dictionary<string, object?>
		{
			["id"] = m.Id,
			["toEmail"] = m.ToEmail,
			["fromEmail"] = m.FromEmail,
			["fromName"] = m.FromName,
			["text"] = m.Text,
			["sentAt"] = m.SentAt,
			["isRead"] = m.IsRead,
			["kind"] = m.Kind.ToString(),
			["reviewRequestId"] = m.ReviewRequestId,
			["offersResubmit"] = m.OffersResubmit
		});
		await _local.SaveMessageAsync(m);
	}

	private static InboxMessage? ToMessage(Dictionary<string, object?> f)
	{
		if (f.GetValueOrDefault("id") is not string id) return null;

		return new InboxMessage
		{
			Id = id,
			ToEmail = f.GetValueOrDefault("toEmail") as string ?? string.Empty,
			FromEmail = f.GetValueOrDefault("fromEmail") as string ?? string.Empty,
			FromName = f.GetValueOrDefault("fromName") as string ?? string.Empty,
			Text = f.GetValueOrDefault("text") as string ?? string.Empty,
			SentAt = f.GetValueOrDefault("sentAt") as DateTime? ?? DateTime.UtcNow,
			IsRead = f.GetValueOrDefault("isRead") as bool? ?? false,
			Kind = Enum.TryParse<MessageKind>(f.GetValueOrDefault("kind") as string, out var k) ? k : MessageKind.Message,
			ReviewRequestId = f.GetValueOrDefault("reviewRequestId") as string,
			OffersResubmit = f.GetValueOrDefault("offersResubmit") as bool? ?? false
		};
	}
}
