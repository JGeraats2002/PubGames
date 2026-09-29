using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// The admin library: every game including drafts, for admins and moderators
/// to open for editing or deleting. Moderators also see their own review
/// requests here with the admins' decisions; admins see how many requests are
/// waiting for their review.
/// </summary>
public partial class AdminLibraryViewModel : ObservableObject
{
	private readonly ILocalDatabaseService _local;
	private readonly ICloudSyncService _cloud;
	private readonly ITeamService _team;
	private readonly IPermissionService _permissions;
	private readonly IAuthService _auth;
	private readonly ICategoryService _categories;
	private readonly IMessageService _messages;

	// TODO: replace with the team the user picked once multiple teams exist.
	private const string CurrentTeamId = AuthService.DefaultTeamId;

	public ObservableCollection<PubGame> Games { get; } = new();

	/// <summary>
	/// Moderators only: their requests still waiting for review, newest first.
	/// Decisions arrive in the Inbox instead.
	/// </summary>
	public ObservableCollection<ReviewRequest> MyRequests { get; } = new();

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasUnreadMessages), nameof(UnreadMessagesText))]
	private int unreadMessageCount;

	public bool HasUnreadMessages => UnreadMessageCount > 0;

	public string UnreadMessagesText => UnreadMessageCount == 1
		? "You have 1 new message in your Inbox ›"
		: $"You have {UnreadMessageCount} new messages in your Inbox ›";

	[ObservableProperty]
	private string syncMessage = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(NewGameButtonText))]
	private bool isModerator;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasMyRequests))]
	private int myRequestCount;

	public bool HasMyRequests => MyRequestCount > 0;

	/// <summary>Admins only: shown as a shortcut to the Requests page.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasRequestsToReview), nameof(RequestsToReviewText))]
	private int requestsToReviewCount;

	public bool HasRequestsToReview => RequestsToReviewCount > 0;

	public string RequestsToReviewText => RequestsToReviewCount == 1
		? "1 request is waiting for your review ›"
		: $"{RequestsToReviewCount} requests are waiting for your review ›";

	public string NewGameButtonText => IsModerator ? "+ Propose a new game" : "+ New game";

	public AdminLibraryViewModel(ILocalDatabaseService local, ICloudSyncService cloud, ITeamService team,
		IPermissionService permissions, IAuthService auth, ICategoryService categories, IMessageService messages)
	{
		_local = local;
		_cloud = cloud;
		_team = team;
		_permissions = permissions;
		_auth = auth;
		_categories = categories;
		_messages = messages;
	}

	private string MyEmail => TeamMember.NormalizeEmail(_auth.CurrentUser?.Email ?? string.Empty);

	public async Task LoadAsync()
	{
		var me = _team.Me;
		IsModerator = me is not null && !_permissions.CanChangeGamesDirectly(me);
		var canReview = me is not null && _permissions.CanReviewRequests(me);

		await ShowLocalAsync(canReview);

		try
		{
			// Only team members may read drafts from the cloud (see firestore.rules).
			if (me is not null)
				await _cloud.PullAllGamesAsync();
			else
				await _cloud.PullPublishedGamesAsync();

			if (canReview)
				// The first admin to open the app after categories were introduced creates the defaults.
				await _categories.EnsureDefaultsAsync();
			else
				await _categories.PullAsync();

			if (IsModerator)
				await _cloud.PullRequestsSubmittedByAsync(MyEmail);
			if (canReview)
				await _cloud.PullRequestsWaitingForReviewAsync(CurrentTeamId);
			if (me is not null)
				await _messages.PullInboxAsync();

			SyncMessage = string.Empty;
		}
		catch (Exception ex)
		{
			SyncMessage = ex is HttpRequestException
				? "Offline - showing what's saved on this phone."
				: $"Couldn't refresh: {ex.Message}";
			return;
		}

		await ShowLocalAsync(canReview);
	}

	private async Task ShowLocalAsync(bool canReview)
	{
		var categories = await _categories.GetCachedAsync();
		Games.Clear();
		foreach (var game in await _local.GetManageableGamesAsync())
		{
			var label = _categories.LabelFor(game.CategoryIds, categories);
			game.CategoryLabel = label.Length == 0 ? "No category yet" : label;
			Games.Add(game);
		}

		MyRequests.Clear();
		if (IsModerator)
			foreach (var request in (await _local.GetRequestsSubmittedByAsync(MyEmail)).Where(r => r.IsWaitingForReview))
				MyRequests.Add(request);
		MyRequestCount = MyRequests.Count;

		RequestsToReviewCount = canReview ? (await _local.GetRequestsWaitingForReviewAsync(CurrentTeamId)).Count : 0;
		UnreadMessageCount = _team.Me is null ? 0 : (await _messages.GetCachedInboxAsync()).Count(m => !m.IsRead);
	}

	/// <summary>A moderator takes back a deletion request (other requests are withdrawn from the edit page).</summary>
	public async Task<bool> WithdrawAsync(ReviewRequest request)
	{
		try
		{
			await _cloud.WithdrawReviewRequestAsync(request);
		}
		catch (Exception ex)
		{
			SyncMessage = ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message;
			return false;
		}
		MyRequests.Remove(request);
		MyRequestCount = MyRequests.Count;
		return true;
	}
}
