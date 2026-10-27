using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;

namespace PubGames.ViewModels;

/// <summary>
/// One amount rule as the create-game form edits it: a kind from the dropdown
/// and the number(s) as typed. Turned into a ScoreRule on save.
/// </summary>
public partial class EditableScoreRule : ObservableObject
{
	/// <summary>Every kind of rule, for the picker.</summary>
	public List<ScoreRuleKindOption> KindOptions { get; } = ScoreRuleKindOption.All;

	/// <summary>Null until chosen in the dropdown ("Choose rule").</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(NeedsValue), nameof(IsBetween), nameof(ValuePlaceholder))]
	private ScoreRuleKindOption? kind;

	/// <summary>Text, so a half-typed number doesn't break the binding; checked on save.</summary>
	[ObservableProperty]
	private string value = string.Empty;

	/// <summary>Between only: the other end.</summary>
	[ObservableProperty]
	private string value2 = string.Empty;

	/// <summary>How this rule links to the one above it: OR (either may be true) or AND (both must be).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(JoinerText))]
	private bool orWithPrevious;

	/// <summary>The first rule has nothing above it, so no AND / OR.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasJoiner))]
	private bool isFirst;

	/// <summary>Whether the rule shows AND / OR (every rule but the first).</summary>
	public bool HasJoiner => !IsFirst;

	/// <summary>"AND" or "OR": how the rule links to the one above it.</summary>
	public string JoinerText => OrWithPrevious ? "OR" : "AND";

	/// <summary>Switches how this rule links to the one above it: AND or OR.</summary>
	public void ToggleJoiner() => OrWithPrevious = !OrWithPrevious;

	/// <summary>Whether the rule needs a number.</summary>
	public bool NeedsValue => Kind is not null && new ScoreRule(Kind.Kind).NeedsValue;
	/// <summary>A "between" rule: it needs two numbers.</summary>
	public bool IsBetween => Kind?.Kind == ScoreRuleKind.Between;
	/// <summary>The grey hint in the number box: "from" for between, otherwise "number".</summary>
	public string ValuePlaceholder => IsBetween ? "from" : "number";

	/// <summary>A rule from a saved game, ready to change in the editor.</summary>
	public static EditableScoreRule From(ScoreRule rule) => new()
	{
		Kind = ScoreRuleKindOption.All.First(o => o.Kind == rule.Kind),
		Value = rule.Value?.ToString(CultureInfo.CurrentCulture) ?? string.Empty,
		Value2 = rule.Value2?.ToString(CultureInfo.CurrentCulture) ?? string.Empty,
		OrWithPrevious = rule.OrWithPrevious
	};

	/// <summary>
	/// The rule, with null for numbers that aren't filled in (ScoringSettings.Problem
	/// reports those); null when no kind is chosen yet.
	/// </summary>
	public ScoreRule? ToRule()
	{
		if (Kind is null) return null;
		static int? Parse(string text) =>
			int.TryParse(text.Trim().Replace('−', '-'), NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out var n) ? n : null;
		return new ScoreRule(Kind.Kind, NeedsValue ? Parse(Value) : null, IsBetween ? Parse(Value2) : null, !IsFirst && OrWithPrevious);
	}
}
