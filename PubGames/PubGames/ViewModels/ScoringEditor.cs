using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;

namespace PubGames.ViewModels;

/// <summary>
/// The scoring part of the game form: the scoring type and its numbers (step,
/// starting score, limits, warning) and, for Manual plus / minus, the amount
/// rules. Used for the game itself and again for a tie-break duel with its own
/// scoring (ScoringEditorView shows it).
/// </summary>
public partial class ScoringEditor : ObservableObject
{
	/// <summary>Put in front of every message, e.g. "Duel scoring: ".</summary>
	private readonly string _messagePrefix;

	/// <summary>
	/// An editor for a scoring type and its numbers: with these types to choose from, this prompt in the
	/// picker, and `messagePrefix` in front of its problems (e.g. "Duel: ").
	/// </summary>
	public ScoringEditor(List<ScoringTypeOption> options, string prompt, string messagePrefix = "")
	{
		ScoringTypeOptions = options;
		ScoringTypePrompt = prompt;
		_messagePrefix = messagePrefix;
		AmountRules.CollectionChanged += OnAmountRulesChanged;
		Sections.CollectionChanged += OnSheetChanged;
	}

	/// <summary>Bound to the scoring type Picker.</summary>
	public List<ScoringTypeOption> ScoringTypeOptions { get; }

	/// <summary>The Picker's text while nothing is chosen.</summary>
	public string ScoringTypePrompt { get; }

	/// <summary>The chosen scoring type.</summary>
	[ObservableProperty]
	private ScoringType scoringType = ScoringType.PlusMinus;

	// A new game starts with nothing chosen ("Choose ..."); an existing game shows its choice.
	private bool _chosen;

	/// <summary>Whether a scoring type is chosen yet.</summary>
	public bool IsChosen => _chosen;

	/// <summary>Plus / minus: a fixed step per tap.</summary>
	public bool IsPlusMinus => _chosen && ScoringType == ScoringType.PlusMinus;

	/// <summary>Manual plus / minus: an amount typed in per tap, within the amount rules.</summary>
	public bool IsManual => _chosen && ScoringType == ScoringType.ManualPlusMinus;

	/// <summary>Score sheet: named rows per list, typed in per player, with bonuses.</summary>
	public bool IsSheet => _chosen && ScoringType == ScoringType.ScoreSheet;

	/// <summary>Both plus / minus types share the start score, limits and warning.</summary>
	public bool HasScoreSettings => IsPlusMinus || IsManual;

	public ScoringTypeOption? SelectedScoringOption
	{
		get => _chosen ? ScoringTypeOptions.FirstOrDefault(o => o.Type == ScoringType) : null;
		set
		{
			if (value is null) return;
			_chosen = true;
			ScoringType = value.Type;
			// A new score sheet starts with one empty list to fill in.
			if (value.Type == ScoringType.ScoreSheet && Sections.Count == 0)
				AddSection();
			NotifyTypeChanged();
		}
	}

	/// <summary>The scoring type changed: the fields that belong to it show or hide.</summary>
	private void NotifyTypeChanged()
	{
		OnPropertyChanged(nameof(SelectedScoringOption));
		OnPropertyChanged(nameof(IsChosen));
		OnPropertyChanged(nameof(IsPlusMinus));
		OnPropertyChanged(nameof(IsManual));
		OnPropertyChanged(nameof(IsSheet));
		OnPropertyChanged(nameof(HasScoreSettings));
	}

	/// <summary>Score sheet: its lists, top to bottom.</summary>
	public ObservableCollection<EditableSheetSection> Sections { get; } = new();

	/// <summary>Adds an empty list with one empty row.</summary>
	public void AddSection()
	{
		var section = new EditableSheetSection();
		section.Rows.Add(new EditableSheetRow());
		Sections.Add(section);
	}

	/// <summary>Removes a list with its rows and bonuses.</summary>
	public void RemoveSection(EditableSheetSection section) => Sections.Remove(section);

	/// <summary>Adds an empty row at the bottom of a list.</summary>
	public static void AddRow(EditableSheetSection section) => section.Rows.Add(new EditableSheetRow());

	/// <summary>Removes a row from whichever list it's in.</summary>
	public void RemoveRow(EditableSheetRow row)
	{
		foreach (var section in Sections)
			section.Rows.Remove(row);
	}

	/// <summary>Adds an empty rule to a row.</summary>
	public static void AddRowRule(EditableSheetRow row) => row.Rules.Add(new EditableScoreRule());

	/// <summary>Removes a rule from whichever row it's in (rows' rules, not the amount rules).</summary>
	public void RemoveRowRule(EditableScoreRule rule)
	{
		foreach (var row in Sections.SelectMany(s => s.Rows))
			row.Rules.Remove(rule);
	}

