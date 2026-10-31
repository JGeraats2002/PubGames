using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;

namespace PubGames.ViewModels;

/// <summary>
/// One list of a score sheet as the create-game form edits it: its name, its
/// rows and its bonuses. Turned into a ScoreSheetSection on save.
/// </summary>
public partial class EditableSheetSection : ObservableObject
{
	/// <summary>Kept from the saved list, so filled-in scores keep their place.</summary>
	public string Id { get; init; } = ScoreSheet.NewId();

	/// <summary>The list's name, as typed.</summary>
	[ObservableProperty]
	private string name = string.Empty;

	/// <summary>The rows players fill in.</summary>
	public ObservableCollection<EditableSheetRow> Rows { get; } = new();

	/// <summary>The bonuses under the rows.</summary>
	public ObservableCollection<EditableSheetBonus> Bonuses { get; } = new();

	/// <summary>A list from a saved game, ready to change in the editor.</summary>
	public static EditableSheetSection From(ScoreSheetSection section)
	{
		var editable = new EditableSheetSection { Id = section.Id, Name = section.Name };
		foreach (var row in section.Rows)
			editable.Rows.Add(EditableSheetRow.From(row));
		foreach (var bonus in section.Bonuses)
			editable.Bonuses.Add(EditableSheetBonus.From(bonus));
		return editable;
	}

	/// <summary>The list as it's saved; numbers that aren't filled in are null (ScoreSheet.Problem reports them).</summary>
	public ScoreSheetSection ToSection() => new()
	{
		Id = Id,
		Name = Name.Trim(),
		Rows = Rows.Select(r => r.ToRow()).ToList(),
		Bonuses = Bonuses.Select(b => b.ToBonus()).ToList()
	};
}

/// <summary>One row of a score sheet as the form edits it: a name and the rules for the score in it.</summary>
public partial class EditableSheetRow : ObservableObject
{
	/// <summary>Builds an empty row; the first rule never shows AND / OR.</summary>
	public EditableSheetRow()
	{
		Rules.CollectionChanged += OnRulesChanged;
	}

	/// <summary>Kept from the saved row, so filled-in scores keep their place.</summary>
	public string Id { get; init; } = ScoreSheet.NewId();

	/// <summary>The row's name, as typed.</summary>
	[ObservableProperty]
	private string name = string.Empty;

	/// <summary>The row has a fixed score that players tick off, instead of typing a score.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsTyped), nameof(RulesPreview))]
	private bool isFixed;

	/// <summary>The score is typed in, within the row's rules.</summary>
	public bool IsTyped => !IsFixed;

	/// <summary>The fixed score, as typed.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(RulesPreview))]
	private string fixedScore = string.Empty;

	/// <summary>0 may be filled in (or ticked) when a throw failed.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(RulesPreview))]
	private bool allowsZero = true;

	/// <summary>The rules for the score in this row, linked with AND / OR.</summary>
	public ObservableCollection<EditableScoreRule> Rules { get; } = new();

	/// <summary>"Allowed: 0, or scores that are between 1 and 5." under the row's rules.</summary>
	public string RulesPreview
	{
		get
		{
			if (IsFixed)
				return EditableSheetBonus.Parse(FixedScore) is { } score
					? AllowsZero ? L.F("Players tick off {0}, or 0 when the throw failed.", score) : L.F("Players tick off {0}.", score)
					: L.T("Fill in the fixed score.");
			if (Rules.Count == 0) return AllowsZero ? L.T("No rules: any score.") : L.T("No rules: any score except 0.");
			if (Rules.Any(r => r.Kind is null))
				return L.T("Choose what each rule checks to see what's allowed.");
			var rules = Rules.Select(r => r.ToRule()!).ToList();
			if (rules.Any(r => r.NeedsValue && r.Value is null || r.NeedsSecondValue && r.Value2 is null))
				return L.T("Fill in the numbers to see what's allowed.");
			return AllowsZero
				? L.F("Allowed: 0, or scores that are {0}.", AmountRuleSet.Describe(rules))
				: L.F("Allowed: scores that are {0}, not 0.", AmountRuleSet.Describe(rules));
		}
	}

	/// <summary>"Full house (25)" when the fixed score is filled in, for the bonus's row picker.</summary>
	public string Title => IsFixed && EditableSheetBonus.Parse(FixedScore) is { } score ? $"{Name.Trim()} ({score})" : Name.Trim();

	/// <summary>A rule was added or removed: only the first has no AND / OR, and the preview follows every rule.</summary>
	private void OnRulesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		foreach (EditableScoreRule rule in e.NewItems ?? Array.Empty<EditableScoreRule>())
			rule.PropertyChanged += OnRuleChanged;
		foreach (EditableScoreRule rule in e.OldItems ?? Array.Empty<EditableScoreRule>())
			rule.PropertyChanged -= OnRuleChanged;
		for (var ruleIndex = 0; ruleIndex < Rules.Count; ruleIndex++)
			Rules[ruleIndex].IsFirst = ruleIndex == 0;
		OnPropertyChanged(nameof(RulesPreview));
	}

	/// <summary>A rule changed: the preview of what's allowed updates.</summary>
	private void OnRuleChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(RulesPreview));

	/// <summary>A row from a saved game, ready to change in the editor.</summary>
	public static EditableSheetRow From(ScoreSheetRow row)
	{
		var editable = new EditableSheetRow
		{
			Id = row.Id,
			Name = row.Name,
			IsFixed = row.FixedScore is not null,
			FixedScore = row.FixedScore?.ToString(CultureInfo.CurrentCulture) ?? string.Empty,
			AllowsZero = row.AllowsZero
		};
		foreach (var rule in row.Rules)
			editable.Rules.Add(EditableScoreRule.From(rule));
		return editable;
	}

	/// <summary>
	/// The row as it's saved. Rules without a kind are left out (the form stops those first);
	/// a fixed score that isn't filled in becomes 0, which ScoreSheet.Problem reports.
	/// </summary>
	public ScoreSheetRow ToRow() => new()
	{
		Id = Id,
		Name = Name.Trim(),
		Rules = IsFixed ? [] : Rules.Select(r => r.ToRule()).OfType<ScoreRule>().ToList(),
		FixedScore = IsFixed ? EditableSheetBonus.Parse(FixedScore) ?? 0 : null,
		AllowsZero = AllowsZero
	};
}

