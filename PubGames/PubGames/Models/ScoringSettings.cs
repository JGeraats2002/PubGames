using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PubGames.Models;

/// <summary>
/// What an admin sets up for a game's scoring. ResultMode applies to every
/// scoring type; StartScore to WarningScore to both plus / minus types; Step
/// only to Plus / minus and AmountRules only to Manual plus / minus. (How many
/// subgames to play is up to the players: see GameSession.SubgameCount.) Stored on
/// the game as JSON text, so later scoring types can add their own settings
/// without new database columns.
/// </summary>
public sealed record ScoringSettings
{
	/// <summary>How much one tap on + or − adds or subtracts.</summary>
	public int Step { get; init; } = 1;

	/// <summary>Every player's score when the game starts (and when they join later).</summary>
	public int StartScore { get; init; }

	/// <summary>Scores never go above this; null means no maximum.</summary>
	public int? MaxScore { get; init; }

	/// <summary>Scores never go below this; null means no minimum.</summary>
	public int? MinScore { get; init; }

	/// <summary>A player's card is highlighted once their score reaches this; null means no warning.</summary>
	public int? WarningScore { get; init; }

	/// <summary>Manual plus / minus: every amount a player types in must meet all of these.</summary>
	public List<ScoreRule> AmountRules { get; init; } = [];

	/// <summary>How the result of each subgame is noted: winner(s), a ranking or loser(s).</summary>
	public ResultMode ResultMode { get; init; } = ResultMode.Winner;

	/// <summary>
	/// False: the highest score is best in a subgame; true: the lowest. Used to
	/// pre-select the winner(s), loser(s) or ranking on the result screen.
	/// In loser mode "best" means "not the loser": true = the highest score loses.
	/// </summary>
	public bool LowestWins { get; init; }

	/// <summary>The two choices for LowestWins (false, true), worded for the result type.</summary>
	public static List<string> ScoreDirectionOptions(ResultMode mode) => mode switch
	{
		ResultMode.Loser => ["Lowest score loses", "Highest score loses"],
		ResultMode.Ranking => ["Highest score is 1st", "Lowest score is 1st"],
		_ => ["Highest score wins", "Lowest score wins"]
	};

	[JsonIgnore]
	public string ScoreDirectionLabel => ScoreDirectionOptions(ResultMode)[LowestWins ? 1 : 0].ToLowerInvariant();

	public static ScoringSettings Default { get; } = new();

	/// <summary>"winner(s) only", "ranking (1st, 2nd, 3rd...)" or "loser(s) only".</summary>
	[JsonIgnore]
	public string ResultSummary => $"{ResultModeOption.LabelFor(ResultMode).ToLowerInvariant()} · {ScoreDirectionLabel}";

	/// <summary>"steps of 1 · start at 0 · max 10 · warning at 9 · ...", for admins reviewing a game.</summary>
	public string SummaryFor(ScoringType type)
	{
		var parts = new List<string>();
		if (type == ScoringType.PlusMinus) parts.Add($"steps of {Step}");
		if (type == ScoringType.ManualPlusMinus)
			parts.Add(AmountRules.Count == 0 ? "any amount" : $"amounts {AmountRulesText}");
		parts.Add($"start at {StartScore}");
		if (MinScore is { } min) parts.Add($"min {min}");
		if (MaxScore is { } max) parts.Add($"max {max}");
		if (WarningScore is { } warning) parts.Add($"warning at {warning}");
		parts.Add(ResultSummary);
		return string.Join(" · ", parts);
	}

	/// <summary>"divisible by 50 and (higher than or equal to 350 or lower than 0)".</summary>
	[JsonIgnore]
	public string AmountRulesText => AmountRuleSet.Describe(AmountRules);

	/// <summary>Whether a player may type in this amount (Manual plus / minus). Negative amounts are allowed if the rules allow them.</summary>
	public bool AllowsAmount(int amount) => AmountRuleSet.Allows(AmountRules, amount);

	/// <summary>With a min and a max, an amount bigger than this (either way) can never fully count.</summary>
	private int? ScoreRange => MaxScore is { } max && MinScore is { } min ? max - min : null;

	/// <summary>
	/// The allowed amounts from low to high, at most `limit` of them. Complete
	/// is true when these are all of them. Searched within the score range
	/// when there's a min and max; otherwise far enough past the rules'
	/// numbers to see whether the allowed amounts go on forever.
	/// </summary>
	public (List<int> Amounts, bool Complete) AllowedAmounts(int limit, bool ignoreScoreRange = false)
	{
		var allows = AmountRuleSet.Predicate(AmountRules);
		var reach = AmountRuleSet.Reach(AmountRules);
		var range = ignoreScoreRange ? null : ScoreRange;
		var bound = range ?? reach + 100_000;

		var amounts = new List<int>();
		var endless = false;
		for (var n = -bound; n <= bound; n++)
		{
			if (!allows(n)) continue;
			// Allowed beyond every number in the rules: it keeps repeating, so there are endless amounts.
			if (range is null && Math.Abs(n) > reach) endless = true;
			if (amounts.Count <= limit) amounts.Add(n);
			if (amounts.Count > limit && (endless || range is not null)) break;
		}

		return (amounts.Take(limit).ToList(), !endless && amounts.Count <= limit);
	}

