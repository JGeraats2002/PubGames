using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
	private readonly IImageStore _images;

	private ReviewRequest? _request;

	/// <summary>The game as it is now; null for a new game (or one that no longer exists).</summary>
	private PubGame? _current;

	private List<GameCategory> _allCategories = new();

	private List<CategoryGroup> _allGroups = new();

	/// <summary>The category or main category a change is proposed for; null if it no longer exists.</summary>
	private GameCategory? _category;
	private CategoryGroup? _group;

	/// <summary>Raised after a decision so the page can navigate back. The text, if any, is shown first.</summary>
	public event EventHandler<string?>? Finished;

	/// <summary>What the request asks: "New game", "Changes to a game" ...</summary>
	[ObservableProperty]
	private string typeLabel = string.Empty;

	/// <summary>What it's about: the game's or category's name.</summary>
	[ObservableProperty]
	private string headline = string.Empty;

	/// <summary>Who sent it and when.</summary>
	[ObservableProperty]
	private string submittedLabel = string.Empty;

	/// <summary>Changes to a game: one line per changed part.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasChanges))]
	private List<string> changes = new();

	/// <summary>Whether there's a list of changes (for changes to a game).</summary>
	public bool HasChanges => Changes.Count > 0;

	/// <summary>False for category requests, which have no game to show.</summary>
	[ObservableProperty]
	private bool hasGamePreview;

	/// <summary>The name of the version that's shown.</summary>
	[ObservableProperty]
	private string previewTitle = string.Empty;

	/// <summary>The game after approval (new game, changes) or the game that would be deleted.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PreviewHasCover))]
	private PubGame? preview;

	/// <summary>Whether the version that's shown has a cover picture.</summary>
	public bool PreviewHasCover => !string.IsNullOrEmpty(Preview?.CoverImageUrl);

	/// <summary>The categories of the version that's shown.</summary>
	[ObservableProperty]
	private string previewCategories = string.Empty;

	/// <summary>The rules of the version that's shown, in text and pictures.</summary>
	[ObservableProperty]
	private List<RulesBlock> previewRules = new();

	/// <summary>
	/// The viewer can't decide on this request: a moderator looking at what
	/// another moderator submitted. They see the same, without Approve and Reject.
	/// </summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanDecide))]
	private bool isReadOnly;

	/// <summary>Whether Approve and Reject show (not after a decision).</summary>
	public bool CanDecide => !IsReadOnly;

	/// <summary>Changes to a game that still exists: the preview can switch between the game as it is and with the changes.</summary>
	[ObservableProperty]
	private bool canSwitch;

	/// <summary>The preview shows the game as it is now, rather than with the proposed changes.</summary>
	[ObservableProperty]
	private bool showingOriginal;

	/// <summary>The game with the proposed changes; what the preview switches back to.</summary>
	private PubGame? _proposed;

	/// <summary>Every change, sentence by sentence: first how it was, then how it would become.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasSentenceChanges))]
	private List<SentenceChange> sentenceChanges = [];

	/// <summary>There are changes to show sentence by sentence.</summary>
	public bool HasSentenceChanges => SentenceChanges.Count > 0;

	/// <summary>The whole game (and the side-by-side comparison) shows, not only the changes.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(FullGameButtonText))]
	private bool showFullGame = true;

	/// <summary>The button that shows or hides the whole game.</summary>
	public string FullGameButtonText => ShowFullGame ? L.T("Hide the whole game") : L.T("View the whole game");

	/// <summary>Shows or hides the whole game under the changes.</summary>
	[RelayCommand]
	private void ToggleFullGame() => ShowFullGame = !ShowFullGame;

	/// <summary>The languages the request touches: translations corrected or checked, a new original.</summary>
	public ObservableCollection<TranslationChange> TranslationChanges { get; } = [];

	/// <summary>The request touches translations.</summary>
	[ObservableProperty]
	private bool hasTranslationChanges;

	/// <summary>The translations that can be put next to the original to check them.</summary>
	[ObservableProperty]
	private List<AppLanguageInfo> compareLanguages = [];

	/// <summary>The translation shown next to the original.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CompareFlag))]
	private AppLanguageInfo? compareLanguage;

	partial void OnCompareLanguageChanged(AppLanguageInfo? value) => ShowComparison();

	/// <summary>The language the game is written in (with the proposed changes), for the comparison.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(OriginalFlag))]
	private AppLanguageInfo? originalLanguage;

	/// <summary>The original's flag.</summary>
	public string? OriginalFlag => OriginalLanguage?.Flag;

	/// <summary>The compared translation's flag.</summary>
	public string? CompareFlag => CompareLanguage?.Flag;

	/// <summary>The original and the chosen translation, piece by piece.</summary>
	[ObservableProperty]
	private List<ComparisonRow> comparisonRows = [];

	/// <summary>There's a translation to put next to the original.</summary>
	[ObservableProperty]
	private bool hasComparison;

	/// <summary>
	/// Shows which languages the request touches, who on the team uses the app in each (to ask them to look),
	/// and the first translation next to the original.
	/// </summary>
	private async Task ShowTranslationChangesAsync(PubGame? before, PubGame after)
	{
		var changes = TranslationChange.Between(before, after);
		List<TeamMember> members;
		try { members = await _team.GetCachedMembersAsync(); }
		catch (Exception) { members = []; }

		TranslationChanges.Clear();
		foreach (var change in changes)
		{
			var speakers = members.Where(m => m.Language == change.Language.Code).Select(m => m.Label).ToList();
			TranslationChanges.Add(change with
			{
				Speakers = speakers.Count == 0
					? L.F("Nobody on the team uses the app in {0} yet.", change.Language.Name)
					: L.F("Uses the app in {0}: {1}", change.Language.Name, string.Join(", ", speakers))
			});
		}
		HasTranslationChanges = TranslationChanges.Count > 0;
		if (HasTranslationChanges && Changes.Count == 1 && Changes[0] == NothingDiffers)
			Changes = [];

		OriginalLanguage = AppLanguage.Find(after.SourceLanguageCode);
		CompareLanguages = changes.Where(c => c.Kind != TranslationChangeKind.NewOriginal)
			.Select(c => c.Language).Where(l => after.Translations.ContainsKey(l.Code)).ToList();
		CompareLanguage = CompareLanguages.FirstOrDefault();
		HasComparison = CompareLanguage is not null;
	}

	/// <summary>Puts the chosen translation (with the proposed changes) next to the original, piece by piece.</summary>
	private void ShowComparison()
	{
		if ((_proposed ?? Preview) is not { } game || CompareLanguage is not { } language
			|| !game.Translations.TryGetValue(language.Code, out var translation))
		{
			ComparisonRows = [];
			return;
		}
		ComparisonRows = ComparisonRow.Pair(game.Name, game.RulesText, translation, language.Code);
	}

	/// <summary>The line shown when a change turns out to change nothing.</summary>
	private static string NothingDiffers => L.T("Nothing differs from the current version any more.");

	/// <summary>The approve button's text, for what approving does.</summary>
	[ObservableProperty]
	private string approveButtonText = L.T("Approve");

	/// <summary>Whether it can be approved (e.g. not when the game no longer exists).</summary>
	[ObservableProperty]
	private bool canApprove;

	/// <summary>What happened, or why it couldn't.</summary>
	[ObservableProperty]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	/// <summary>Who sent the request, for the confirmation questions.</summary>
	public string SubmitterLabel => _request?.SubmitterLabel ?? L.T("the moderator");

	/// <summary>
	/// Gets the services it works with - the phone's database, cloud sync, the permissions, the sign-in, the
	/// team, the categories, the Inbox and the picture store - from the app (dependency injection).
	/// </summary>
	public ReviewRequestViewModel(ILocalDatabaseService local, ICloudSyncService cloud, IPermissionService permissions,
		IAuthService auth, ITeamService team, ICategoryService categories, IMessageService messages, IImageStore images)
	{
		_images = images;
		_local = local;
		_cloud = cloud;
		_permissions = permissions;
		_auth = auth;
		_team = team;
		_categories = categories;
		_messages = messages;
	}

	/// <summary>
	/// Opened for a review request: shows what it proposes, and for changes what's different.
	/// </summary>
	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (!query.TryGetValue("requestId", out var id) || id is not string requestId) return;

		_request = await _local.GetReviewRequestAsync(requestId);
		if (_request is null)
		{
			_statusMessageTimer.ShowLasting(L.T("This request no longer exists - it may have been withdrawn."), v => StatusMessage = v);
			return;
		}

		_allCategories = await _categories.GetCachedAsync();
		_allGroups = await _categories.GetCachedGroupsAsync();
		try
		{
			_allCategories = await _categories.PullAsync();
			_allGroups = await _categories.PullGroupsAsync();
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

	/// <summary>Shows a review request: who sent it, what kind it is and what it proposes.</summary>
	private void Show(ReviewRequest r)
	{
		OnPropertyChanged(nameof(SubmitterLabel));
		TypeLabel = r.TypeLabel;
		SubmittedLabel = r.SubmittedLabel;
		HasGamePreview = r.Type is ReviewRequestType.NewGame or ReviewRequestType.GameChanges or ReviewRequestType.Deletion;
		IsReadOnly = !(_team.Me is { } me && _permissions.CanReviewRequests(me));
		_category = _allCategories.FirstOrDefault(c => c.Id == r.CategoryId);
		_group = _allGroups.FirstOrDefault(g => g.Id == r.CategoryId);
		var categoryGone = L.T("This category no longer exists, so this request can only be rejected.");
		var groupGone = L.T("This main category no longer exists, so this request can only be rejected.");
		var gameGone = _current is null or { Status: GameStatus.Deleted };

		switch (r.Type)
		{
			case ReviewRequestType.NewGame:
				Headline = L.F("{0} wants to add a new game: \"{1}\".", r.SubmitterLabel, r.ProposedName);
				PreviewTitle = L.T("How it will look for players");
				Preview = r.ProposedGame(null);
				ApproveButtonText = L.T("Approve and publish");
				CanApprove = true;
				_proposed = Preview;
				_ = ShowTranslationChangesAsync(null, Preview);
				break;

			case ReviewRequestType.GameChanges:
				Headline = L.F("{0} wants to change \"{1}\".", r.SubmitterLabel, _current?.ShownName ?? r.SubjectName);
				PreviewTitle = L.T("How it will look for players");
				Preview = r.ProposedGame(_current);
				Changes = gameGone ? [] : DescribeChanges(_current!, Preview);
				ApproveButtonText = L.T("Approve changes");
				CanApprove = !gameGone;
				_proposed = Preview;
				CanSwitch = !gameGone;
				if (!gameGone)
				{
					_ = ShowTranslationChangesAsync(_current, Preview);
					// First only what changes; the whole game behind a button.
					SentenceChanges = SentenceChange.Between(_current, Preview);
					ShowFullGame = SentenceChanges.Count == 0;
				}
				break;

			case ReviewRequestType.Deletion:
				Headline = L.F("{0} wants to delete \"{1}\" from every player's library.", r.SubmitterLabel, _current?.ShownName ?? r.SubjectName);
				PreviewTitle = L.T("The game that would be deleted");
				Preview = _current;
				ApproveButtonText = L.T("Approve deletion");
				CanApprove = !gameGone;
				break;

			case ReviewRequestType.NewCategory:
				var group = _allGroups.FirstOrDefault(g => g.Id == r.ProposedGroupId);
				var problem = _categories.ValidateNewName(r.ProposedName, r.ProposedGroupId ?? string.Empty, _allCategories)
					// Also requests from before main categories existed, which have none.
					?? (group is null && _allGroups.Count > 0
						? L.T("The main category it was proposed for doesn't exist (any more). Reject it and ask for a new proposal.")
						: null);
				Headline = group is null
					? L.F("{0} wants to add the category \"{1}\".", r.SubmitterLabel, r.ProposedName)
					: L.F("{0} wants to add the category \"{1}\" to the main category \"{2}\".", r.SubmitterLabel, r.ProposedName, group.Name);
				Changes = [problem ?? L.T("After approving, you'll get a request to choose which games belong in it.")];
				ApproveButtonText = L.T("Approve category");
				CanApprove = problem is null;
				break;

			case ReviewRequestType.NewCategoryGroup:
				var groupProblem = _categories.ValidateNewGroupName(r.ProposedName, _allGroups);
				Headline = L.F("{0} wants to add the main category \"{1}\".", r.SubmitterLabel, r.ProposedName);
				Changes = [groupProblem ?? L.T("After approving, add categories to it on the Categories page. Once it has categories, every game needs at least one of them.")];
				ApproveButtonText = L.T("Approve main category");
				CanApprove = groupProblem is null;
				break;

			case ReviewRequestType.RenameCategory:
				Headline = L.F("{0} wants to rename the category \"{1}\" to \"{2}\".", r.SubmitterLabel, _category?.Name ?? r.SubjectName, r.ProposedName);
				ShowCategoryChange(L.T("Approve rename"), _category is null ? categoryGone
					: _categories.ValidateNewName(r.ProposedName, _category.GroupId, _allCategories.Where(c => c.Id != _category.Id)),
					L.T("Games keep the category; only its name changes."));
				break;

			case ReviewRequestType.MoveCategory:
				var target = _allGroups.FirstOrDefault(g => g.Id == r.ProposedGroupId);
				Headline = L.F("{0} wants to move the category \"{1}\" to the main category \"{2}\".", r.SubmitterLabel, _category?.Name ?? r.SubjectName, target?.Name ?? r.ProposedName);
				ShowCategoryChange(L.T("Approve move"), _category is null ? categoryGone
					: target is null ? L.T("The main category it should move to no longer exists, so this request can only be rejected.")
					: _categories.ValidateNewName(_category.Name, target.Id, _allCategories.Where(c => c.Id != _category.Id)),
					L.T("Games keep the category; it's listed under the other main category."));
				break;

			case ReviewRequestType.DeleteCategory:
				Headline = L.F("{0} wants to delete the category \"{1}\".", r.SubmitterLabel, _category?.Name ?? r.SubjectName);
				ShowCategoryChange(L.T("Approve deletion"), _category is null ? categoryGone : null,
					L.T("The category is taken off every game it's in. This can't be undone."));
				break;

			case ReviewRequestType.RenameCategoryGroup:
				Headline = L.F("{0} wants to rename the main category \"{1}\" to \"{2}\".", r.SubmitterLabel, _group?.Name ?? r.SubjectName, r.ProposedName);
				ShowCategoryChange(L.T("Approve rename"), _group is null ? groupGone
					: _categories.ValidateNewGroupName(r.ProposedName, _allGroups.Where(g => g.Id != _group.Id)),
					L.T("Its categories stay as they are."));
				break;

			case ReviewRequestType.DeleteCategoryGroup:
				var inGroup = _allCategories.Where(c => c.GroupId == r.CategoryId).Select(c => c.Name).ToList();
				Headline = L.F("{0} wants to delete the main category \"{1}\".", r.SubmitterLabel, _group?.Name ?? r.SubjectName);
				ShowCategoryChange(L.T("Approve deletion"), _group is null ? groupGone
					: _allGroups.Count <= 1 ? L.T("This is the last main category, so it can't be deleted.")
					: null,
					inGroup.Count == 0
						? L.T("It has no categories. This can't be undone.")
						: L.F("Its categories ({0}) are deleted too and taken off every game. This can't be undone.", string.Join(", ", inGroup)));
				break;
		}

		if (gameGone && r.Type is ReviewRequestType.GameChanges or ReviewRequestType.Deletion)
			_statusMessageTimer.ShowLasting(L.T("This game no longer exists, so this request can only be rejected."), v => StatusMessage = v);

		ShowPreviewDetails();
	}

	/// <summary>The categories and rules of the game the preview shows.</summary>
	private void ShowPreviewDetails()
	{
		PreviewCategories = Preview is null ? string.Empty : _categories.LabelFor(Preview.CategoryIds, _allCategories);
		PreviewRules = Preview is null || string.IsNullOrWhiteSpace(Preview.RulesText)
			? []
			: RulesBlock.Parse(Preview.RulesText);
	}

	/// <summary>Switches the preview between the game as players see it now and the game with the proposed changes.</summary>
	public void ShowVersion(bool original)
	{
		if (!CanSwitch || _current is null || _proposed is null) return;

		ShowingOriginal = original;
		Preview = original ? _current : _proposed;
		PreviewTitle = original ? L.T("The game as players see it now") : L.T("How it will look for players");
		ShowPreviewDetails();
	}

	/// <summary>A message to the moderator who submitted this, in their Inbox. False when it couldn't be sent (StatusMessage says why).</summary>
	public async Task<bool> SendMessageAsync(string text)
	{
		if (_request is null) return false;
		try
		{
			await _messages.SendAsync(_request.SubmittedByEmail, text);
			return true;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - the message wasn't sent.") : ex.Message;
			return false;
		}
	}

	/// <summary>What the request is about, to start a message with.</summary>
	public string SubjectName => _current?.Name ?? _request?.SubjectName ?? string.Empty;

	/// <summary>A change to a category or main category: what approving does, or why it can't be approved.</summary>
	private void ShowCategoryChange(string approveText, string? problem, string consequence)
	{
		Changes = [problem ?? consequence];
		ApproveButtonText = approveText;
		CanApprove = problem is null;
	}

	/// <summary>
	/// What's different between the current game and the proposed one, one line per change.
	/// </summary>
	private List<string> DescribeChanges(PubGame before, PubGame after)
	{
		var changes = new List<string>();
		if (before.Name != after.Name)
			changes.Add(L.F("Name: \"{0}\" → \"{1}\"", before.Name, after.Name));
		if (before.SourceLanguageCode != after.SourceLanguageCode)
			changes.Add(L.F("Written in: {0} → {1}",
				AppLanguage.Find(before.SourceLanguageCode)?.Name ?? before.SourceLanguageCode,
				AppLanguage.Find(after.SourceLanguageCode)?.Name ?? after.SourceLanguageCode));
		if (before.CoverImageUrl != after.CoverImageUrl)
			changes.Add(string.IsNullOrEmpty(before.CoverImageUrl) ? L.T("Cover image: added")
				: string.IsNullOrEmpty(after.CoverImageUrl) ? L.T("Cover image: removed")
				: L.T("Cover image: replaced"));
		if (before.IsPaid != after.IsPaid || before.Price != after.Price)
			changes.Add(L.F("Price: {0} → {1}", before.PriceLabel, after.PriceLabel));
		if (before.ScoringType != after.ScoringType || before.ScoringSettings != after.ScoringSettings)
			changes.Add(L.F("Scoring: {0} → {1}", before.ScoringLabel, after.ScoringLabel));
		if (before.MinPlayers != after.MinPlayers || before.MaxPlayers != after.MaxPlayers)
			changes.Add(L.F("Players: {0} → {1}", before.PlayersLabel, after.PlayersLabel));
		if (!before.CategoryIds.ToHashSet().SetEquals(after.CategoryIds))
		{
			var old = _categories.LabelFor(before.CategoryIds, _allCategories);
			changes.Add(L.F("Categories: {0} → {1}", (old.Length == 0 ? L.T("none") : old), _categories.LabelFor(after.CategoryIds, _allCategories)));
		}
		if (before.RulesText != after.RulesText)
		{
			var picturesBefore = RulesBlock.Parse(before.RulesText).Count(b => b.IsImage);
			var picturesAfter = RulesBlock.Parse(after.RulesText).Count(b => b.IsImage);
			changes.Add(picturesBefore == picturesAfter
				? L.T("How to play: text changed")
				: L.F("How to play: text changed, pictures {0} → {1}", picturesBefore, picturesAfter));
		}
		if (changes.Count == 0)
			changes.Add(NothingDiffers);
		return changes;
	}

	/// <summary>
	/// Approves the request: the change happens (game saved, deleted or categories changed) and the moderator
	/// gets the decision in their Inbox.
	/// </summary>
	public async Task ApproveAsync()
	{
		if (_request is null || !IsAllowed()) return;

		StatusMessage = L.T("Approving...");
		try
		{
			// The change first: if that fails, the request stays waiting and can be approved again.
			switch (_request.Type)
			{
				case ReviewRequestType.NewCategory:
					await _categories.AddAsync(_request.ProposedName, _request.ProposedGroupId ?? string.Empty, _request.CategoryId);
					break;

				case ReviewRequestType.NewCategoryGroup:
					await _categories.AddGroupAsync(_request.ProposedName, _request.CategoryId);
					break;

				case ReviewRequestType.RenameCategory when _category is not null:
					await _categories.RenameAsync(_category, _request.ProposedName);
					break;

				case ReviewRequestType.MoveCategory when _category is not null:
					await _categories.MoveAsync(_category, _request.ProposedGroupId);
					break;

				case ReviewRequestType.DeleteCategory when _category is not null:
					await _categories.DeleteAsync(_category);
					break;

				case ReviewRequestType.RenameCategoryGroup when _group is not null:
					await _categories.RenameGroupAsync(_group, _request.ProposedName);
					break;

				case ReviewRequestType.DeleteCategoryGroup when _group is not null:
					await _categories.DeleteGroupAsync(_group);
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
					// The pictures the game showed before the changes, as far as it doesn't show them any more.
					await ImageCleanup.AfterSaveAsync(_local, _images, changed, ImageCleanup.RefsOf(_current));
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
			StatusMessage = ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
			return;
		}

		var informed = await TellModeratorAsync(L.F("Your request \"{0}\" was approved. ", _request.Summary) + _request.Type switch
		{
			ReviewRequestType.NewCategory => L.T("The category now exists; an admin will add games to it."),
			ReviewRequestType.NewCategoryGroup => L.T("The main category now exists; categories can be added to it."),
			ReviewRequestType.Deletion => L.T("The game has been removed from every player's library."),
			ReviewRequestType.NewGame or ReviewRequestType.GameChanges => L.T("Players can now see it."),
			_ => L.T("The change has been made.")
		});
		Finished?.Invoke(this, _request.Type switch
		{
			ReviewRequestType.NewCategory => L.T("Category added. You'll find a request to add games to it on the Requests page.") + informed,
			ReviewRequestType.NewCategoryGroup => L.T("Main category added. Add categories to it on the Categories page.") + informed,
			_ => informed.TrimStart()
		});
	}

	/// <param name="reason">Sent to the moderator's Inbox, so they know what to change.</param>
	public async Task RejectAsync(string reason)
	{
		if (_request is null || !IsAllowed()) return;

		StatusMessage = L.T("Rejecting...");
		try
		{
			await RecordDecisionAsync(ReviewStatus.Rejected, reason);
		}
		catch (Exception ex)
		{
			_request.Status = ReviewStatus.WaitingForReview;
			StatusMessage = ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
			return;
		}

		// The pictures it proposed aren't used by anything now: they're removed.
		if (_request.HasGameProposal)
		{
			await _local.SaveReviewRequestAsync(_request);
			await ImageCleanup.AfterRejectAsync(_local, _images, _request);
		}

		var informed = await TellModeratorAsync(
			L.F("Your request \"{0}\" was rejected.\n\nReason: {1}", _request.Summary, reason) +
			(_request.HasGameProposal ? L.T("\n\nTap \"Edit and submit again\" to change it and send it back for review.") : string.Empty),
			offersResubmit: _request.HasGameProposal);
		Finished?.Invoke(this, informed.TrimStart());
	}

	/// <summary>Saves the approved game in the cloud and on the phone.</summary>
	private async Task SaveGameAsync(PubGame game)
	{
		await _cloud.SaveGameAsync(game);
		await _local.SaveGameAsync(game);
	}

	/// <summary>Saves the decision on the request and sends it to the moderator's Inbox.</summary>
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
			return L.F(" The decision is saved, but {0} couldn't be sent a message about it - you can message them from the Inbox.", SubmitterLabel);
		}
	}

	/// <summary>Whether the user may decide on requests (admins); otherwise the page says why not.</summary>
	private bool IsAllowed()
	{
		if (_team.Me is { } me && _permissions.CanReviewRequests(me)) return true;

		StatusMessage = L.T("Only admins can approve or reject requests.");
		return false;
	}
}
