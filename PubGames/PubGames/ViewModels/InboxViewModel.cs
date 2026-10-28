using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>Someone a message can be sent to. Detail: their role and email, also searched.</summary>
public record MessageRecipient(string Email, string Label, string Detail = "");

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

	/// <summary>The messages, newest first.</summary>
	public ObservableCollection<InboxMessage> Messages { get; } = new();

	/// <summary>That a message is sent, or what went wrong.</summary>
	[ObservableProperty]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	/// <summary>
	/// Gets the services it works with - the Inbox, the team, the sign-in, the phone's database and cloud
	/// sync - from the app (dependency injection).
	/// </summary>
	public InboxViewModel(IMessageService messages, ITeamService team, IAuthService auth, ILocalDatabaseService local, ICloudSyncService cloud)
	{
		_messages = messages;
		_team = team;
		_auth = auth;
		_local = local;
		_cloud = cloud;
	}

	private string MyEmail => TeamMember.NormalizeEmail(_auth.CurrentUser?.Email ?? string.Empty);

	/// <summary>
	/// Shows the messages as saved on the phone, then refreshes them from the cloud and marks them read.
	/// </summary>
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
				? L.T("Offline - showing the messages saved on this phone.")
				: L.F("Couldn't refresh messages: {0}", ex.Message);
		}

		// Unread ones keep their "New" label for this visit; next time they're read.
		foreach (var message in Messages.Where(m => !m.IsRead).ToList())
			await _messages.MarkReadAsync(message);
	}

	/// <summary>Shows the messages, newest first.</summary>
	private void Show(IEnumerable<InboxMessage> messages)
	{
		Messages.Clear();
		foreach (var message in messages)
		{
			// Show "New" based on the state when loaded, even after marking it read.
			Messages.Add(new InboxMessage
			{
				Id = message.Id, ToEmail = message.ToEmail, FromEmail = message.FromEmail, FromName = message.FromName, Text = message.Text,
				SentAt = message.SentAt, IsRead = message.IsRead, Kind = message.Kind, ReviewRequestId = message.ReviewRequestId,
				OffersResubmit = message.OffersResubmit
			});
		}
	}

	/// <summary>
	/// Everyone on the team except yourself - the owner, admins and moderators -
	/// plus "All admins". Refreshed from the cloud when online.
	/// </summary>
	public async Task<List<MessageRecipient>> GetRecipientsAsync()
	{
		List<TeamMember> members;
		try
		{
			members = await _team.PullMembersAsync();
		}
		catch (Exception)
		{
			members = await _team.GetCachedMembersAsync();
		}

		var recipients = new List<MessageRecipient> { new(InboxMessage.AllAdmins, L.T("All admins"), L.T("Everyone with admin rights")) };
		recipients.AddRange(members
			.Where(m => m.Email != MyEmail)
			.Select(m => new MessageRecipient(m.Email,
				string.IsNullOrWhiteSpace(m.DisplayName) ? m.Email : m.DisplayName,
				$"{(m.IsBuiltInAdmin ? L.T("Owner") : m.RoleName)} · {m.Email}")));
		return recipients;
	}

	/// <summary>Returns true when sent (StatusMessage says why not otherwise).</summary>
	public async Task<bool> SendAsync(string toEmail, string text)
	{
		StatusMessage = L.T("Sending...");
		try
		{
			await _messages.SendAsync(toEmail, text);
			StatusMessage = L.T("Message sent.");
			return true;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
			return false;
		}
	}

	/// <summary>Deletes a message from the user's Inbox.</summary>
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
			StatusMessage = ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
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
			? L.T("That request can't be found any more.")
			: L.T("Only rejected new games and changes can be edited and submitted again.");
		return null;
	}
}
