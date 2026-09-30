using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// One review request from a moderator, for an admin to approve or reject.
/// Shows exactly what would change and a preview of the game as players would
/// see it afterwards. The decision is sent to the moderator's Inbox.
/// Opened with a "requestId" navigation parameter. ("Add games to category"
/// requests open CategoryGamesPage instead.)
/// </summary>
public partial class ReviewRequestViewModel : ObservableObject, IQueryAttributable
{
	private readonly ILocalDatabaseService _local;
	private readonly ICloudSyncService _cloud;
	private readonly IPermissionService _permissions;
	private readonly IAuthService _auth;
	private readonly ITeamService _team;
	private readonly ICategoryService _categories;
	private readonly IMessageService _messages;

	private ReviewRequest? _request;

	/// <summary>The game as it is now; null for a new game (or one that no longer exists).</summary>
	private PubGame? _current;

	private List<GameCategory> _allCategories = new();

	/// <summary>Raised after a decision so the page can navigate back. The text, if any, is shown first.</summary>
	public event EventHandler<string?>? Finished;

	[ObservableProperty]
	private string typeLabel = string.Empty;

	[ObservableProperty]
	private string headline = string.Empty;

	[ObservableProperty]
	private string submittedLabel = string.Empty;

	/// <summary>Changes to a game: one line per changed part.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasChanges))]
	private List<string> changes = new();

	public bool HasChanges => Changes.Count > 0;

	/// <summary>False for category requests, which have no game to show.</summary>
	[ObservableProperty]
	private bool hasGamePreview;

	[ObservableProperty]
	private string previewTitle = string.Empty;

	/// <summary>The game after approval (new game, changes) or the game that would be deleted.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PreviewHasCover))]
	private PubGame? preview;

	public bool PreviewHasCover => !string.IsNullOrEmpty(Preview?.CoverImageUrl);

	[ObservableProperty]
	private string previewCategories = string.Empty;

	[ObservableProperty]
	private List<RulesBlock> previewRules = new();

	[ObservableProperty]
	private string approveButtonText = "Approve";

	[ObservableProperty]
	private bool canApprove;

	[ObservableProperty]
	private string statusMessage = string.Empty;

	public string SubmitterLabel => _request?.SubmitterLabel ?? "the moderator";

	public ReviewRequestViewModel(ILocalDatabaseService local, ICloudSyncService cloud, IPermissionService permissions,
		IAuthService auth, ITeamService team, ICategoryService categories, IMessageService messages)
	{
		_local = local;
		_cloud = cloud;
		_permissions = permissions;
		_auth = auth;
		_team = team;
		_categories = categories;
		_messages = messages;
	}

	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (!query.TryGetValue("requestId", out var id) || id is not string requestId) return;

		_request = await _local.GetReviewRequestAsync(requestId);
		if (_request is null)
		{
			StatusMessage = "This request no longer exists - it may have been withdrawn.";
			return;
		}

		_allCategories = await _categories.GetCachedAsync();
		try
		{
			_allCategories = await _categories.PullAsync();
			if (_request.Type is ReviewRequestType.GameChanges or ReviewRequestType.Deletion)
				// The newest version, so "what changes" compares against what players see right now.
				_current = await _cloud.PullGameAsync(_request.GameId);
		}
		catch (Exception)
		{
			// Offline: compare against this phone's copy.
			_current = await _local.GetGameAsync(_request.GameId);
		}

