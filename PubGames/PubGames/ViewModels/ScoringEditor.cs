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

	public ScoringEditor(List<ScoringTypeOption> options, string prompt, string messagePrefix = "")
	{
		ScoringTypeOptions = options;
		ScoringTypePrompt = prompt;
		_messagePrefix = messagePrefix;
		AmountRules.CollectionChanged += OnAmountRulesChanged;
	}

	/// <summary>Bound to the scoring type Picker.</summary>
	public List<ScoringTypeOption> ScoringTypeOptions { get; }

	/// <summary>The Picker's text while nothing is chosen.</summary>
	public string ScoringTypePrompt { get; }

	[ObservableProperty]
	private ScoringType scoringType = ScoringType.PlusMinus;

	// A new game starts with nothing chosen ("Choose ..."); an existing game shows its choice.
	private bool _chosen;

	public bool IsChosen => _chosen;

	/// <summary>Plus / minus: a fixed step per tap.</summary>
	public bool IsPlusMinus => _chosen && ScoringType == ScoringType.PlusMinus;

	/// <summary>Manual plus / minus: an amount typed in per tap, within the amount rules.</summary>
	public bool IsManual => _chosen && ScoringType == ScoringType.ManualPlusMinus;

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
			NotifyTypeChanged();
		}
	}

	private void NotifyTypeChanged()
	{
		OnPropertyChanged(nameof(SelectedScoringOption));
		OnPropertyChanged(nameof(IsChosen));
		OnPropertyChanged(nameof(IsPlusMinus));
		OnPropertyChanged(nameof(IsManual));
		OnPropertyChanged(nameof(HasScoreSettings));
	}

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

	private void OnAmountRulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		foreach (EditableScoreRule rule in e.NewItems ?? Array.Empty<EditableScoreRule>())
			rule.PropertyChanged += OnAmountRuleChanged;
		foreach (EditableScoreRule rule in e.OldItems ?? Array.Empty<EditableScoreRule>())
			rule.PropertyChanged -= OnAmountRuleChanged;
		for (var i = 0; i < AmountRules.Count; i++)
			AmountRules[i].IsFirst = i == 0;
		OnPropertyChanged(nameof(AmountRulesPreview));
	}

	private void OnAmountRuleChanged(object? sender, PropertyChangedEventArgs e) =>
		OnPropertyChanged(nameof(AmountRulesPreview));

	// Text rather than numbers, so a half-typed "-" or an empty box doesn't break the binding;
	// they're checked on save (see Read).
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
	}

	/// <summary>Everything filled in here as one text, to see whether the form changed.</summary>
	public string Snapshot() => string.Join("\u001F",
		_chosen, ScoringType, ScoreStep, StartScore, HasMaxScore, MaxScore, HasMinScore, MinScore, HasWarningScore, WarningScore,
		string.Join(";", AmountRules.Select(r => $"{r.Kind?.Kind}:{r.Value}:{r.Value2}:{r.OrWithPrevious}")));

	/// <summary>Takes over another editor's scoring type, numbers and amount rules (the duel copying the game's).</summary>
	public void CopyFrom(ScoringEditor other)
	{
		_chosen = other._chosen && ScoringTypeOptions.Any(o => o.Type == other.ScoringType);
		ScoringType = other.ScoringType;
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
			error = $"Enter a whole number for the {what}.";
			return null;
		}

		if (!_chosen)
		{
			problem = $"{_messagePrefix}{ScoringTypePrompt}.";
			return null;
		}
		if (IsManual && AmountRules.Select((r, i) => (r, i)).FirstOrDefault(x => x.r.Kind is null) is { r: not null } missing)
		{
			problem = $"{_messagePrefix}Rule {missing.i + 1}: choose what it checks.";
			return null;
		}
		if (!HasScoreSettings) return settings;

		var step = 1;
		int? start = null, max = null, min = null, warning = null;
		if (IsPlusMinus && Number(ScoreStep, "step") is { } typedStep) step = typedStep;
		if (error is null) start = Number(StartScore, "starting score");
		if (error is null && HasMaxScore) max = Number(MaxScore, "maximum score");
		if (error is null && HasMinScore) min = Number(MinScore, "minimum score");
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
}
