using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>Where a game's picture comes from.</summary>
public enum PictureSource
{
	/// <summary>Picked from the phone's photos.</summary>
	Library,

	/// <summary>Taken with the camera now.</summary>
	Camera,

	/// <summary>Made in the app's image editor.</summary>
	Drawing
}

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

	/// <summary>The rules as pieces of text with the pictures in between (see EditableRulesBlock); saved as RulesText.</summary>
	public ObservableCollection<EditableRulesBlock> RulesBlocks { get; private set; } = EditableRulesBlock.FromRulesText(string.Empty);

	/// <summary>Image reference from IImageStore (not necessarily a URL, despite the model's field name).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasCover), nameof(CoverButtonText))]
	private string? coverImageUrl;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
	private bool isUploading;

	public bool HasCover => !string.IsNullOrEmpty(CoverImageUrl);
	public string CoverButtonText => HasCover ? "Change cover image" : "Upload cover image";

	[ObservableProperty]
	private bool isPaid;

	[ObservableProperty]
	private decimal price = 0.50m;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsPlusMinus), nameof(IsManual), nameof(HasScoreSettings), nameof(SelectedScoringOption))]
	private ScoringType scoringType = ScoringType.PlusMinus;

	// A new game starts with nothing chosen in the dropdowns ("Choose ..."); an existing game shows its choices.
	private bool _scoringChosen;
	private bool _resultModeChosen;

	/// <summary>Plus / minus: a fixed step per tap.</summary>
	public bool IsPlusMinus => _scoringChosen && ScoringType == ScoringType.PlusMinus;

	/// <summary>Manual plus / minus: an amount typed in per tap, within the amount rules.</summary>
	public bool IsManual => _scoringChosen && ScoringType == ScoringType.ManualPlusMinus;

	/// <summary>Both plus / minus types share the start score, limits and warning.</summary>
	public bool HasScoreSettings => IsPlusMinus || IsManual;

	/// <summary>Manual plus / minus: the rules for typed amounts, linked with AND / OR.</summary>
	public ObservableCollection<EditableScoreRule> AmountRules { get; } = new();

	public void AddAmountRule() => AmountRules.Add(new EditableScoreRule());

	public void RemoveAmountRule(EditableScoreRule rule) => AmountRules.Remove(rule);

	public void ToggleJoiner(EditableScoreRule rule) => rule.ToggleJoiner();

	/// <summary>The rules as one sentence with brackets, so it's clear how AND and OR combine.</summary>
	public string AmountRulesPreview
	{
		get
		{
			if (AmountRules.Count == 0) return "No rules: any amount can be entered.";
			if (AmountRules.Any(r => r.Kind is null))
				return "Choose what each rule checks to see what's allowed.";
			var rules = AmountRules.Select(r => r.ToRule()!).ToList();
			if (rules.Any(r => r.NeedsValue && r.Value is null || r.NeedsSecondValue && r.Value2 is null))
				return "Fill in the numbers to see what's allowed.";
			return $"Allowed: amounts that are {AmountRuleSet.Describe(rules)}.";
		}
	}

	private void OnAmountRulesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		foreach (EditableScoreRule rule in e.NewItems ?? Array.Empty<EditableScoreRule>())
			rule.PropertyChanged += OnAmountRuleChanged;
		foreach (EditableScoreRule rule in e.OldItems ?? Array.Empty<EditableScoreRule>())
			rule.PropertyChanged -= OnAmountRuleChanged;
		for (var i = 0; i < AmountRules.Count; i++)
			AmountRules[i].IsFirst = i == 0;
		OnPropertyChanged(nameof(AmountRulesPreview));
	}

	private void OnAmountRuleChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
		OnPropertyChanged(nameof(AmountRulesPreview));

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
	[NotifyPropertyChangedFor(nameof(SelectedResultModeOption), nameof(ScoreDirectionTitle), nameof(ScoreDirectionPrompt))]
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

	/// <summary>The dropdown's text while nothing is chosen.</summary>
	public string ScoreDirectionPrompt => ResultMode switch
	{
		ResultMode.Loser => "Choose who loses",
		ResultMode.Ranking => "Choose who is 1st",
		_ => "Choose who wins"
	};

	public List<ResultModeOption> ResultModeOptions { get; } = ResultModeOption.All;

	public List<string> BestScoreOptions => ScoringSettings.ScoreDirectionOptions(ResultMode);

	/// <summary>Null until chosen.</summary>
	private bool? _lowestWins;

	/// <summary>
	/// The Picker's index: 1 means ScoringSettings.LowestWins, -1 nothing chosen
	/// yet. The -1 the Picker writes while its items change is ignored, so the
	/// choice isn't lost.
	/// </summary>
	public int BestScoreIndex
	{
		get => _lowestWins switch { null => -1, true => 1, false => 0 };
		set
		{
			if (value < 0 || (value == 1) == _lowestWins) return;
			_lowestWins = value == 1;
			OnPropertyChanged();
		}
	}

	public ResultModeOption? SelectedResultModeOption
	{
		get => _resultModeChosen ? ResultModeOptions.FirstOrDefault(o => o.Mode == ResultMode) : null;
		set
		{
			if (value is null) return;
			_resultModeChosen = true;
			ResultMode = value.Mode;
			OnPropertyChanged();
		}
	}

	[ObservableProperty]
	private List<string> selectedCategories = new();

	// Number of players. Each limit has its own switch; both off means any number.
	// Text rather than numbers, like the score settings; checked on save (see ReadPlayers).
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PlayersPreview))]
	private bool hasMinPlayers;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PlayersPreview))]
	private string minPlayers = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PlayersPreview))]
	private bool hasMaxPlayers;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PlayersPreview))]
	private string maxPlayers = string.Empty;

	/// <summary>What players will see, e.g. "Players: 2-6", or what still needs filling in.</summary>
	public string PlayersPreview => ReadPlayers(out var min, out var max) is { } problem
		? problem
		: $"Players: {PubGame.PlayersLabelFor(min, max)}";

	private void ShowPlayers(int? min, int? max)
	{
		HasMinPlayers = min is not null;
		MinPlayers = min?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
		HasMaxPlayers = max is not null;
		MaxPlayers = max?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
	}

	/// <summary>The minimum and maximum from the form (null where its switch is off). Returns what's wrong, or null when it's fine.</summary>
	private string? ReadPlayers(out int? min, out int? max)
	{
		min = max = null;
		if (HasMinPlayers)
		{
			if (!int.TryParse(MinPlayers.Trim(), NumberStyles.None, CultureInfo.CurrentCulture, out var least) || least < 1)
				return "Enter a whole number (1 or more) for the minimum number of players, or switch it off.";
			min = least;
		}
		if (HasMaxPlayers)
		{
			if (!int.TryParse(MaxPlayers.Trim(), NumberStyles.None, CultureInfo.CurrentCulture, out var most) || most < 1)
				return "Enter a whole number (1 or more) for the maximum number of players, or switch it off.";
			max = most;
		}
		return min > max ? "The minimum number of players can't be higher than the maximum." : null;
	}

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
		get => _scoringChosen ? ScoringTypeOptions.FirstOrDefault(o => o.Type == ScoringType) : null;
		set
		{
			if (value is null) return;
			_scoringChosen = true;
			ScoringType = value.Type;
			OnPropertyChanged();
			OnPropertyChanged(nameof(IsPlusMinus));
			OnPropertyChanged(nameof(IsManual));
			OnPropertyChanged(nameof(HasScoreSettings));
		}
	}

	/// <summary>Every category, with a tick for the ones this game is in.</summary>
	public ObservableCollection<CategoryChoice> CategoryChoices { get; } = new();

	/// <summary>
	/// The same choices under their main categories, as shown on the page. At
	/// least one is required from every main category.
	/// </summary>
	public ObservableCollection<CategoryChoiceGroup> CategoryChoiceGroups { get; } = new();

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

		AmountRules.CollectionChanged += OnAmountRulesChanged;

		IsModerator = _team.Me is { } me && !_permissions.CanChangeGamesDirectly(me);
	}

	/// <summary>Images uploaded while this form was open; the ones the saved game doesn't show are deleted again.</summary>
	private readonly List<string> _uploadedHere = new();

	/// <summary>The game or request was loaded into the form already.</summary>
	private bool _loaded;

	/// <summary>
	/// Shell calls this when the page opens, and again every time another page
	/// closes on top of it - the image editor, for one. Loading again then
	/// would throw away everything typed since, so it only loads once.
	/// </summary>
	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (_loaded) return;
		_loaded = true;

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
				ReviewInfo = "Your changes to this game are waiting for review. Submitting again will replace them.";
				return;
			}
		}

		Name = _editing.Name;
		ShowRules(_editing.RulesText);
		CoverImageUrl = _editing.CoverImageUrl;
		IsPaid = _editing.IsPaid;
		Price = _editing.IsPaid ? _editing.Price : 0.50m;
		ShowScoring(_editing.ScoringType, _editing.ScoringSettings);
		ShowPlayers(_editing.MinPlayers, _editing.MaxPlayers);
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
			ReviewStatus.WaitingForReview => "This request is waiting for review. Submitting again will replace it.",
			ReviewStatus.Rejected when !string.IsNullOrWhiteSpace(_request.ReviewNote) =>
				$"An admin rejected this request: \"{_request.ReviewNote}\". Change what's needed and submit it again.",
			ReviewStatus.Rejected => "An admin rejected this request. Change what's needed and submit it again.",
			_ => "This request was approved. Submitting again will send a new request."
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
		ShowPlayers(request.ProposedMinPlayers, request.ProposedMaxPlayers);
		SelectCategories(request.ProposedCategoryIdsText.Split(',', StringSplitOptions.RemoveEmptyEntries));
	}

	/// <summary>Loads the category list (called when the page appears; the game may still be loading).</summary>
	public async Task LoadCategoriesAsync()
	{
		if (CategoryChoices.Count > 0) return;

		var categories = await _categories.GetCachedAsync();
		var groups = await _categories.GetCachedGroupsAsync();
		try
		{
			categories = await _categories.PullAsync();
			groups = await _categories.PullGroupsAsync();
		}
		catch (Exception)
		{
			// Offline: this phone's copy.
		}

		foreach (var (group, inGroup) in CategoryGroup.Arrange(groups, categories))
		{
			// Categories without a main category are on their way out (an admin's app removes them).
			if (group is null && groups.Count > 0) continue;

			var choices = inGroup
				.Select(c => new CategoryChoice { Category = c, IsSelected = _selectedCategoryIds.Contains(c.Id) })
				.ToList();
			foreach (var choice in choices)
				CategoryChoices.Add(choice);
			// A main category without categories has nothing to choose from.
			if (choices.Count > 0)
				CategoryChoiceGroups.Add(new CategoryChoiceGroup { Group = group, Choices = choices });
		}
	}

	/// <summary>Null when every main category has a category chosen; otherwise what's still missing.</summary>
	private string? MissingCategories()
	{
		var missing = CategoryChoiceGroups.Where(g => g.IsRequired && !g.HasSelection).Select(g => g.Name).ToList();
		if (missing.Count > 0)
			return $"Pick at least one category for: {string.Join(", ", missing)}.";
		return SelectedCategoryIds().Count == 0 ? "Pick at least one category, so players can find the game." : null;
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
		_scoringChosen = true;
		ScoringType = type;
		OnPropertyChanged(nameof(SelectedScoringOption));
		OnPropertyChanged(nameof(IsPlusMinus));
		OnPropertyChanged(nameof(IsManual));
		OnPropertyChanged(nameof(HasScoreSettings));
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
		while (AmountRules.Count > 0)
			AmountRules.RemoveAt(0); // not Clear(): that doesn't say which rules left
		foreach (var rule in settings.AmountRules)
			AmountRules.Add(EditableScoreRule.From(rule));
		_resultModeChosen = true;
		ResultMode = settings.ResultMode;
		OnPropertyChanged(nameof(SelectedResultModeOption));
		_lowestWins = settings.LowestWins;
		OnPropertyChanged(nameof(BestScoreIndex));
	}

	/// <summary>
	/// The scoring settings from the form, or null with StatusMessage saying
	/// what to fix: numbers that aren't numbers, and settings that can't work
	/// together (ScoringSettings.Problem), e.g. a minimum above the starting
	/// score or amount rules that allow nothing. The result mode applies to
	/// every scoring type; the rest only to the plus / minus types.
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

		// Every dropdown needs a choice.
		if (!_scoringChosen)
		{
			StatusMessage = "Choose a scoring type.";
			return null;
		}
		if (!_resultModeChosen)
		{
			StatusMessage = "Choose how the result of each subgame is noted.";
			return null;
		}
		if (_lowestWins is not { } lowestWins)
		{
			StatusMessage = $"{ScoreDirectionPrompt}: the highest or the lowest score.";
			return null;
		}
		if (IsManual && AmountRules.Select((r, i) => (r, i)).FirstOrDefault(x => x.r.Kind is null) is { r: not null } missing)
		{
			StatusMessage = $"Rule {missing.i + 1}: choose what it checks.";
			return null;
		}

		var settings = new ScoringSettings { ResultMode = ResultMode, LowestWins = lowestWins };

		if (HasScoreSettings)
		{
			var step = 1;
			if (IsPlusMinus)
			{
				if (Read(ScoreStep, "step") is not { } typedStep) return null;
				step = typedStep;
			}
			if (Read(StartScore, "starting score") is not { } start) return null;
			int? max = null, min = null, warning = null;
			if (HasMaxScore && (max = Read(MaxScore, "maximum score")) is null) return null;
			if (HasMinScore && (min = Read(MinScore, "minimum score")) is null) return null;
			if (HasWarningScore && (warning = Read(WarningScore, "warning")) is null) return null;
			settings = settings with
			{
				Step = step, StartScore = start, MaxScore = max, MinScore = min, WarningScore = warning,
				AmountRules = IsManual ? AmountRules.Select(r => r.ToRule()!).ToList() : []
			};
		}

		if (settings.Problem(ScoringType) is { } problem)
		{
			StatusMessage = problem;
			return null;
		}
		return settings;
	}

	private void ShowRules(string rules)
	{
		RulesBlocks = EditableRulesBlock.FromRulesText(rules);
		OnPropertyChanged(nameof(RulesBlocks));
	}

	private bool CanUpload() => !IsUploading;

	/// <summary>Sets the cover to a picture from the given source.</summary>
	public async Task SetCoverAsync(PictureSource source, EditedImage? drawing = null)
	{
		var reference = await UploadPictureAsync(source, drawing);
		if (reference is not null)
			CoverImageUrl = reference;
	}

	/// <summary>The drawing a picture was made from in the image editor, or null if it can't be edited.</summary>
	public Task<string?> GetDrawingAsync(string? reference) =>
		string.IsNullOrEmpty(reference) ? Task.FromResult<string?>(null) : _images.GetDrawingAsync(reference);

	[RelayCommand]
	private void RemoveCover() => CoverImageUrl = null;

	/// <summary>
	/// Uploads one image: picked from the phone, taken with the camera, or made
	/// in the image editor (then given as drawing). Returns its reference, or
	/// null if cancelled or failed (StatusMessage says why).
	/// </summary>
	public async Task<string?> UploadPictureAsync(PictureSource source, EditedImage? drawing = null)
	{
		if (IsUploading) return null;

		IsUploading = true;
		StatusMessage = "Uploading image...";
		try
		{
			var reference = source switch
			{
				PictureSource.Camera => await _images.TakePhotoAndUploadAsync(),
				PictureSource.Drawing when drawing is not null => await _images.UploadAsync(drawing.Image, drawing.Drawing),
				_ => await _images.PickAndUploadAsync()
			};
			StatusMessage = string.Empty;
			if (reference is not null)
				_uploadedHere.Add(reference);
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

		if (ReadPlayers(out _, out _) is { } playersProblem)
		{
			StatusMessage = playersProblem;
			return;
		}

		if (MissingCategories() is { } missing)
		{
			StatusMessage = missing;
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
		// Checked by SaveAsync before saving; deleting keeps the saved numbers if the form's are unfinished.
		(game.MinPlayers, game.MaxPlayers) = ReadPlayers(out var min, out var max) is null
			? (min, max)
			: (basedOn?.MinPlayers, basedOn?.MaxPlayers);
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

		// What the game showed before and what was tried out here, as far as it's not shown any more.
		await ImageCleanup.AfterSaveAsync(_local, _images, game,
			RulesImages.Refs(_editing?.CoverImageUrl, _editing?.RulesText).Concat(_uploadedHere));

		StatusMessage = string.Empty;
		Finished?.Invoke(this, null);
	}

	/// <summary>
	/// Whether another moderator got in first with changes to (or the deletion
	/// of) this game since the library was opened; if so StatusMessage says so.
	/// </summary>
	private async Task<bool> IsTakenByAnotherModeratorAsync()
	{
		if (_editing is null) return false;

		List<ReviewRequest> waiting;
		try
		{
			waiting = await _cloud.PullRequestsWaitingForReviewAsync(CurrentTeamId);
		}
		catch (Exception)
		{
			// Can't be checked right now; submitting itself will say if there's no connection.
			return false;
		}

		var email = TeamMember.NormalizeEmail(_auth.CurrentUser?.Email ?? string.Empty);
		if (!ReviewRequest.GamesTakenByOthers(waiting, email).TryGetValue(_editing.Id, out var taken)) return false;

		StatusMessage = $"{taken.SubmitterLabel} has already submitted a request for this game, which is waiting for review. You can change the game again once an admin has decided on it, or it's withdrawn.";
		return true;
	}

	private async Task SubmitForReviewAsync()
	{
		if (await IsTakenByAnotherModeratorAsync()) return;

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
			? $"\"{request.SubjectName}\" has been submitted for review. An admin will decide whether it's published; you'll get the decision in your Inbox."
			: $"Your changes to \"{request.SubjectName}\" have been submitted for review. Players will keep seeing the current version until an admin approves them; you'll get the decision in your Inbox.");
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
		if (await IsTakenByAnotherModeratorAsync()) return;

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
		Finished?.Invoke(this, $"You've requested the deletion of \"{_editing.Name}\". The game stays playable until an admin approves it; you'll get the decision in your Inbox.");
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
		// Older games have their pictures without the empty lines around them; that's no change.
		a.Name == b.Name && RulesImages.Normalize(a.RulesText) == RulesImages.Normalize(b.RulesText) && a.CoverImageUrl == b.CoverImageUrl
		&& a.ScoringType == b.ScoringType && a.ScoringSettings == b.ScoringSettings && a.IsPaid == b.IsPaid && a.Price == b.Price
		&& a.MinPlayers == b.MinPlayers && a.MaxPlayers == b.MaxPlayers
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
