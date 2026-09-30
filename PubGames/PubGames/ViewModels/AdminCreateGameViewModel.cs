using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// Create a new game, or edit/delete an existing one when opened with a
/// "gameId" navigation parameter from the admin library.
///
/// Admins change games directly. Moderators never do: everything they do here
/// becomes a review request (new game, changes to a game, deletion) that an
/// admin approves or rejects. A moderator can also reopen one of their own
/// requests ("requestId" parameter) to change it or withdraw it.
/// </summary>
public partial class AdminCreateGameViewModel : ObservableObject, IQueryAttributable
{
	private readonly ILocalDatabaseService _local;
	private readonly ICloudSyncService _cloud;
	private readonly IPermissionService _permissions;
	private readonly IAuthService _auth;
	private readonly ITeamService _team;
	private readonly IImageStore _images;
	private readonly ICategoryService _categories;

	// TODO: replace with the team the user picked once multiple teams exist.
	private const string CurrentTeamId = AuthService.DefaultTeamId;

	private string CurrentUserId => _auth.AccountId;

	/// <summary>The published game being edited; null when creating a new one.</summary>
	private PubGame? _editing;

	/// <summary>The moderator's own review request this page was opened from (or that already exists for this game).</summary>
	private ReviewRequest? _request;

	/// <summary>
	/// Raised after a successful save, submit, delete or withdraw so the page
	/// can navigate back. The text, if any, is shown to the user first.
	/// </summary>
	public event EventHandler<string?>? Finished;