/// <summary>A kind of bonus as the dropdown shows it.</summary>
public sealed record ScoreSheetBonusKindOption(ScoreSheetBonusKind Kind)
{
	/// <summary>The Picker shows this.</summary>
	public override string ToString() => Kind == ScoreSheetBonusKind.TotalAtLeast
		? L.T("Automatic, at a total")
		: L.T("Per time, filled in");

	/// <summary>Both kinds, for the picker.</summary>
	public static List<ScoreSheetBonusKindOption> All { get; } =
		[new(ScoreSheetBonusKind.TotalAtLeast), new(ScoreSheetBonusKind.PerTime)];
}

/// <summary>A row a bonus can need, as the bonus's dropdown shows it; Id "" is no row (always possible).</summary>
public sealed record SheetRowChoice(string Id, string Label)
{
	/// <summary>The Picker shows this.</summary>
	public override string ToString() => Label;
}

/// <summary>One bonus of a list as the form edits it; the numbers as typed.</summary>
public partial class EditableSheetBonus : ObservableObject
{
	/// <summary>The row the bonus needs a score in, or null: always possible.</summary>
	public string? RequiresRowId { get; private set; }

	/// <summary>Every row of the sheet, after "always possible"; ScoringEditor keeps it up to date.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(RequiredRow))]
	private List<SheetRowChoice> rowChoices = [];

	/// <summary>
	/// The chosen row in the dropdown. The dropdown clears it while its list is
	/// replaced (a row was renamed); that's ignored, so the choice stays.
	/// </summary>
	public SheetRowChoice? RequiredRow
	{
		get => RowChoices.FirstOrDefault(c => c.Id == (RequiresRowId ?? string.Empty)) ?? RowChoices.FirstOrDefault();
		set
		{
			if (value is null) return;
			var id = value.Id.Length == 0 ? null : value.Id;
			if (id == RequiresRowId) return;
			RequiresRowId = id;
			OnPropertyChanged();
		}
	}

	/// <summary>Kept from the saved bonus, so filled-in bonuses keep their place.</summary>
	public string Id { get; init; } = ScoreSheet.NewId();

	/// <summary>Both kinds of bonus, for the picker.</summary>
	public List<ScoreSheetBonusKindOption> KindOptions { get; } = ScoreSheetBonusKindOption.All;

	/// <summary>The bonus's name, as typed.</summary>
	[ObservableProperty]
	private string name = string.Empty;

	/// <summary>Automatic at a total, or filled in per time.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsTotalAtLeast), nameof(IsPerTime))]
	private ScoreSheetBonusKindOption kind = ScoreSheetBonusKindOption.All[0];

	/// <summary>The bonus is added by itself at a total.</summary>
	public bool IsTotalAtLeast => Kind.Kind == ScoreSheetBonusKind.TotalAtLeast;

	/// <summary>The players fill the bonus in, per time.</summary>
	public bool IsPerTime => Kind.Kind == ScoreSheetBonusKind.PerTime;

	/// <summary>Automatic: the total of the list's rows that gives the bonus, as typed.</summary>
	[ObservableProperty]
	private string threshold = string.Empty;

	/// <summary>The bonus points (once, or per time), as typed.</summary>
	[ObservableProperty]
	private string points = string.Empty;

	/// <summary>Per time: the most times, as typed; empty is no limit.</summary>
	[ObservableProperty]
	private string maxTimes = string.Empty;

	/// <summary>A bonus from a saved game, ready to change in the editor.</summary>
	public static EditableSheetBonus From(ScoreSheetBonus bonus) => new()
	{
		Id = bonus.Id,
		Name = bonus.Name,
		Kind = ScoreSheetBonusKindOption.All.First(o => o.Kind == bonus.Kind),
		Threshold = bonus.Threshold?.ToString(CultureInfo.CurrentCulture) ?? string.Empty,
		Points = bonus.Points.ToString(CultureInfo.CurrentCulture),
		MaxTimes = bonus.MaxTimes?.ToString(CultureInfo.CurrentCulture) ?? string.Empty,
		RequiresRowId = bonus.RequiresRowId
	};

	/// <summary>A typed whole number, or null when it isn't one.</summary>
	public static int? Parse(string text) =>
		int.TryParse(text.Trim().Replace('−', '-'), NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out var number) ? number : null;

	/// <summary>The bonus as it's saved; numbers that aren't filled in are null or 0 (ScoreSheet.Problem reports them).</summary>
	public ScoreSheetBonus ToBonus() => new()
	{
		Id = Id,
		Name = Name.Trim(),
		Kind = Kind.Kind,
		Threshold = IsTotalAtLeast ? Parse(Threshold) : null,
		Points = Parse(Points) ?? 0,
		MaxTimes = IsPerTime ? Parse(MaxTimes) : null,
		RequiresRowId = RequiresRowId
	};
}
