namespace PubGames.Services;

/// <summary>
/// Sends an email from the app. Two implementations, swapped in MauiProgram:
///   ComposeEmailSender     - opens the phone's mail app prefilled; the admin taps Send (no setup)
///   FirestoreEmailSender   - fully automatic via Firebase's Trigger Email extension (Blaze plan)
/// </summary>
public interface IEmailSender
{
	/// <summary>True when the email goes out by itself; false when the admin still has to tap Send.</summary>
	bool SendsAutomatically { get; }

	/// <param name="replyTo">Where replies go - the admin, so questions reach a person.</param>
	Task SendAsync(string to, string subject, string body, string replyTo);
}

/// <summary>
/// Opens the phone's mail app (e.g. Gmail) with everything filled in, sent
/// from the admin's own address. Throws if the phone has no mail app.
/// </summary>
public class ComposeEmailSender : IEmailSender
{
	public bool SendsAutomatically => false;

	public async Task SendAsync(string to, string subject, string body, string replyTo)
	{
		if (!Email.Default.IsComposeSupported)
			throw new InvalidOperationException("No email app found on this phone - install Gmail to send the email.");

		await Email.Default.ComposeAsync(new EmailMessage
		{
			To = [to],
			Subject = subject,
			Body = body,
			BodyFormat = EmailBodyFormat.PlainText
		});
	}
}

/// <summary>
/// Queues the email as a document in the "mail" collection; Firebase's Trigger
/// Email extension sends it and records the delivery status on the document.
/// To switch: move to the Blaze plan, install the extension (Firebase console →
/// Extensions → Trigger Email from Firestore, collection "mail", your SMTP
/// details), then register this class instead of ComposeEmailSender in MauiProgram.
/// firestore.rules already lets admins write to "mail".
/// </summary>
public class FirestoreEmailSender : IEmailSender
{
	private readonly FirestoreClient _firestore;

	public FirestoreEmailSender(FirestoreClient firestore)
	{
		_firestore = firestore;
	}

	public bool SendsAutomatically => true;

	public Task SendAsync(string to, string subject, string body, string replyTo) =>
		_firestore.SetAsync("mail", Guid.NewGuid().ToString("N"), new Dictionary<string, object?>
		{
			["to"] = to,
			["replyTo"] = replyTo,
			["message"] = new Dictionary<string, object?>
			{
				["subject"] = subject,
				["text"] = body
			},
			["createdAt"] = DateTime.UtcNow
		});
}
