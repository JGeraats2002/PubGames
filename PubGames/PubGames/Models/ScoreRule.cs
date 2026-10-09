namespace PubGames.Models;

/// <summary>Stored as a number, so new kinds go at the end.</summary>
public enum ScoreRuleKind
{
	HigherThan,
	LowerThan,
	Even,
	Odd,
	Between,
	DivisibleBy,
	HigherOrEqual,
	LowerOrEqual
}

/// <summary>
/// A rule for the amounts players type in with Manual plus / minus, e.g.
/// "lower than 11" or "divisible by 5". Higher/lower than are strict, the
/// "or equal to" kinds include the number, between includes both numbers.
/// OrWithPrevious links it to the rule above with OR instead of AND; see
/// AmountRuleSet for how the rules combine.
/// </summary>
public sealed record ScoreRule(ScoreRuleKind Kind, int? Value = null, int? Value2 = null, bool OrWithPrevious = false)
{
	public bool NeedsValue => Kind is not (ScoreRuleKind.Even or ScoreRuleKind.Odd);

	public bool NeedsSecondValue => Kind == ScoreRuleKind.Between;

	public bool Allows(int amount) => Kind switch
	{
		ScoreRuleKind.HigherThan => Value is { } v && amount > v,
		ScoreRuleKind.HigherOrEqual => Value is { } v && amount >= v,
		ScoreRuleKind.LowerThan => Value is { } v && amount < v,
		ScoreRuleKind.LowerOrEqual => Value is { } v && amount <= v,
		ScoreRuleKind.Even => amount % 2 == 0,
		ScoreRuleKind.Odd => amount % 2 != 0,
		ScoreRuleKind.Between => Value is { } a && Value2 is { } b && amount >= Math.Min(a, b) && amount <= Math.Max(a, b),
		ScoreRuleKind.DivisibleBy => Value is > 0 and var d && amount % d == 0,
		_ => true
	};

	/// <summary>"higher than 3", "between 0 and 10", "even", ...</summary>
	public string Describe() => Kind switch
	{
		ScoreRuleKind.HigherThan => $"higher than {Value}",
		ScoreRuleKind.HigherOrEqual => $"higher than or equal to {Value}",
		ScoreRuleKind.LowerThan => $"lower than {Value}",
		ScoreRuleKind.LowerOrEqual => $"lower than or equal to {Value}",
		ScoreRuleKind.Even => "even",
		ScoreRuleKind.Odd => "not even",
		ScoreRuleKind.Between => $"between {Value} and {Value2}",
		_ => $"divisible by {Value}"
	};

	/// <summary>Why this rule can't be used on its own (a number missing, dividing by 0), or null.</summary>
	public string? Problem(int number)
	{
		var name = ScoreRuleKindOption.TextFor(Kind);
		if (NeedsValue && Value is null || NeedsSecondValue && Value2 is null)
			return $"Rule {number} ({name}): fill in {(NeedsSecondValue ? "both numbers" : "the number")}.";
		if (Kind == ScoreRuleKind.DivisibleBy && Value < 1)
			return $"Rule {number} ({name}): the number must be 1 or more.";
		return null;
	}
}

/// <summary>
/// How a list of amount rules combines. OR binds first: rules linked with OR
/// form a group in which one rule must be true, and every group (split by
/// AND) must be true. So "divisible by 50 AND ≥ 350 OR &lt; 0" means
/// divisible by 50 and (≥ 350 or &lt; 0). No rules: every amount is allowed.
/// </summary>
public static class AmountRuleSet
{
	public static List<List<ScoreRule>> Groups(IReadOnlyList<ScoreRule> rules)
	{
		var groups = new List<List<ScoreRule>>();
		foreach (var rule in rules)
		{
			if (rule.OrWithPrevious && groups.Count > 0)
				groups[^1].Add(rule);
			else
				groups.Add([rule]);
		}
		return groups;
	}

	/// <summary>A check for many amounts in a row (the groups are worked out once).</summary>
	public static Func<int, bool> Predicate(IReadOnlyList<ScoreRule> rules)
	{
		var groups = Groups(rules);
		return amount => groups.All(g => g.Any(r => r.Allows(amount)));
	}

	public static bool Allows(IReadOnlyList<ScoreRule> rules, int amount) => Predicate(rules)(amount);

	/// <summary>"divisible by 50 and (higher than or equal to 350 or lower than 0)".</summary>
	public static string Describe(IReadOnlyList<ScoreRule> rules)
	{
		var groups = Groups(rules);
		return string.Join(" and ", groups.Select(g =>
		{
			var text = string.Join(" or ", g.Select(r => r.Describe()));
			return g.Count > 1 && groups.Count > 1 ? $"({text})" : text;
		}));
	}

	/// <summary>The largest number in any rule: further from 0 than this, the rules only repeat.</summary>
	public static int Reach(IReadOnlyList<ScoreRule> rules) => rules
		.SelectMany(r => new[] { r.Value, r.Value2 })
		.Where(v => v is not null)
		.Select(v => Math.Abs((long)v!.Value))
		.DefaultIfEmpty(0)
		.Select(v => (int)Math.Min(v, 1_000_000))
		.Max();
}

/// <summary>A rule kind as the dropdown shows it: one symbol and its text.</summary>
public sealed record ScoreRuleKindOption(ScoreRuleKind Kind)
{
	public string Label => $"{SymbolFor(Kind)} {TextFor(Kind)}";

	/// <summary>The Picker shows this.</summary>
	public override string ToString() => Label;

	public static List<ScoreRuleKindOption> All { get; } = new[]
	{
		ScoreRuleKind.HigherThan, ScoreRuleKind.HigherOrEqual, ScoreRuleKind.LowerThan, ScoreRuleKind.LowerOrEqual,
		ScoreRuleKind.Even, ScoreRuleKind.Odd, ScoreRuleKind.Between, ScoreRuleKind.DivisibleBy
	}.Select(k => new ScoreRuleKindOption(k)).ToList();

	public static string SymbolFor(ScoreRuleKind kind) => kind switch
	{
		ScoreRuleKind.HigherThan => ">",
		ScoreRuleKind.HigherOrEqual => "≥",
		ScoreRuleKind.LowerThan => "<",
		ScoreRuleKind.LowerOrEqual => "≤",
		ScoreRuleKind.Even => "●",
		ScoreRuleKind.Odd => "○",
		ScoreRuleKind.Between => "↔",
		_ => "÷"
	};

	public static string TextFor(ScoreRuleKind kind) => kind switch
	{
		ScoreRuleKind.HigherThan => "Higher than",
		ScoreRuleKind.HigherOrEqual => "Higher than or equal to",
		ScoreRuleKind.LowerThan => "Lower than",
		ScoreRuleKind.LowerOrEqual => "Lower than or equal to",
		ScoreRuleKind.Even => "Even",
		ScoreRuleKind.Odd => "Not even",
		ScoreRuleKind.Between => "Between",
		_ => "Divisible by"
	};
}
