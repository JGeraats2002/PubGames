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

	/// <summary>The games matching the search and category filter.</summary>
	public ObservableCollection<PubGame> Games { get; } = new();

	/// <summary>Every manageable game, before searching.</summary>
	private List<PubGame> _allGames = new();

	/// <summary>Matches game names.</summary>
	[ObservableProperty]
	private string searchText = string.Empty;

	/// <summary>Category clouds: "All" plus every category; several can be selected.</summary>
	public CategoryFilterSet Filters { get; } = new();

	/// <summary>"3 of 12 games" while searching or filtering.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasResultsText))]
	private string resultsText = string.Empty;

	/// <summary>Whether the number of results shows.</summary>
	public bool HasResultsText => ResultsText.Length > 0;

	partial void OnSearchTextChanged(string value) => ApplyFilter();

	/// <summary>Shows the games whose name contains the search text.</summary>
	private void ApplyFilter()
	{
		var text = SearchText.Trim();
		var matches = _allGames.Where(g =>
			Filters.Matches(g)
			&& (text.Length == 0 || g.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase))).ToList();

		Games.Clear();
		foreach (var game in matches)
			Games.Add(game);

		var filtered = text.Length > 0 || Filters.IsFiltering;
		ResultsText = !filtered ? string.Empty
			: matches.Count == 0 ? L.T("No games match your search.")
			: L.F("{0} of {1} games", matches.Count, _allGames.Count);
	}

	/// <summary>
	/// Moderators only: their requests still waiting for review, newest first.
	/// Decisions arrive in the Inbox instead.
	/// </summary>
	public ObservableCollection<ReviewRequest> MyRequests { get; } = new();

	/// <summary>How many Inbox messages haven't been read.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasUnreadMessages), nameof(UnreadMessagesText))]
	private int unreadMessageCount;

	/// <summary>Whether there are unread messages: a line shows at the top.</summary>
	public bool HasUnreadMessages => UnreadMessageCount > 0;

	/// <summary>"1 new message" or "3 new messages".</summary>
	public string UnreadMessagesText => UnreadMessageCount == 1
		? L.T("You have 1 new message in your Inbox ›")
		: L.F("You have {0} new messages in your Inbox ›", UnreadMessageCount);

	/// <summary>About refreshing from the cloud: offline, or what went wrong.</summary>
	[ObservableProperty]
	private string syncMessage = string.Empty;

	/// <summary>The user is a moderator: they propose instead of changing directly.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(NewGameButtonText))]
	private bool isModerator;

	/// <summary>How many of the moderator's own requests are waiting.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasMyRequests))]
	private int myRequestCount;

	/// <summary>Whether the moderator has requests waiting: they're listed at the top.</summary>
	public bool HasMyRequests => MyRequestCount > 0;

	/// <summary>Admins only: shown as a shortcut to the Requests page.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasRequestsToReview), nameof(RequestsToReviewText))]
	private int requestsToReviewCount;

	/// <summary>Whether requests are waiting for this admin: a line shows at the top.</summary>
	public bool HasRequestsToReview => RequestsToReviewCount > 0;

	/// <summary>"1 request to review" or "3 requests to review".</summary>
	public string RequestsToReviewText => RequestsToReviewCount == 1
		? L.T("1 request is waiting for your review ›")
		: L.F("{0} requests are waiting for your review ›", RequestsToReviewCount);

	/// <summary>"+ New game" for admins, "+ Propose a new game" for moderators.</summary>
	public string NewGameButtonText => IsModerator ? L.T("+ Propose a new game") : L.T("+ New game");

	/// <summary>
	/// Gets the services it works with - the phone's database, cloud sync, the team, the permissions, the
	/// sign-in, the categories and the Inbox - from the app (dependency injection).
	/// </summary>
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
		Filters.Changed += (_, _) => ApplyFilter();
	}

	private string MyEmail => TeamMember.NormalizeEmail(_auth.CurrentUser?.Email ?? string.Empty);

	/// <summary>
	/// Shows the games as saved on the phone, then refreshes them (and the requests) from the cloud.
	/// </summary>
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
			{
				await _categories.PullAsync();
				await _categories.PullGroupsAsync();
			}

			if (IsModerator)
			{
				// What's waiting from the other moderators, to show which games they're changing.
				// Before the others' own requests: that pull also drops the ones decided since.
				try
				{
					await _cloud.PullRequestsWaitingForReviewAsync(CurrentTeamId);
				}
				catch (CloudException)
				{
					// The cloud rules that allow this aren't published yet: then nothing is hidden.
				}
				await _cloud.PullRequestsSubmittedByAsync(MyEmail);
			}
			if (canReview)
				await _cloud.PullRequestsWaitingForReviewAsync(CurrentTeamId);
			if (me is not null)
				await _messages.PullInboxAsync();

			SyncMessage = string.Empty;
		}
		catch (Exception ex)
		{
			SyncMessage = ex is HttpRequestException
				? L.T("Offline - showing what's saved on this phone.")
				: L.F("Couldn't refresh: {0}", ex.Message);
			return;
		}

		await ShowLocalAsync(canReview);
	}

	/// <summary>
	/// Shows every game on the phone with its categories, and which ones have changes waiting for review.
	/// </summary>
	private async Task ShowLocalAsync(bool canReview)
	{
		var categories = await _categories.GetCachedAsync();
		_allGames = (await _local.GetManageableGamesAsync()).OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

		// A game another moderator has changes (or its deletion) waiting for stays in the list, but says who,
		// and can't be changed by the others until an admin decides or it's withdrawn: two sets of changes
		// to one game would clash.
		if (IsModerator)
		{
			var takenElsewhere = ReviewRequest.GamesTakenByOthers(await _local.GetRequestsWaitingForReviewAsync(CurrentTeamId), MyEmail);
			foreach (var game in _allGames)
				game.TakenBy = takenElsewhere.GetValueOrDefault(game.Id);
		}

		// Keeps the selected categories when the list refreshes (unless they were deleted).
		var groups = await _categories.GetCachedGroupsAsync();
		Filters.Show(groups, categories);

		// What each game's card says: its categories, and the main categories it has none of yet.
		var required = CategoryGroup.Arrange(groups, categories).Where(g => g.Group is not null && g.Categories.Count > 0).ToList();
		// Changes that are waiting for review: a moderator's own, or (for admins) everyone's.
		var waitingChanges = (IsModerator
				? (await _local.GetRequestsSubmittedByAsync(MyEmail)).Where(r => r.IsWaitingForReview)
				: canReview ? await _local.GetRequestsWaitingForReviewAsync(CurrentTeamId) : [])
			.Where(r => r.Type == ReviewRequestType.GameChanges)
			.ToList();
		List<string> Missing(IEnumerable<string> categoryIds)
		{
			var ids = categoryIds.ToHashSet();
			return required.Where(g => !g.Categories.Any(c => ids.Contains(c.Id))).Select(g => g.Group!.Name).ToList();
		}
		foreach (var game in _allGames)
		{
			game.CategoryLabel = _categories.LabelFor(game.CategoryIds, categories);
			var missing = Missing(game.CategoryIds);
			// Filled in by changes that are waiting for review: nothing left to do but the review.
			var filledIn = missing.Count > 0 && waitingChanges.Any(r => r.GameId == game.Id
				&& Missing(r.ProposedCategoryIdsText.Split(',', StringSplitOptions.RemoveEmptyEntries)).Count == 0);
			game.MissingLabel = missing.Count == 0 ? string.Empty
				: filledIn ? L.T("Waiting for review")
				: L.F("Still needs: {0}", string.Join(", ", missing));
		}
		ApplyFilter();

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
			SyncMessage = ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
			return false;
		}
		MyRequests.Remove(request);
		MyRequestCount = MyRequests.Count;
		return true;
	}
}

