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
/// The parts of the scoring page that are added with "+ Add your rules". A
/// new game starts without any; they always show in this order, whatever order
/// they were added in.
/// </summary>
public enum GameFormSection
{
	ScoringType,
	Results,
	Ties,
	Turns
}

/// <summary>A part of the form, its title in the add list, and the form page it's on.</summary>
public record GameFormSectionInfo(GameFormSection Section, string Title, int Page);

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

	// The form's three pages: general info, scoring, written rules.
	public const int GeneralPage = 0;
	public const int ScoringPage = 1;
	public const int RulesPage = 2;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsGeneralPage), nameof(IsScoringPage), nameof(IsRulesPage), nameof(CanAddSection), nameof(HasNothingAdded))]
	private int currentPage = GeneralPage;

	public bool IsGeneralPage => CurrentPage == GeneralPage;
	public bool IsScoringPage => CurrentPage == ScoringPage;
	public bool IsRulesPage => CurrentPage == RulesPage;

	/// <summary>Every part that can be added, in the order the form shows them.</summary>
	public static IReadOnlyList<GameFormSectionInfo> Sections { get; } =
	[
		new(GameFormSection.ScoringType, "Scoring type", ScoringPage),
		new(GameFormSection.Results, "Subgame results", ScoringPage),
		new(GameFormSection.Ties, "Ties", ScoringPage),
		new(GameFormSection.Turns, "Turns and the end of a subgame", ScoringPage)
	];

	[ObservableProperty] private bool scoringTypeAdded;
	[ObservableProperty] private bool resultsAdded;
	[ObservableProperty] private bool tiesAdded;
	[ObservableProperty] private bool turnsAdded;

	public bool IsAdded(GameFormSection section) => section switch
	{
		GameFormSection.ScoringType => ScoringTypeAdded,
		GameFormSection.Results => ResultsAdded,
		GameFormSection.Ties => TiesAdded,
		_ => TurnsAdded
	};

	private void SetAdded(GameFormSection section, bool added)
	{
		switch (section)
		{
			case GameFormSection.ScoringType: ScoringTypeAdded = added; break;
			case GameFormSection.Results: ResultsAdded = added; break;
			case GameFormSection.Ties: TiesAdded = added; break;
			default: TurnsAdded = added; break;
		}
		OnPropertyChanged(nameof(CanAddSection));
		OnPropertyChanged(nameof(HasNothingAdded));
	}

	/// <summary>What can still be added on the page that's open, in the form's order.</summary>
	public List<GameFormSectionInfo> AddableSections() =>
		Sections.Where(s => s.Page == CurrentPage && !IsAdded(s.Section)).ToList();

	/// <summary>The page has something left to add (only the scoring page has parts to add).</summary>
	public bool CanAddSection => AddableSections().Count > 0;

	/// <summary>Nothing has been added on the scoring page yet: show how to start.</summary>
	public bool HasNothingAdded => CurrentPage == ScoringPage
		&& Sections.Where(s => s.Page == CurrentPage).All(s => !IsAdded(s.Section));

	public static string TitleOf(GameFormSection section) => Sections.First(s => s.Section == section).Title;

	public void AddSection(GameFormSection section) => SetAdded(section, true);

	/// <summary>Takes a part off the form again; what was filled in there goes back to how a new game starts.</summary>
	public void RemoveSection(GameFormSection section)
	{
		SetAdded(section, false);
		switch (section)
		{
			case GameFormSection.ScoringType:
				Scoring.Reset();
				OnPropertyChanged(nameof(ScoringType));
				break;

			case GameFormSection.Results:
				_resultModeChosen = false;
				ResultMode = ResultMode.Winner;
				_lowestWins = null;
				OnPropertyChanged(nameof(SelectedResultModeOption));
				OnPropertyChanged(nameof(BestScoreIndex));
				break;

			case GameFormSection.Ties:
				AllowTies = true;
				_tieBreakMoment = TieBreakMoment.SubgamesAndFinal;
				_tieBreakScope = TieBreakScope.Decisive;
				OnPropertyChanged(nameof(SelectedTieBreakMoment));
				OnPropertyChanged(nameof(SelectedTieBreakScope));
				DuelSameScoring = true;
				DuelScoring.Reset();
				_duelLowestWins = null;
				OnPropertyChanged(nameof(DuelBestScoreIndex));
				break;

			case GameFormSection.Turns:
				_startRotation = StartRotation.None;
				OnPropertyChanged(nameof(SelectedStartRotation));
				FixedTurnOrder = false;
				HasEndScore = false;
				EndScore = string.Empty;
				_endMode = EndMode.Automatic;
				OnPropertyChanged(nameof(SelectedEndMode));
				OnPropertyChanged(nameof(CanHaveEqualizingTurn));
				EqualizingTurn = false;
				break;
		}
	}

	/// <summary>An existing game or request shows the parts it uses: everything that isn't how a new game starts.</summary>
	private void ShowAddedSections()
	{
		SetAdded(GameFormSection.ScoringType, Scoring.IsChosen);
		SetAdded(GameFormSection.Results, _resultModeChosen);
		SetAdded(GameFormSection.Ties, !AllowTies);
		SetAdded(GameFormSection.Turns, _startRotation != StartRotation.None || FixedTurnOrder || HasEndScore);
	}

	/// <summary>Saving found something missing in a part: open its page and make sure the part is on it.</summary>
	private void ShowProblemIn(GameFormSection section)
	{
		SetAdded(section, true);
		CurrentPage = Sections.First(s => s.Section == section).Page;
	}

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

	/// <summary>The game's scoring type and its numbers.</summary>
	public ScoringEditor Scoring { get; } = new(ScoringTypeOption.All, "Choose scoring type");

	public ScoringType ScoringType => Scoring.ScoringType;

	// A new game starts with nothing chosen in the dropdowns ("Choose ..."); an existing game shows its choices.
	private bool _resultModeChosen;

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
		OnPropertyChanged(nameof(TieBreakScopeOptions));
		OnPropertyChanged(nameof(SelectedTieBreakScope));
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

	// Ties: allowed, or played off in a duel.
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasNoTies), nameof(HasDuelScoring))]
	private bool allowTies = true;

	/// <summary>Ties aren't allowed: show when and how they're played off.</summary>
	public bool HasNoTies => !AllowTies;

	public List<TieBreakMomentOption> TieBreakMomentOptions { get; } = TieBreakMomentOption.All;

	private TieBreakMoment _tieBreakMoment = TieBreakMoment.SubgamesAndFinal;

	public TieBreakMomentOption? SelectedTieBreakMoment
	{
		get => TieBreakMomentOptions.FirstOrDefault(o => o.Moment == _tieBreakMoment);
		set
		{
			if (value is null) return;
			_tieBreakMoment = value.Moment;
			OnPropertyChanged();
		}
	}

	/// <summary>Worded for the result type ("Only a shared loss" in a loser game).</summary>
	public List<TieBreakScopeOption> TieBreakScopeOptions => TieBreakScopeOption.AllFor(ResultMode);

	private TieBreakScope _tieBreakScope = TieBreakScope.Decisive;

	/// <summary>The null the Picker writes while its items change is ignored, so the choice isn't lost.</summary>
	public TieBreakScopeOption? SelectedTieBreakScope
	{
		get => TieBreakScopeOptions.FirstOrDefault(o => o.Scope == _tieBreakScope);
		set
		{
			if (value is null) return;
			_tieBreakScope = value.Scope;
			OnPropertyChanged();
		}
	}

	/// <summary>The duel is scored like the game; unticked, the duel gets its own scoring below.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasDuelScoring))]
	private bool duelSameScoring = true;

	public bool HasDuelScoring => !AllowTies && !DuelSameScoring;

	/// <summary>The duel's own scoring type and numbers, e.g. to 10 while the game goes to 100.</summary>
	public ScoringEditor DuelScoring { get; } = new(ScoringTypeOption.DuelOptions, "Choose the duel's scoring type", "Duel scoring: ");

	public List<string> DuelBestScoreOptions { get; } = ["Highest score wins the duel", "Lowest score wins the duel"];

	/// <summary>Null until chosen.</summary>
	private bool? _duelLowestWins;

	/// <summary>The Picker's index: 1 means the lowest duel score wins, -1 nothing chosen yet.</summary>
	public int DuelBestScoreIndex
	{
		get => _duelLowestWins switch { null => -1, true => 1, false => 0 };
		set
		{
			if (value < 0 || (value == 1) == _duelLowestWins) return;
			_duelLowestWins = value == 1;
			OnPropertyChanged();
		}
	}

	// Turns and the end of a subgame.
	public List<StartRotationOption> StartRotationOptions { get; } = StartRotationOption.All;

	private StartRotation _startRotation = StartRotation.None;

	public StartRotationOption? SelectedStartRotation
	{
		get => StartRotationOptions.FirstOrDefault(o => o.Rotation == _startRotation);
		set
		{
			if (value is null) return;
			_startRotation = value.Rotation;
			OnPropertyChanged();
		}
	}

	/// <summary>Scores are entered seat by seat; 0 has to be entered too.</summary>
	[ObservableProperty]
	private bool fixedTurnOrder;

	/// <summary>A subgame ends when a player reaches the end score.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanHaveEqualizingTurn))]
	private bool hasEndScore;

	/// <summary>Text, like the score settings; checked on save.</summary>
	[ObservableProperty]
	private string endScore = string.Empty;

	public List<EndModeOption> EndModeOptions { get; } = EndModeOption.All;

	private EndMode _endMode = EndMode.Automatic;

	public EndModeOption? SelectedEndMode
	{
		get => EndModeOptions.FirstOrDefault(o => o.Mode == _endMode);
		set
		{
			if (value is null) return;
			_endMode = value.Mode;
			OnPropertyChanged();
			OnPropertyChanged(nameof(CanHaveEqualizingTurn));
		}
	}

	/// <summary>An equalizing turn only goes with an automatic end.</summary>
	public bool CanHaveEqualizingTurn => HasEndScore && _endMode == EndMode.Automatic;

	[ObservableProperty]
	private bool equalizingTurn;

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
		ShowAddedSections();
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
		ShowAddedSections();
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
			settings = ScoringSettings.Default with
			{
				ResultMode = settings.ResultMode, LowestWins = settings.LowestWins,
				AllowTies = settings.AllowTies, TieBreakMoment = settings.TieBreakMoment, TieBreakScope = settings.TieBreakScope,
				DuelSameScoring = settings.DuelSameScoring, DuelScoringType = settings.DuelScoringType, DuelSettings = settings.DuelSettings,
				StartRotation = settings.StartRotation, FixedTurnOrder = settings.FixedTurnOrder,
				EndScore = settings.EndScore, EndMode = settings.EndMode, EqualizingTurn = settings.EqualizingTurn
			};
		}
		Scoring.Show(type, settings);
		OnPropertyChanged(nameof(ScoringType));

		_resultModeChosen = true;
		ResultMode = settings.ResultMode;
		OnPropertyChanged(nameof(SelectedResultModeOption));
		_lowestWins = settings.LowestWins;
		OnPropertyChanged(nameof(BestScoreIndex));

		AllowTies = settings.AllowTies;
		_tieBreakMoment = settings.TieBreakMoment;
		OnPropertyChanged(nameof(SelectedTieBreakMoment));
		_tieBreakScope = settings.TieBreakScope;
		OnPropertyChanged(nameof(SelectedTieBreakScope));
		DuelSameScoring = settings.DuelSameScoring;

		_startRotation = settings.StartRotation;
		OnPropertyChanged(nameof(SelectedStartRotation));
		FixedTurnOrder = settings.FixedTurnOrder;
		HasEndScore = settings.EndScore is not null;
		EndScore = settings.EndScore?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
		_endMode = settings.EndMode;
		OnPropertyChanged(nameof(SelectedEndMode));
		OnPropertyChanged(nameof(CanHaveEqualizingTurn));
		EqualizingTurn = settings.EqualizingTurn;

		if (settings.DuelSettings is { } duel)
		{
			DuelScoring.Show(settings.DuelScoringType, duel);
			_duelLowestWins = duel.LowestWins;
			OnPropertyChanged(nameof(DuelBestScoreIndex));
		}
	}

	/// <summary>
	/// The scoring settings from the form, or null with StatusMessage saying
	/// what to fix: numbers that aren't numbers, and settings that can't work
	/// together (ScoringSettings.Problem), e.g. a minimum above the starting
	/// score or amount rules that allow nothing - for the duel too when it has
	/// its own scoring. The result mode and ties apply to every scoring type;
	/// the numbers only to the plus / minus types.
	/// </summary>
	private ScoringSettings? ReadScoringSettings()
	{
		// Every dropdown needs a choice.
		if (!Scoring.IsChosen)
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

		int? endScore = null;
		if (HasEndScore)
		{
			// Some keyboards type a real minus sign (−) instead of a hyphen.
			if (!int.TryParse(EndScore.Trim().Replace('−', '-'), NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out var end))
			{
				StatusMessage = "Enter a whole number for the end score, or switch it off.";
				return null;
			}
			endScore = end;
		}

		var basis = new ScoringSettings
		{
			ResultMode = ResultMode, LowestWins = lowestWins,
			AllowTies = AllowTies, TieBreakMoment = _tieBreakMoment, TieBreakScope = _tieBreakScope,
			DuelSameScoring = DuelSameScoring,
			StartRotation = _startRotation, FixedTurnOrder = FixedTurnOrder,
			EndScore = endScore, EndMode = _endMode,
			EqualizingTurn = CanHaveEqualizingTurn && EqualizingTurn
		};
		if (Scoring.Read(basis, out var problem) is not { } settings)
		{
			StatusMessage = problem ?? string.Empty;
			return null;
		}

		if (HasDuelScoring)
		{
			if (!DuelScoring.IsChosen)
			{
				StatusMessage = "Duel scoring: choose the duel's scoring type.";
				return null;
			}
			if (_duelLowestWins is not { } duelLowestWins)
			{
				StatusMessage = "Duel scoring: choose whether the highest or the lowest score wins the duel.";
				return null;
			}
			if (DuelScoring.Read(new ScoringSettings { LowestWins = duelLowestWins }, out var duelProblem) is not { } duel)
			{
				StatusMessage = duelProblem ?? string.Empty;
				return null;
			}
			settings = settings with { DuelScoringType = DuelScoring.ScoringType, DuelSettings = duel };
		}
		else if (DuelScoring.IsChosen && _duelLowestWins is { } keptLowestWins
			&& DuelScoring.Read(new ScoringSettings { LowestWins = keptLowestWins }, out _) is { } kept
			&& kept.Problem(DuelScoring.ScoringType) is null)
		{
			// Not used now, but kept so ticking "same scoring" again later doesn't lose the duel's own scoring.
			settings = settings with { DuelScoringType = DuelScoring.ScoringType, DuelSettings = kept };
		}

		if (settings.Problem(ScoringType) is { } settingsProblem)
		{
			StatusMessage = settingsProblem;
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
			CurrentPage = GeneralPage;
			return;
		}

		if (ReadPlayers(out _, out _) is { } playersProblem)
		{
			StatusMessage = playersProblem;
			CurrentPage = GeneralPage;
			return;
		}

		if (MissingCategories() is { } missing)
		{
			StatusMessage = missing;
			CurrentPage = GeneralPage;
			return;
		}

		if (ReadScoringSettings() is null)
		{
			// Show the part that's missing a choice; other problems are in a part that's already on the scoring page.
			if (!Scoring.IsChosen)
				ShowProblemIn(GameFormSection.ScoringType);
			else if (!_resultModeChosen || _lowestWins is null)
				ShowProblemIn(GameFormSection.Results);
			else
				CurrentPage = ScoringPage;
			return;
		}

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
