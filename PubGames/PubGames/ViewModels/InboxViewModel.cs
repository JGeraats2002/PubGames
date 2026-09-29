using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>Someone a message can be sent to.</summary>
public record MessageRecipient(string Email, string Label);

/// <summary>
/// Admin tab > Inbox: short messages between admins and moderators, and the
/// review decisions moderators receive. Each message can be kept or deleted.
/// </summary>
public partial class InboxViewModel : ObservableObject
{
	private readonly IMessageService _messages;
	private readonly ITeamService _team;
	private readonly IAuthService _auth;
	private readonly ILocalDatabaseService _local;
	private readonly ICloudSyncService _cloud;

	public ObservableCollection<InboxMessage> Messages { get; } = new();

	[ObservableProperty]
	private string statusMessage = string.Empty;

	public InboxViewModel(IMessageService messages, ITeamService team, IAuthService auth, ILocalDatabaseService local, ICloudSyncService cloud)
	{
		_messages = messages;
		_team = team;
		_auth = auth;
		_local = local;
		_cloud = cloud;
	}

	private string MyEmail => TeamMember.NormalizeEmail(_auth.CurrentUser?.Email ?? string.Empty);

	public async Task LoadAsync()
	{
		Show(await _messages.GetCachedInboxAsync());
		try
		{
			Show(await _messages.PullInboxAsync());
			StatusMessage = string.Empty;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException
				? "Offline - showing the messages saved on this phone."
				: $"Couldn't refresh messages: {ex.Message}";
		}

		// Unread ones keep their "New" label for this visit; next time they're read.
		foreach (var message in Messages.Where(m => !m.IsRead).ToList())
			await _messages.MarkReadAsync(message);
	}

	private void Show(IEnumerable<InboxMessage> messages)
	{
		Messages.Clear();
		foreach (var m in messages)
		{
			// Show "New" based on the state when loaded, even after marking it read.
			Messages.Add(new InboxMessage
			{
				Id = m.Id, ToEmail = m.ToEmail, FromEmail = m.FromEmail, FromName = m.FromName, Text = m.Text,
				SentAt = m.SentAt, IsRead = m.IsRead, Kind = m.Kind, ReviewRequestId = m.ReviewRequestId,
				OffersResubmit = m.OffersResubmit
			});
		}
	}

	/// <summary>Admins: every team member except themselves, plus all admins. Moderators: all admins.</summary>
	public async Task<List<MessageRecipient>> GetRecipientsAsync()
	{
		var recipients = new List<MessageRecipient> { new(InboxMessage.AllAdmins, "All admins") };
		if (_team.Me?.IsAdmin == true)
			recipients.AddRange((await _team.GetCachedMembersAsync())
				.Where(m => m.Email != MyEmail)
				.Select(m => new MessageRecipient(m.Email, $"{m.Label} ({m.RoleName.ToLowerInvariant()})")));
		return recipients;
	}

	/// <summary>Returns true when sent (StatusMessage says why not otherwise).</summary>
	public async Task<bool> SendAsync(string toEmail, string text)
	{
		StatusMessage = "Sending...";
		try
		{
			await _messages.SendAsync(toEmail, text);
			StatusMessage = "Message sent.";
			return true;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message;
			return false;
		}
	}

	public async Task DeleteAsync(InboxMessage message)
	{
		try
		{
			await _messages.DeleteAsync(message);
			Messages.Remove(message);
			StatusMessage = string.Empty;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message;
		}
	}

	/// <summary>
	/// For a rejected request's decision: the request to open in the editor, or
	/// null with StatusMessage saying why it can't be edited any more.
	/// </summary>
	public async Task<ReviewRequest?> FindRejectedRequestAsync(InboxMessage message)
	{
		if (message.ReviewRequestId is not { } id) return null;

		var request = await _local.GetReviewRequestAsync(id);
		if (request is null)
		{
			try
			{
				await _cloud.PullRequestsSubmittedByAsync(MyEmail);
				request = await _local.GetReviewRequestAsync(id);
			}
			catch (Exception)
			{
				// Offline and not on this phone.
			}
		}

		if (request is { Status: ReviewStatus.Rejected, HasGameProposal: true })
			return request;

		StatusMessage = request is null
			? "That request can't be found any more."
			: "Only rejected new games and changes can be edited and submitted again.";
		return null;
	}
}
