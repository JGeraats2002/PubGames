using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// Someone a message can be sent to. Detail: their role and email. IsAdmin: an admin or the owner. Language:
/// the language they use the app in, when known (its flag shows, and it can be searched for).
/// </summary>
public record MessageRecipient(string Email, string Label, string Detail = "", bool IsAdmin = false, AppLanguageInfo? Language = null)
{
	/// <summary>The flag of their language; null when not known.</summary>
	public string? Flag => Language?.Flag;

	/// <summary>Their language is known, so its flag shows.</summary>
	public bool HasLanguage => Language is not null;

	/// <summary>Their language by name; empty when not known.</summary>
	public string LanguageName => Language?.Name ?? string.Empty;
}

/// <summary>One filter above the Inbox: all messages, or one category, with how many there are.</summary>
public partial class InboxFilter : ObservableObject
{
	/// <summary>The category; null is everything.</summary>
	public InboxCategory? Category { get; init; }

	/// <summary>"Reports (2)".</summary>
	[ObservableProperty]
	private string text = string.Empty;

	/// <summary>The filter that's on.</summary>
	[ObservableProperty]
	private bool isSelected;
}

/// <summary>
/// Admin tab > Inbox: short messages between admins and moderators, the
/// review decisions moderators receive, tips, reports and drafts to take
/// over. Filtered by category; the categories that matter most come first.
/// Each message can be kept or deleted.
/// </summary>
public partial class InboxViewModel : ObservableObject
{
	/// <summary>Every message loaded, before filtering.</summary>
	private List<InboxMessage> _all = [];

	/// <summary>The filters: all, then each category in order of priority.</summary>
	public ObservableCollection<InboxFilter> Filters { get; } = new(
		new InboxFilter[] { new() { IsSelected = true } }
			.Concat(Enum.GetValues<InboxCategory>().Select(c => new InboxFilter { Category = c })));

	/// <summary>Shows only the messages of a filter's category (or all).</summary>
	public void Choose(InboxFilter filter)
	{
		foreach (var other in Filters)
			other.IsSelected = other == filter;
		ShowFiltered();
	}

	/// <summary>
	/// The messages of the chosen filter: the most important categories
	/// first (reports, review decisions), newest first within each.
	/// </summary>
	private void ShowFiltered()
	{
		var category = Filters.FirstOrDefault(f => f.IsSelected)?.Category;
		Messages.Clear();
		foreach (var message in _all.Where(m => category is null || m.Category == category)
			.OrderBy(m => m.Category).ThenByDescending(m => m.SentAt))
			Messages.Add(message);
		foreach (var filter in Filters)
		{
			var count = filter.Category is { } c ? _all.Count(m => m.Category == c) : _all.Count;
			var unread = filter.Category is { } d ? _all.Count(m => m.Category == d && m.IsUnread) : _all.Count(m => m.IsUnread);
			var name = filter.Category is { } e ? InboxMessage.LabelFor(e) : L.T("All");
			filter.Text = unread > 0 ? L.F("{0} ({1}, {2} new)", name, count, unread) : L.F("{0} ({1})", name, count);
		}
	}

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
	public InboxViewModel(IMessageService messages, ITeamService team, IAuthService auth, ILocalDatabaseService local, ICloudSyncService cloud, DraftService drafts)
	{
		_drafts = drafts;
		_messages = messages;
		_team = team;
		_auth = auth;
		_local = local;
		_cloud = cloud;
	}

	private string MyEmail => TeamMember.NormalizeEmail(_auth.CurrentUser?.Email ?? string.Empty);

	/// <summary>Drafts, to take one over from its message.</summary>
	private readonly DraftService _drafts;