		Show(_request);
	}

	private void Show(ReviewRequest r)
	{
		OnPropertyChanged(nameof(SubmitterLabel));
		TypeLabel = r.TypeLabel;
		SubmittedLabel = r.SubmittedLabel;
		HasGamePreview = r.Type != ReviewRequestType.NewCategory;
		var gameGone = _current is null or { Status: GameStatus.Deleted };

		switch (r.Type)
		{
			case ReviewRequestType.NewGame:
				Headline = $"{r.SubmitterLabel} wants to add a new game: \"{r.ProposedName}\".";
				PreviewTitle = "How it will look for players";
				Preview = r.ProposedGame(null);
				ApproveButtonText = "Approve and publish";
				CanApprove = true;
				break;

			case ReviewRequestType.GameChanges:
				Headline = $"{r.SubmitterLabel} wants to change \"{_current?.Name ?? r.SubjectName}\".";
				PreviewTitle = "How it will look for players";
				Preview = r.ProposedGame(_current);
				Changes = gameGone ? [] : DescribeChanges(_current!, Preview);
				ApproveButtonText = "Approve changes";
				CanApprove = !gameGone;
				break;

			case ReviewRequestType.Deletion:
				Headline = $"{r.SubmitterLabel} wants to delete \"{_current?.Name ?? r.SubjectName}\" from every player's library.";
				PreviewTitle = "The game that would be deleted";
				Preview = _current;
				ApproveButtonText = "Approve deletion";
				CanApprove = !gameGone;
				break;

			case ReviewRequestType.NewCategory:
				var problem = _categories.ValidateNewName(r.ProposedName, _allCategories);
				Headline = $"{r.SubmitterLabel} wants to add the category \"{r.ProposedName}\".";
				Changes = [problem ?? "After approving, you'll get a request to choose which games belong in it."];
				ApproveButtonText = "Approve category";
				CanApprove = problem is null;
				break;
		}

		if (gameGone && r.Type is ReviewRequestType.GameChanges or ReviewRequestType.Deletion)
			StatusMessage = "This game no longer exists, so this request can only be rejected.";

		PreviewCategories = Preview is null ? string.Empty : _categories.LabelFor(Preview.CategoryIds, _allCategories);
		PreviewRules = Preview is null || string.IsNullOrWhiteSpace(Preview.RulesText)
			? []
			: RulesBlock.Parse(Preview.RulesText);
	}

	private List<string> DescribeChanges(PubGame before, PubGame after)
	{
		var changes = new List<string>();
		if (before.Name != after.Name)
			changes.Add($"Name: \"{before.Name}\" → \"{after.Name}\"");
		if (before.CoverImageUrl != after.CoverImageUrl)
			changes.Add(string.IsNullOrEmpty(before.CoverImageUrl) ? "Cover image: added"
				: string.IsNullOrEmpty(after.CoverImageUrl) ? "Cover image: removed"
				: "Cover image: replaced");
		if (before.IsPaid != after.IsPaid || before.Price != after.Price)
			changes.Add($"Price: {before.PriceLabel} → {after.PriceLabel}");
		if (before.ScoringType != after.ScoringType || before.ScoringSettings != after.ScoringSettings)
			changes.Add($"Scoring: {before.ScoringLabel} → {after.ScoringLabel}");
		if (!before.CategoryIds.ToHashSet().SetEquals(after.CategoryIds))
		{
			var old = _categories.LabelFor(before.CategoryIds, _allCategories);
			changes.Add($"Categories: {(old.Length == 0 ? "none" : old)} → {_categories.LabelFor(after.CategoryIds, _allCategories)}");
		}
		if (before.RulesText != after.RulesText)
		{
			var picturesBefore = RulesBlock.Parse(before.RulesText).Count(b => b.IsImage);
			var picturesAfter = RulesBlock.Parse(after.RulesText).Count(b => b.IsImage);
			changes.Add(picturesBefore == picturesAfter
				? "How to play: text changed"
				: $"How to play: text changed, pictures {picturesBefore} → {picturesAfter}");
		}
		if (changes.Count == 0)
			changes.Add("Nothing differs from the current version any more.");
		return changes;
	}

	public async Task ApproveAsync()
	{
		if (_request is null || !IsAllowed()) return;

		StatusMessage = "Approving...";
		try
		{
			// The change first: if that fails, the request stays waiting and can be approved again.
			switch (_request.Type)
			{
				case ReviewRequestType.NewCategory:
					await _categories.AddAsync(_request.ProposedName, _request.CategoryId);
					break;

				case ReviewRequestType.NewGame:
					var created = _request.ProposedGame(null);
					created.CreatedAt = DateTime.UtcNow;
					created.Status = GameStatus.Published;
					created.PublishedAt = DateTime.UtcNow;
					await SaveGameAsync(created);
					break;

				case ReviewRequestType.GameChanges when _current is not null:
					var changed = _request.ProposedGame(_current);
					changed.Status = GameStatus.Published;
					changed.PublishedAt ??= DateTime.UtcNow;
					await SaveGameAsync(changed);
					break;

				case ReviewRequestType.Deletion when _current is not null:
					_current.Status = GameStatus.Deleted;
					await SaveGameAsync(_current);
					break;

				default:
					return;
			}

			await RecordDecisionAsync(ReviewStatus.Approved, note: string.Empty);
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message;
			return;
		}

		var informed = await TellModeratorAsync($"Your request \"{_request.Summary}\" was approved. " + _request.Type switch
		{
			ReviewRequestType.NewCategory => "The category now exists; an admin will add games to it.",
			ReviewRequestType.Deletion => "The game is removed from every player's library.",
			_ => "Players can now see it."
		});
		Finished?.Invoke(this, _request.Type == ReviewRequestType.NewCategory
			? "Category added. You'll find a request to add games to it on the Requests page." + informed
			: informed.TrimStart());
	}

	/// <param name="reason">Sent to the moderator's Inbox, so they know what to change.</param>
	public async Task RejectAsync(string reason)
	{
		if (_request is null || !IsAllowed()) return;

		StatusMessage = "Rejecting...";
		try
		{
			await RecordDecisionAsync(ReviewStatus.Rejected, reason);
		}
		catch (Exception ex)
		{
			_request.Status = ReviewStatus.WaitingForReview;
			StatusMessage = ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message;
			return;
		}

		var informed = await TellModeratorAsync(
			$"Your request \"{_request.Summary}\" was rejected.\n\nReason: {reason}" +
			(_request.HasGameProposal ? "\n\nTap \"Edit and submit again\" to change it and send it back for review." : string.Empty),
			offersResubmit: _request.HasGameProposal);
		Finished?.Invoke(this, informed.TrimStart());
	}

	private async Task SaveGameAsync(PubGame game)
	{
		await _cloud.SaveGameAsync(game);
		await _local.SaveGameAsync(game);
	}

	private Task RecordDecisionAsync(ReviewStatus decision, string note)
	{
		_request!.Status = decision;
		_request.ReviewNote = note;
		_request.ReviewedByUserId = _auth.AccountId;
		_request.ReviewedAt = DateTime.UtcNow;
		return _cloud.SaveReviewRequestAsync(_request);
	}

	/// <summary>The decision goes to the moderator's Inbox. Returns a note for the admin if that failed.</summary>
	private async Task<string> TellModeratorAsync(string text, bool offersResubmit = false)
	{
		try
		{
			await _messages.SendAsync(_request!.SubmittedByEmail, text, MessageKind.ReviewDecision, _request.Id, offersResubmit);
			return string.Empty;
		}
		catch (Exception)
		{
			return $" The decision is saved, but {SubmitterLabel} couldn't be sent a message about it - you can message them from the Inbox.";
		}
	}

	private bool IsAllowed()
	{
		if (_team.Me is { } me && _permissions.CanReviewRequests(me)) return true;

		StatusMessage = "Only admins can approve or reject requests.";
		return false;
	}
}