	/// <summary>
	/// An amount the rules allow, as an example for the amount box: the one
	/// closest to 0 (positive first), but not 0 itself. Null when there are no
	/// rules - then the box stays empty - or when nothing is allowed.
	/// </summary>
	public int? ExampleAmount()
	{
		if (AmountRules.Count == 0) return null;
		var allows = AmountRuleSet.Predicate(AmountRules);
		var bound = ScoreRange ?? AmountRuleSet.Reach(AmountRules) + 100_000;
		for (var n = 1; n <= bound; n++)
		{
			if (allows(n)) return n;
			if (allows(-n)) return -n;
		}
		return null;
	}

	/// <summary>"You can enter −50, 350 or 400." or the rules in words, for the amount prompt.</summary>
	public string AllowedAmountsHint()
	{
		var (amounts, complete) = AllowedAmounts(12);
		if (complete && amounts.Count > 0)
			return amounts.Count == 1
				? $"You can enter {amounts[0]}."
				: $"You can enter {string.Join(", ", amounts.SkipLast(1))} or {amounts[^1]}.";
		return AmountRules.Count == 0 ? "Any amount." : $"The amount must be {AmountRulesText}.";
	}

	/// <summary>Why these settings can't be used with this scoring type, or null when they're fine.</summary>
	public string? Problem(ScoringType type)
	{
		if (type == ScoringType.PlusMinus && Step < 1)
			return "The step must be at least 1.";
		if (MinScore is { } min && MaxScore is { } max && min >= max)
			return "The minimum score must be lower than the maximum score.";
		if (MaxScore is { } maxScore && StartScore > maxScore)
			return "The starting score can't be higher than the maximum score.";
		if (MinScore is { } minScore && StartScore < minScore)
			return "The starting score can't be lower than the minimum score.";
		if (WarningScore is { } warning)
		{
			if (warning == StartScore)
				return "The warning can't be the same as the starting score - everyone would start with a warning.";
			if (MaxScore is { } m && warning > m || MinScore is { } n && warning < n)
				return "The warning must be between the minimum and maximum score.";
		}
		if (type == ScoringType.ManualPlusMinus)
			return AmountRulesProblem();
		return null;
	}

	/// <summary>Rules missing a number, or rules that together allow no useful amount at all.</summary>
	private string? AmountRulesProblem()
	{
		for (var i = 0; i < AmountRules.Count; i++)
			if (AmountRules[i].Problem(i + 1) is { } problem)
				return problem;

		var (amounts, _) = AllowedAmounts(2);
		if (amounts.Count == 0)
		{
			// Tell apart "the rules clash" from "only amounts too big for the score range".
			return AllowedAmounts(1, ignoreScoreRange: true).Amounts.Count == 0
				? $"No amount can ever be entered: an amount can't be {AmountRulesText}. Change a rule, or link rules with OR instead of AND."
				: $"Every amount the rules allow is bigger than the whole range from the minimum to the maximum score ({MinScore} to {MaxScore}), so it could never count.";
		}
		if (amounts is [0])
			return "With these rules only 0 can be entered, so scores would never change. Change or remove a rule.";
		return null;
	}

	// AmountRules is a list, so compare by content rather than by reference.
	public bool Equals(ScoringSettings? other) => other is not null && ToJson() == other.ToJson();

	public override int GetHashCode() => ToJson().GetHashCode();

	public string ToJson() => JsonSerializer.Serialize(this);

	/// <summary>Games saved before scoring settings existed have none: they get the defaults.</summary>
	public static ScoringSettings FromJson(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return Default;
		try
		{
			return JsonSerializer.Deserialize<ScoringSettings>(json) ?? Default;
		}
		catch (JsonException)
		{
			return Default;
		}
	}

	/// <summary>As a Firestore map field.</summary>
	public Dictionary<string, object?> ToMap() => new()
	{
		["step"] = Step,
		["startScore"] = StartScore,
		["maxScore"] = MaxScore,
		["minScore"] = MinScore,
		["warningScore"] = WarningScore,
		["resultMode"] = ResultMode.ToString(),
		["lowestWins"] = LowestWins,
		// As JSON text: the Firestore client doesn't write lists of maps.
		["amountRules"] = JsonSerializer.Serialize(AmountRules)
	};

	public static ScoringSettings FromMap(object? value)
	{
		if (value is not IDictionary<string, object?> map) return Default;

		int? Number(string key) => map.TryGetValue(key, out var v) && v is not null ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : null;
		return new ScoringSettings
		{
			Step = Number("step") ?? 1,
			StartScore = Number("startScore") ?? 0,
			MaxScore = Number("maxScore"),
			MinScore = Number("minScore"),
			WarningScore = Number("warningScore"),
			ResultMode = map.TryGetValue("resultMode", out var mode) && Enum.TryParse<ResultMode>(mode as string, out var m) ? m : ResultMode.Winner,
			LowestWins = map.TryGetValue("lowestWins", out var lowest) && lowest is true,
			AmountRules = map.TryGetValue("amountRules", out var rules) && rules is string json ? RulesFromJson(json) : []
		};
	}

	private static List<ScoreRule> RulesFromJson(string json)
	{
		try
		{
			return JsonSerializer.Deserialize<List<ScoreRule>>(json) ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}
}