	/// <summary>
	/// Take over from a "draft to take over" message: the draft's id to open
	/// when it's yours now, or null with StatusMessage saying why not.
	/// </summary>
	public async Task<string?> TakeOverAsync(InboxMessage message)
	{
		if (message.GameId is not { } id) return null;
		GameDraft? draft;
		try
		{
			draft = await _drafts.GetAsync(id);
		}
		catch (Exception)
		{
			StatusMessage = L.T("No connection - try again when you're online.");
			return null;
		}
		if (draft is null)
		{
			StatusMessage = L.T("This draft isn't there any more: it was finished or removed.");
			return null;
		}
		if (draft.OwnerEmail == MyEmail) return draft.Id;
		if (!draft.IsStale)
		{
			StatusMessage = L.F("{0} is working on it again, or took it over first.", string.IsNullOrEmpty(draft.OwnerName) ? draft.OwnerEmail : draft.OwnerName);
			return null;
		}
		var previous = draft.OwnerEmail;
		try
		{
			await _drafts.TakeOverAsync(draft);
		}
		catch (Exception)
		{
			StatusMessage = L.T("This draft can't be taken over (any more): someone worked on it in the last 3 days, or took it over first.");
			return null;
		}
		try
		{
			await _messages.SendAsync(previous, L.F("I've taken over your draft \"{0}\", because nobody had worked on it for 3 days.", draft.Name));
		}
		catch (Exception)
		{
			// The take-over counts; the message is a courtesy.
		}
		return draft.Id;
	}

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
		foreach (var message in _all.Where(m => !m.IsRead).ToList())
			await _messages.MarkReadAsync(message);
	}

	/// <summary>Shows the messages, newest first.</summary>
	private void Show(IEnumerable<InboxMessage> messages)
	{
		// Show "New" based on the state when loaded, even after marking it read.
		_all = messages.Select(message => new InboxMessage
		{
			Id = message.Id, ToEmail = message.ToEmail, FromEmail = message.FromEmail, FromName = message.FromName, Text = message.Text,
			SentAt = message.SentAt, IsRead = message.IsRead, Kind = message.Kind, ReviewRequestId = message.ReviewRequestId,
			OffersResubmit = message.OffersResubmit, GameId = message.GameId, GameName = message.GameName, Language = message.Language,
			AboutEmail = message.AboutEmail, AboutName = message.AboutName, ReportCategory = message.ReportCategory, PageName = message.PageName
		}).ToList();
		ShowFiltered();
	}

	/// <summary>
	/// Everyone on the team except yourself - the owner, admins and moderators. Refreshed from the cloud when
	/// online.
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

		return members
			.Where(m => m.Email != MyEmail)
			.Select(m => new MessageRecipient(m.Email, m.Label,
				$"{(m.IsBuiltInAdmin ? L.T("Owner") : m.RoleName)} · {m.Email}", m.IsAdmin || m.IsBuiltInAdmin, m.LanguageInfo))
			.ToList();
	}

	/// <summary>
	/// Sends the message to each recipient, as a message of their own. Returns true when it reached everyone
	/// (StatusMessage says who it didn't reach otherwise).
	/// </summary>
	public async Task<bool> SendAsync(IReadOnlyList<MessageRecipient> recipients, string text)
	{
		StatusMessage = L.T("Sending...");
		var failed = new List<string>();
		string? error = null;
		foreach (var recipient in recipients)
		{
			try
			{
				await _messages.SendAsync(recipient.Email, text);
			}
			catch (InvalidOperationException ex)
			{
				// Empty or too long: the same for everyone.
				StatusMessage = ex.Message;
				return false;
			}
			catch (Exception ex)
			{
				failed.Add(recipient.Label);
				error ??= ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
			}
		}

		if (failed.Count == 0)
		{
			StatusMessage = recipients.Count == 1 ? L.T("Message sent.") : L.F("Message sent to {0} people.", recipients.Count);
			return true;
		}
		StatusMessage = failed.Count == recipients.Count
			? error!
			: L.F("Not sent to {0}: {1}", string.Join(", ", failed), error);
		return false;
	}

	/// <summary>Deletes a message from the user's Inbox.</summary>
	public async Task DeleteAsync(InboxMessage message)
	{
		try
		{
			await _messages.DeleteAsync(message);
			_all.Remove(message);
			ShowFiltered();
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