	/// <summary>Adds a bonus to a list: automatic at a total unless changed.</summary>
	public static void AddBonus(EditableSheetSection section) => section.Bonuses.Add(new EditableSheetBonus());

	/// <summary>Removes a bonus from whichever list it's in.</summary>
	public void RemoveBonus(EditableSheetBonus bonus)
	{
		foreach (var section in Sections)
			section.Bonuses.Remove(bonus);
	}

	/// <summary>
	/// A list, row or bonus was added or removed, or a row changed: follow the
	/// new ones too, and give every bonus the rows it can need.
	/// </summary>
	private void OnSheetChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		foreach (var item in e.NewItems ?? Array.Empty<object>())
			switch (item)
			{
				case EditableSheetSection section:
					section.Rows.CollectionChanged += OnSheetChanged;
					section.Bonuses.CollectionChanged += OnSheetChanged;
					foreach (var row in section.Rows) row.PropertyChanged += OnSheetRowChanged;
					break;
				case EditableSheetRow row:
					row.PropertyChanged += OnSheetRowChanged;
					break;
			}
		foreach (var item in e.OldItems ?? Array.Empty<object>())
			switch (item)
			{
				case EditableSheetSection section:
					section.Rows.CollectionChanged -= OnSheetChanged;
					section.Bonuses.CollectionChanged -= OnSheetChanged;
					foreach (var row in section.Rows) row.PropertyChanged -= OnSheetRowChanged;
					break;
				case EditableSheetRow row:
					row.PropertyChanged -= OnSheetRowChanged;
					break;
			}
		// Only removing can take away a bonus's row; while lists are being added, its row may still come.
		RefreshRowChoices(dropMissing: e.OldItems is { Count: > 0 });
	}

	/// <summary>A row's name or fixed score changed: the bonuses' row dropdowns show it.</summary>
	private void OnSheetRowChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(EditableSheetRow.Name) or nameof(EditableSheetRow.FixedScore) or nameof(EditableSheetRow.IsFixed))
			RefreshRowChoices(dropMissing: false);
	}

	/// <summary>Gives every bonus the sheet's rows to choose from; with `dropMissing` a bonus whose row is gone is always possible again.</summary>
	private void RefreshRowChoices(bool dropMissing)
	{
		var choices = new List<SheetRowChoice> { new(string.Empty, L.T("Always possible")) };
		foreach (var section in Sections)
			foreach (var row in section.Rows)
				choices.Add(new(row.Id, L.F("Only with a score in: {0}", row.Title.Length > 0 ? row.Title : L.T("(row without a name)"))));
		foreach (var bonus in Sections.SelectMany(s => s.Bonuses))
		{
			if (dropMissing && bonus.RequiresRowId is { } id && choices.All(c => c.Id != id))
				bonus.RequiredRow = choices[0];
			bonus.RowChoices = choices;
		}
	}

	/// <summary>Shows saved lists in the editor.</summary>
	private void ShowSheet(IEnumerable<ScoreSheetSection> sections)
	{
		while (Sections.Count > 0)
			Sections.RemoveAt(0);
		foreach (var section in sections)
			Sections.Add(EditableSheetSection.From(section));
	}

	/// <summary>Manual plus / minus: the rules for typed amounts, linked with AND / OR.</summary>
	public ObservableCollection<EditableScoreRule> AmountRules { get; } = new();

	/// <summary>Adds an empty rule for entered amounts.</summary>
	public void AddAmountRule() => AmountRules.Add(new EditableScoreRule());

	/// <summary>Removes a rule for entered amounts.</summary>
	public void RemoveAmountRule(EditableScoreRule rule) => AmountRules.Remove(rule);

	/// <summary>Switches how a rule links to the one above it: AND or OR.</summary>
	public void ToggleJoiner(EditableScoreRule rule) => rule.ToggleJoiner();

	/// <summary>The rules as one sentence with brackets, so it's clear how AND and OR combine.</summary>
	public string AmountRulesPreview
	{
		get
		{
			if (AmountRules.Count == 0) return L.T("No rules: any amount can be entered.");
			if (AmountRules.Any(r => r.Kind is null))
				return L.T("Choose what each rule checks to see what's allowed.");
			var rules = AmountRules.Select(r => r.ToRule()!).ToList();
			if (rules.Any(r => r.NeedsValue && r.Value is null || r.NeedsSecondValue && r.Value2 is null))
				return L.T("Fill in the numbers to see what's allowed.");
			return L.F("Allowed: amounts that are {0}.", AmountRuleSet.Describe(rules));
		}
	}

	/// <summary>
	/// A rule was added or removed: the preview follows changes in every rule, and only the first rule has no
	/// AND / OR.
	/// </summary>
	private void OnAmountRulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		foreach (EditableScoreRule rule in e.NewItems ?? Array.Empty<EditableScoreRule>())
			rule.PropertyChanged += OnAmountRuleChanged;
		foreach (EditableScoreRule rule in e.OldItems ?? Array.Empty<EditableScoreRule>())
			rule.PropertyChanged -= OnAmountRuleChanged;
		for (var ruleIndex = 0; ruleIndex < AmountRules.Count; ruleIndex++)
			AmountRules[ruleIndex].IsFirst = ruleIndex == 0;
		OnPropertyChanged(nameof(AmountRulesPreview));
	}

	/// <summary>A rule changed: the preview of what's allowed updates.</summary>
	private void OnAmountRuleChanged(object? sender, PropertyChangedEventArgs e) =>
		OnPropertyChanged(nameof(AmountRulesPreview));

	// Text rather than numbers, so a half-typed "-" or an empty box doesn't break the binding;
	// they're checked on save (see Read).
	[ObservableProperty]
	private string scoreStep = "1";

	/// <summary>The starting score, as typed.</summary>
	[ObservableProperty]
	private string startScore = "0";

	/// <summary>Whether scores have a maximum.</summary>
	[ObservableProperty]
	private bool hasMaxScore;

	/// <summary>The maximum score, as typed.</summary>
	[ObservableProperty]
	private string maxScore = string.Empty;

	/// <summary>Whether scores have a minimum.</summary>
	[ObservableProperty]
	private bool hasMinScore;

	/// <summary>The minimum score, as typed.</summary>
	[ObservableProperty]
	private string minScore = string.Empty;

	/// <summary>Whether there's a warning score.</summary>
	[ObservableProperty]
	private bool hasWarningScore;

	/// <summary>The warning score, as typed.</summary>
	[ObservableProperty]
	private string warningScore = string.Empty;

	/// <summary>Shows a saved scoring type and its numbers.</summary>
	public void Show(ScoringType type, ScoringSettings settings)
	{
		_chosen = true;
		ScoringType = type;
		NotifyTypeChanged();

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
		ShowSheet(settings.Sheet);
	}

	/// <summary>Everything filled in here as one text, to see whether the form changed.</summary>
	public string Snapshot() => string.Join("\u001F",
		_chosen, ScoringType, ScoreStep, StartScore, HasMaxScore, MaxScore, HasMinScore, MinScore, HasWarningScore, WarningScore,
		string.Join(";", AmountRules.Select(r => $"{r.Kind?.Kind}:{r.Value}:{r.Value2}:{r.OrWithPrevious}")),
		string.Join(";", Sections.Select(SheetSnapshot)));

	/// <summary>A list as one text, for Snapshot: also numbers that are typed but not valid yet.</summary>
	private static string SheetSnapshot(EditableSheetSection section) => string.Join("|",
		section.Name,
		string.Join(",", section.Rows.Select(row => $"{row.Name}:{row.IsFixed}:{row.FixedScore}:{row.AllowsZero}[{string.Join(",", row.Rules.Select(r => $"{r.Kind?.Kind}:{r.Value}:{r.Value2}:{r.OrWithPrevious}"))}]")),
		string.Join(",", section.Bonuses.Select(b => $"{b.Name}:{b.Kind.Kind}:{b.Threshold}:{b.Points}:{b.MaxTimes}:{b.RequiresRowId}")));

	/// <summary>Takes over another editor's scoring type, numbers and amount rules (the duel copying the game's).</summary>
	public void CopyFrom(ScoringEditor other)
	{
		// A duel can't be played on a score sheet: its score is typed in instead.
		var type = other.ScoringType == ScoringType.ScoreSheet && ScoringTypeOptions.Any(o => o.Type == ScoringType.TypedScore)
			? ScoringType.TypedScore : other.ScoringType;
		_chosen = other._chosen && ScoringTypeOptions.Any(o => o.Type == type);
		ScoringType = type;
		NotifyTypeChanged();
		ScoreStep = other.ScoreStep;
		StartScore = other.StartScore;
		HasMaxScore = other.HasMaxScore;
		MaxScore = other.MaxScore;
		HasMinScore = other.HasMinScore;
		MinScore = other.MinScore;
		HasWarningScore = other.HasWarningScore;
		WarningScore = other.WarningScore;
		while (AmountRules.Count > 0)
			AmountRules.RemoveAt(0); // not Clear(): that doesn't say which rules left
		foreach (var rule in other.AmountRules)
			AmountRules.Add(new EditableScoreRule { Kind = rule.Kind, Value = rule.Value, Value2 = rule.Value2, OrWithPrevious = rule.OrWithPrevious });
	}

	/// <summary>Back to how a new game starts: nothing chosen, default numbers, no amount rules.</summary>
	public void Reset()
	{
		Show(ScoringType.PlusMinus, ScoringSettings.Default);
		_chosen = false;
		NotifyTypeChanged();
	}

	/// <summary>
	/// The form's numbers filled into `settings`, or null with `problem` saying
	/// what to fix: no scoring type chosen, a rule without a kind, or numbers
	/// that aren't numbers. Whether the numbers work together is checked by
	/// ScoringSettings.Problem.
	/// </summary>
	public ScoringSettings? Read(ScoringSettings settings, out string? problem)
	{
		problem = null;
		string? error = null;
		int? Number(string text, string what)
		{
			// Some keyboards type a real minus sign (−) instead of a hyphen.
			if (int.TryParse(text.Trim().Replace('−', '-'), NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out var value))
				return value;
			error = L.F("Enter a whole number for the {0}.", what);
			return null;
		}

		if (!_chosen)
		{
			problem = $"{_messagePrefix}{ScoringTypePrompt}.";
			return null;
		}
		if (IsManual && AmountRules.Select((r, i) => (r, i)).FirstOrDefault(x => x.r.Kind is null) is { r: not null } missing)
		{
			problem = L.F("{0}Rule {1}: choose what it checks.", _messagePrefix, missing.i + 1);
			return null;
		}
		if (IsSheet)
			return ReadSheet(settings, out problem);
		if (!HasScoreSettings) return settings;

		var step = 1;
		int? start = null, max = null, min = null, warning = null;
		if (IsPlusMinus && Number(ScoreStep, "step") is { } typedStep) step = typedStep;
		if (error is null) start = Number(StartScore, L.T("starting score"));
		if (error is null && HasMaxScore) max = Number(MaxScore, L.T("maximum score"));
		if (error is null && HasMinScore) min = Number(MinScore, L.T("minimum score"));
		if (error is null && HasWarningScore) warning = Number(WarningScore, "warning");
		if (error is not null)
		{
			problem = _messagePrefix + error;
			return null;
		}

		return settings with
		{
			Step = step, StartScore = start ?? 0, MaxScore = max, MinScore = min, WarningScore = warning,
			AmountRules = IsManual ? AmountRules.Select(r => r.ToRule()!).ToList() : []
		};
	}

	/// <summary>
	/// Score sheet: the lists filled into `settings`, or null with `problem`
	/// saying what to fix: a rule without a kind, or a bonus number that isn't a
	/// whole number. Missing names and numbers are checked by ScoreSheet.Problem.
	/// </summary>
	private ScoringSettings? ReadSheet(ScoringSettings settings, out string? problem)
	{
		problem = null;
		for (var sectionIndex = 0; sectionIndex < Sections.Count; sectionIndex++)
		{
			var section = Sections[sectionIndex];
			var list = section.Name.Trim().Length > 0 ? section.Name.Trim() : L.F("List {0}", sectionIndex + 1);
			foreach (var row in section.Rows)
			{
				if (row.IsFixed)
				{
					if (EditableSheetBonus.Parse(row.FixedScore) is null)
					{
						problem = L.F("{0}{1}, {2}: enter the fixed score as a whole number.", _messagePrefix, list, row.Name.Trim());
						return null;
					}
					continue;
				}
				if (row.Rules.Select((r, i) => (r, i)).FirstOrDefault(x => x.r.Kind is null) is { r: not null } missing)
				{
					problem = L.F("{0}{1}, {2}: rule {3}: choose what it checks.", _messagePrefix, list, row.Name.Trim(), missing.i + 1);
					return null;
				}
			}
			foreach (var bonus in section.Bonuses)
			{
				var typed = new[] { bonus.Points, bonus.IsTotalAtLeast ? bonus.Threshold : string.Empty, bonus.IsPerTime ? bonus.MaxTimes : string.Empty };
				if (typed.Any(text => text.Trim().Length > 0 && EditableSheetBonus.Parse(text) is null))
				{
					problem = L.F("{0}{1}, {2}: enter whole numbers for the bonus.", _messagePrefix, list, bonus.Name.Trim());
					return null;
				}
			}
		}
		// The numbers of the plus / minus types don't apply: a sheet starts at 0, without limits.
		return settings with
		{
			Step = 1, StartScore = 0, MaxScore = null, MinScore = null, WarningScore = null, AmountRules = [],
			Sheet = Sections.Select(s => s.ToSection()).ToList()
		};
	}
}