	/// <summary>True for moderators: every action becomes a review request.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PageTitle), nameof(SaveButtonText), nameof(DeleteButtonText), nameof(CanDelete))]
	private bool isModerator;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PageTitle), nameof(SaveButtonText), nameof(CanDelete))]
	private bool isEditing;

	/// <summary>Explains to a moderator what happens with their submission (and why a previous one was rejected).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasReviewInfo))]
	private string reviewInfo = string.Empty;

	public bool HasReviewInfo => !string.IsNullOrEmpty(ReviewInfo);

	/// <summary>The moderator opened a request that's still waiting for review, so it can be withdrawn.</summary>
	[ObservableProperty]
	private bool canWithdraw;

	[ObservableProperty]
	private string name = string.Empty;

	/// <summary>The rules as text boxes with the pictures shown in between; saved as RulesText.</summary>
	public ObservableCollection<EditableRulesBlock> RulesBlocks { get; private set; } = EditableRulesBlock.FromRulesText(string.Empty);

	/// <summary>Image reference from IImageStore (not necessarily a URL, despite the model's field name).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasCover), nameof(CoverButtonText))]
	private string? coverImageUrl;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(UploadCoverCommand), nameof(SaveCommand))]
	private bool isUploading;

	public bool HasCover => !string.IsNullOrEmpty(CoverImageUrl);
	public string CoverButtonText => HasCover ? "Change cover image" : "Upload cover image";

	[ObservableProperty]
	private bool isPaid;

	[ObservableProperty]
	private decimal price = 0.50m;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsPlusMinus), nameof(SelectedScoringOption))]
	private ScoringType scoringType = ScoringType.PlusMinus;

	/// <summary>Plus / minus has settings to fill in; the form shows them only then.</summary>
	public bool IsPlusMinus => ScoringType == ScoringType.PlusMinus;

	// Plus / minus settings. Text rather than numbers, so a half-typed "-" or an empty box doesn't break the binding;
	// they're checked on save (see ReadScoringSettings).
	[ObservableProperty]
	private string scoreStep = "1";

	[ObservableProperty]
	private string startScore = "0";

	[ObservableProperty]
	private bool hasMaxScore;

	[ObservableProperty]
	private string maxScore = string.Empty;

	[ObservableProperty]
	private bool hasMinScore;

	[ObservableProperty]
	private string minScore = string.Empty;

	[ObservableProperty]
	private bool hasWarningScore;

	[ObservableProperty]
	private string warningScore = string.Empty;

	// Subgames and results, for every scoring type.
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SelectedResultModeOption), nameof(ScoreDirectionTitle))]
	private ResultMode resultMode = ResultMode.Winner;

	/// <summary>The direction choices follow the result type ("Highest score loses" for loser mode, ...).</summary>
	partial void OnResultModeChanged(ResultMode value)
	{
		OnPropertyChanged(nameof(BestScoreOptions));
		// The Picker clears its selection when its items change; show the choice again.
		OnPropertyChanged(nameof(BestScoreIndex));
	}

	public string ScoreDirectionTitle => ResultMode switch
	{
		ResultMode.Loser => "Who loses a subgame",
		ResultMode.Ranking => "Who is 1st in a subgame",
		_ => "Who wins a subgame"
	};

	public List<ResultModeOption> ResultModeOptions { get; } = ResultModeOption.All;

	public List<string> BestScoreOptions => ScoringSettings.ScoreDirectionOptions(ResultMode);

	private bool _lowestWins;

	/// <summary>
	/// The Picker's index: 1 means ScoringSettings.LowestWins. The -1 the Picker
	/// writes while its items change is ignored, so the choice isn't lost.
	/// </summary>
	public int BestScoreIndex
	{
		get => _lowestWins ? 1 : 0;
		set
		{
			if (value < 0 || (value == 1) == _lowestWins) return;
			_lowestWins = value == 1;
			OnPropertyChanged();
		}
	}

	public ResultModeOption? SelectedResultModeOption
	{
		get => ResultModeOptions.FirstOrDefault(o => o.Mode == ResultMode);
		set
		{
			if (value is not null)
				ResultMode = value.Mode;
		}
	}

	[ObservableProperty]
	private List<string> selectedCategories = new();

	[ObservableProperty]
	private string statusMessage = string.Empty;

	public string PageTitle => IsEditing ? "Edit game" : "New game";

	public string SaveButtonText => (IsModerator, IsEditing) switch
	{
		(false, false) => "Publish game",
		(false, true) => "Save changes",
		(true, false) => "Submit for review",
		(true, true) => "Submit changes for review"
	};

	public string DeleteButtonText => IsModerator ? "Request deletion" : "Delete game";

	/// <summary>Only an existing, published game can be deleted (or have its deletion requested).</summary>
	public bool CanDelete => _editing is not null;

	/// <summary>Bound to the scoring type Picker.</summary>
	public List<ScoringTypeOption> ScoringTypeOptions { get; } = ScoringTypeOption.All;

	public ScoringTypeOption? SelectedScoringOption
	{
		get => ScoringTypeOptions.FirstOrDefault(o => o.Type == ScoringType);
		set
		{
			if (value is not null)
				ScoringType = value.Type;
		}
	}

	/// <summary>Every category, with a tick for the ones this game is in. At least one is required.</summary>
	public ObservableCollection<CategoryChoice> CategoryChoices { get; } = new();

	/// <summary>What the game/proposal was in, until the category list has loaded.</summary>
	private HashSet<string> _selectedCategoryIds = new();

	public AdminCreateGameViewModel(ILocalDatabaseService local, ICloudSyncService cloud, IPermissionService permissions,
		IAuthService auth, ITeamService team, IImageStore images, ICategoryService categories)
	{
		_auth = auth;
		_team = team;
		_categories = categories;
		_images = images;
		_local = local;
		_cloud = cloud;
		_permissions = permissions;

		IsModerator = _team.Me is { } me && !_permissions.CanChangeGamesDirectly(me);
		if (IsModerator)
			ReviewInfo = "You're a moderator: when you submit, an admin reviews your game before players see it.";
	}

	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("requestId", out var rid) && rid is string requestId)
			await LoadRequestAsync(requestId);
		else if (query.TryGetValue("gameId", out var gid) && gid is string gameId)
			await LoadGameAsync(gameId);
	}

	private async Task LoadGameAsync(string gameId)
	{
		_editing = await _local.GetGameAsync(gameId);
		if (_editing is null) return;
		IsEditing = true;

		if (IsModerator)
		{
			// Continue from changes this moderator already submitted for this game, rather than starting over.
			_request = await FindMyWaitingRequestAsync(gameId, ReviewRequestType.GameChanges);
			if (_request is not null)
			{
				ShowProposal(_request);
				CanWithdraw = true;
				ReviewInfo = "Your changes to this game are waiting for review. Submitting again replaces them.";
				return;
			}
			ReviewInfo = "You're a moderator: your changes go to an admin for review. Players keep seeing the current version until an admin approves them.";
		}

		Name = _editing.Name;
		ShowRules(_editing.RulesText);
		CoverImageUrl = _editing.CoverImageUrl;
		IsPaid = _editing.IsPaid;
		Price = _editing.IsPaid ? _editing.Price : 0.50m;
		ShowScoring(_editing.ScoringType, _editing.ScoringSettings);
		SelectCategories(_editing.CategoryIds);
	}

	private async Task LoadRequestAsync(string requestId)
	{
		_request = await _local.GetReviewRequestAsync(requestId);
		if (_request is null) return;

		_editing = _request.Type == ReviewRequestType.GameChanges ? await _local.GetGameAsync(_request.GameId) : null;
		IsEditing = _editing is not null;
		OnPropertyChanged(nameof(CanDelete));
		ShowProposal(_request);

		CanWithdraw = _request.IsWaitingForReview;
		ReviewInfo = _request.Status switch
		{
			ReviewStatus.WaitingForReview => "This request is waiting for review. Submitting again replaces it.",
			ReviewStatus.Rejected when !string.IsNullOrWhiteSpace(_request.ReviewNote) =>
				$"An admin rejected this request: \"{_request.ReviewNote}\". Change what's needed and submit it again.",
			ReviewStatus.Rejected => "An admin rejected this request. Change what's needed and submit it again.",
			_ => "This request was approved. Submitting again sends a new request."
		};
	}

	private void ShowProposal(ReviewRequest request)
	{
		Name = request.ProposedName;
		ShowRules(request.ProposedRulesText);
		CoverImageUrl = request.ProposedCoverImageUrl;
		IsPaid = request.ProposedIsPaid;
		Price = request.ProposedIsPaid ? request.ProposedPrice : 0.50m;
		ShowScoring(request.ProposedScoringType, ScoringSettings.FromJson(request.ProposedScoringSettingsText));
		SelectCategories(request.ProposedCategoryIdsText.Split(',', StringSplitOptions.RemoveEmptyEntries));
	}

	/// <summary>Loads the category list (called when the page appears; the game may still be loading).</summary>
	public async Task LoadCategoriesAsync()
	{
		if (CategoryChoices.Count > 0) return;

		var categories = await _categories.GetCachedAsync();
		try
		{
			categories = await _categories.PullAsync();
		}
		catch (Exception)
		{
			// Offline: this phone's copy.
		}

		foreach (var category in categories)
			CategoryChoices.Add(new CategoryChoice { Category = category, IsSelected = _selectedCategoryIds.Contains(category.Id) });
	}

	private void SelectCategories(IEnumerable<string> ids)
	{
		_selectedCategoryIds = ids.ToHashSet();
		foreach (var choice in CategoryChoices)
			choice.IsSelected = _selectedCategoryIds.Contains(choice.Category.Id);
	}

	private List<string> SelectedCategoryIds() => CategoryChoices.Count > 0
		? CategoryChoices.Where(c => c.IsSelected).Select(c => c.Category.Id).ToList()
		: _selectedCategoryIds.ToList();

	/// <summary>
	/// A game with a scoring type that can't be picked any more shows as Plus /
	/// minus with the default settings, and switches to it when saved.
	/// </summary>
	private void ShowScoring(ScoringType type, ScoringSettings settings)
	{
		if (!ScoringTypeOption.IsOffered(type))
		{
			type = ScoringType.PlusMinus;
			settings = ScoringSettings.Default with { ResultMode = settings.ResultMode, LowestWins = settings.LowestWins };
		}
		ScoringType = type;
		ShowScoringSettings(settings);
	}

	private void ShowScoringSettings(ScoringSettings settings)
	{
		ScoreStep = settings.Step.ToString(CultureInfo.CurrentCulture);
		StartScore = settings.StartScore.ToString(CultureInfo.CurrentCulture);
		HasMaxScore = settings.MaxScore is not null;
		MaxScore = settings.MaxScore?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
		HasMinScore = settings.MinScore is not null;
		MinScore = settings.MinScore?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
		HasWarningScore = settings.WarningScore is not null;
		WarningScore = settings.WarningScore?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
		ResultMode = settings.ResultMode;
		_lowestWins = settings.LowestWins;
		OnPropertyChanged(nameof(BestScoreIndex));
	}

	/// <summary>
	/// The scoring settings from the form, or null with StatusMessage saying
	/// what to fix. The result mode applies to every scoring type; the rest
	/// only to Plus / minus.
	/// </summary>
	private ScoringSettings? ReadScoringSettings()
	{
		int? Read(string text, string what)
		{
			// Some keyboards type a real minus sign (−) instead of a hyphen.
			if (int.TryParse(text.Trim().Replace('−', '-'), NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out var value))
				return value;
			StatusMessage = $"Enter a whole number for the {what}.";
			return null;
		}

		var settings = new ScoringSettings { ResultMode = ResultMode, LowestWins = _lowestWins };

		if (IsPlusMinus)
		{
			if (Read(ScoreStep, "step") is not { } step) return null;
			if (Read(StartScore, "starting score") is not { } start) return null;
			int? max = null, min = null, warning = null;
			if (HasMaxScore && (max = Read(MaxScore, "maximum score")) is null) return null;
			if (HasMinScore && (min = Read(MinScore, "minimum score")) is null) return null;
			if (HasWarningScore && (warning = Read(WarningScore, "warning")) is null) return null;
			settings = settings with { Step = step, StartScore = start, MaxScore = max, MinScore = min, WarningScore = warning };
		}

		if (settings.Problem() is { } problem)
		{
			StatusMessage = problem;
			return null;
		}
		return settings;
	}

	private void ShowRules(string rulesText)
	{
		RulesBlocks = EditableRulesBlock.FromRulesText(rulesText);
		OnPropertyChanged(nameof(RulesBlocks));
	}

	private bool CanUpload() => !IsUploading;

	[RelayCommand(CanExecute = nameof(CanUpload))]
	private async Task UploadCoverAsync()
	{
		var reference = await UploadImageAsync();
		if (reference is not null)
			CoverImageUrl = reference;
	}

	[RelayCommand]
	private void RemoveCover() => CoverImageUrl = null;

	/// <summary>Uploads a picture and shows it in the rules where the cursor was (at the end when no text box was tapped).</summary>
	public async Task InsertRulesImageAsync(EditableRulesBlock? at, int cursor)
	{
		var reference = await UploadImageAsync();
		if (reference is not null)
			EditableRulesBlock.InsertImage(RulesBlocks, at, cursor, reference);
	}

	[RelayCommand]
	private void RemoveRulesImage(EditableRulesBlock image) =>
		EditableRulesBlock.RemoveImage(RulesBlocks, image);

	/// <summary>Picks and uploads one image. Returns its reference, or null if cancelled or failed (StatusMessage says why).</summary>
	private async Task<string?> UploadImageAsync()
	{
		if (IsUploading) return null;

		IsUploading = true;
		StatusMessage = "Uploading image...";
		try
		{
			var reference = await _images.PickAndUploadAsync();
			StatusMessage = string.Empty;
			return reference;
		}
		catch (HttpRequestException)
		{
			StatusMessage = "No connection - couldn't upload the image.";
		}
		catch (Exception ex)
		{
			StatusMessage = ex.Message;
		}
		finally
		{
			IsUploading = false;
		}
		return null;
	}

	/// <summary>Admins: publish or save directly. Moderators: submit a review request.</summary>
	[RelayCommand(CanExecute = nameof(CanUpload))]
	private async Task SaveAsync()
	{
		if (!_auth.IsSignedIn)
		{
			StatusMessage = "Sign in to create games.";
			return;
		}

		if (string.IsNullOrWhiteSpace(Name))
		{
			StatusMessage = "Give the game a name first.";
			return;
		}

		if (SelectedCategoryIds().Count == 0)
		{
			StatusMessage = "Pick at least one category, so players can find the game.";
			return;
		}

		if (ReadScoringSettings() is null)
			return;

		var me = _team.Me;
		if (me is null)
		{
			StatusMessage = "Only admins and moderators can create games.";
			return;
		}

		if (_permissions.CanChangeGamesDirectly(me))
			await PublishDirectlyAsync();
		else
			await SubmitForReviewAsync();
	}

	/// <summary>The form's content applied to a copy, so nothing changes until it's saved or approved.</summary>
	private PubGame FormAsGame(PubGame? basedOn)
	{
		var game = basedOn is null
			? new PubGame { TeamId = CurrentTeamId, CreatedByUserId = CurrentUserId }
			: new PubGame
			{
				Id = basedOn.Id,
				TeamId = basedOn.TeamId,
				CreatedByUserId = basedOn.CreatedByUserId,
				CreatedAt = basedOn.CreatedAt,
				Status = basedOn.Status,
				PublishedAt = basedOn.PublishedAt
			};
		game.Name = Name.Trim();
		game.RulesText = EditableRulesBlock.ToRulesText(RulesBlocks);
		game.CoverImageUrl = CoverImageUrl;
		game.ScoringType = ScoringType;
		// Checked by SaveAsync before saving; deleting keeps the saved settings if the form's are unfinished.
		game.ScoringSettings = ReadScoringSettings() ?? basedOn?.ScoringSettings ?? ScoringSettings.Default;
		game.IsPaid = IsPaid;
		game.Price = IsPaid ? Price : 0m;
		game.CategoryIds = SelectedCategoryIds();
		return game;
	}

	private async Task PublishDirectlyAsync()
	{
		var game = FormAsGame(_editing);
		game.Status = GameStatus.Published;
		game.PublishedAt ??= DateTime.UtcNow;

		// Cloud first: a change only counts once everyone else can actually see it.
		StatusMessage = IsEditing ? "Saving..." : "Publishing...";
		if (!await TrySaveToCloudAsync(game)) return;

		await _local.SaveGameAsync(game);
		StatusMessage = string.Empty;
		Finished?.Invoke(this, null);
	}

	private async Task SubmitForReviewAsync()
	{
		var proposal = FormAsGame(_editing);
		if (_editing is not null && SameContent(proposal, _editing))
		{
			StatusMessage = "Nothing has changed yet - make your changes first, then submit them for review.";
			return;
		}

		// Replace the request that's still waiting; after a decision, a fresh request is needed.
		var request = _request is { IsWaitingForReview: true }
			? _request
			: new ReviewRequest
			{
				// A resubmitted new game keeps its id, so it's still the same game once approved.
				GameId = _editing?.Id ?? _request?.GameId ?? proposal.Id,
				TeamId = CurrentTeamId,
				Type = _editing is null ? ReviewRequestType.NewGame : ReviewRequestType.GameChanges
			};
		request.SetProposal(proposal);
		request.SubmittedByUserId = CurrentUserId;
		request.SubmittedByEmail = TeamMember.NormalizeEmail(_auth.CurrentUser!.Email);
		request.SubmittedByName = _auth.CurrentUser.DisplayName;
		request.SubmittedAt = DateTime.UtcNow;
		request.Status = ReviewStatus.WaitingForReview;
		request.ReviewedByUserId = null;
		request.ReviewedAt = null;
		request.ReviewNote = string.Empty;

		StatusMessage = "Submitting for review...";
		if (!await TrySaveRequestAsync(request)) return;

		StatusMessage = string.Empty;
		Finished?.Invoke(this, request.Type == ReviewRequestType.NewGame
			? $"\"{request.SubjectName}\" is submitted for review. An admin decides whether it's published - you get the decision in your Inbox."
			: $"Your changes to \"{request.SubjectName}\" are submitted for review. Players keep seeing the current version until an admin approves - you get the decision in your Inbox.");
	}

	/// <summary>Admins: soft delete, the game drops out of every player's library on their next refresh. Moderators: request deletion.</summary>
	public async Task DeleteAsync()
	{
		if (_editing is null) return;

		var me = _team.Me;
		if (me is null) return;

		if (!_permissions.CanChangeGamesDirectly(me))
		{
			await RequestDeletionAsync();
			return;
		}

		var deleted = FormAsGame(_editing);
		deleted.Status = GameStatus.Deleted;
		StatusMessage = "Deleting...";
		if (!await TrySaveToCloudAsync(deleted)) return;

		// Keep the saved content, not unsaved edits from the form.
		_editing.Status = GameStatus.Deleted;
		await _local.SaveGameAsync(_editing);
		StatusMessage = string.Empty;
		Finished?.Invoke(this, null);
	}

	private async Task RequestDeletionAsync()
	{
		if (await FindMyWaitingRequestAsync(_editing!.Id, ReviewRequestType.Deletion) is not null)
		{
			StatusMessage = "You already requested deletion of this game - it's waiting for review.";
			return;
		}

		var request = new ReviewRequest
		{
			GameId = _editing.Id,
			TeamId = CurrentTeamId,
			Type = ReviewRequestType.Deletion,
			SubjectName = _editing.Name,
			SubmittedByUserId = CurrentUserId,
			SubmittedByEmail = TeamMember.NormalizeEmail(_auth.CurrentUser!.Email),
			SubmittedByName = _auth.CurrentUser.DisplayName
		};

		StatusMessage = "Requesting deletion...";
		if (!await TrySaveRequestAsync(request)) return;

		StatusMessage = string.Empty;
		Finished?.Invoke(this, $"Deletion of \"{_editing.Name}\" is requested. The game stays playable until an admin approves - you get the decision in your Inbox.");
	}

	/// <summary>Takes back the moderator's request while it's still waiting for review.</summary>
	public async Task WithdrawAsync()
	{
		if (_request is not { IsWaitingForReview: true }) return;

		StatusMessage = "Withdrawing...";
		try
		{
			await _cloud.WithdrawReviewRequestAsync(_request);
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message;
			return;
		}

		StatusMessage = string.Empty;
		Finished?.Invoke(this, "Request withdrawn.");
	}

	private async Task<ReviewRequest?> FindMyWaitingRequestAsync(string gameId, ReviewRequestType type)
	{
		var email = TeamMember.NormalizeEmail(_auth.CurrentUser?.Email ?? string.Empty);
		return (await _local.GetRequestsSubmittedByAsync(email))
			.FirstOrDefault(r => r.GameId == gameId && r.Type == type && r.IsWaitingForReview);
	}

	private static bool SameContent(PubGame a, PubGame b) =>
		a.Name == b.Name && a.RulesText == b.RulesText && a.CoverImageUrl == b.CoverImageUrl
		&& a.ScoringType == b.ScoringType && a.ScoringSettings == b.ScoringSettings && a.IsPaid == b.IsPaid && a.Price == b.Price
		&& a.CategoryIds.ToHashSet().SetEquals(b.CategoryIds);

	private async Task<bool> TrySaveToCloudAsync(PubGame game)
	{
		try
		{
			await _cloud.SaveGameAsync(game);
			return true;
		}
		catch (HttpRequestException)
		{
			StatusMessage = "No connection - try again when you're online.";
		}
		catch (Exception ex)
		{
			StatusMessage = ex.Message;
		}
		return false;
	}

	private async Task<bool> TrySaveRequestAsync(ReviewRequest request)
	{
		try
		{
			await _cloud.SaveReviewRequestAsync(request);
			return true;
		}
		catch (HttpRequestException)
		{
			StatusMessage = "No connection - try again when you're online.";
		}
		catch (Exception ex)
		{
			StatusMessage = ex.Message;
		}
		return false;
	}
}
