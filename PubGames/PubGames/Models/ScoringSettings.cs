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

	/// <summary>
	/// True: players can share a win, a loss or a place (they get the same
	/// result). False: a tie is played off in a duel; see the TieBreak and Duel settings.
	/// </summary>
	public bool AllowTies { get; init; } = true;

	/// <summary>No ties: whether a tie is played off after each subgame, after the final total, or both.</summary>
	public TieBreakMoment TieBreakMoment { get; init; } = TieBreakMoment.SubgamesAndFinal;

	/// <summary>No ties: only the ties that decide the winner (or loser), or every shared place.</summary>
	public TieBreakScope TieBreakScope { get; init; } = TieBreakScope.Decisive;

	/// <summary>No ties: the duel is scored like the game itself; false uses DuelScoringType and DuelSettings.</summary>
	public bool DuelSameScoring { get; init; } = true;

	/// <summary>The duel's own scoring: Plus / minus, Manual plus / minus or Type in the score.</summary>
	public ScoringType DuelScoringType { get; init; } = ScoringType.PlusMinus;

	/// <summary>The duel's own numbers and which duel score wins; only its scoring fields and LowestWins count.</summary>
	public ScoringSettings? DuelSettings { get; init; }

	/// <summary>Who starts the next subgame: the seats are turned so that player sits in seat 1.</summary>
	public StartRotation StartRotation { get; init; } = StartRotation.None;

	/// <summary>
	/// Scores are entered in seat order: seat 1, then seat 2, ... Only the
	/// player whose turn it is can enter a score, and 0 has to be entered too.
	/// </summary>
	public bool FixedTurnOrder { get; init; }

	/// <summary>A subgame ends once a player's score reaches this (at or past it, away from the start); null: no end score.</summary>
	public int? EndScore { get; init; }

	/// <summary>Whether reaching the end score ends the subgame by itself, or the players end it.</summary>
	public EndMode EndMode { get; init; } = EndMode.Automatic;

	/// <summary>
	/// Automatic end with a fixed turn order: the players after the one who
	/// reached the end score still get their turn in that round, so everyone
	/// had as many turns.
	/// </summary>
	public bool EqualizingTurn { get; init; }

	// How a duel ends - only used in a duel's own settings (DuelSettings).

	/// <summary>Duel: how it ends. Null for duels from before this could be chosen: see EffectiveDuelEnd.</summary>
	public DuelEnd? DuelEnd { get; init; }

	/// <summary>Duel, a number of turns: how many turns each player plays before the scores are compared.</summary>
	public int DuelTurns { get; init; } = 1;

	/// <summary>Duel, best of: how many rounds; the first to win more than half of them wins.</summary>
	public int DuelRounds { get; init; } = 3;

	/// <summary>Duel, turns or score: the lead needed to win; null is any lead (1).</summary>
	public int? DuelMargin { get; init; }

	/// <summary>How the duel ends. Older duels: at their end score if they had one, otherwise after one turn each.</summary>
	[JsonIgnore]
	public DuelEnd EffectiveDuelEnd => DuelEnd ?? (EndScore is null ? Models.DuelEnd.Turns : Models.DuelEnd.Score);

	/// <summary>Best of: the rounds needed to win the duel.</summary>
	[JsonIgnore]
	public int DuelRoundsToWin => DuelRounds / 2 + 1;

	/// <summary>"each player plays 1 turn", "first to 10, winning by 2", "best of 3 rounds".</summary>
	[JsonIgnore]
	public string DuelEndSummary
	{
		get
		{
			var margin = DuelMargin is > 1 ? $", winning by at least {DuelMargin}" : string.Empty;
			return EffectiveDuelEnd switch
			{
				Models.DuelEnd.Score => $"rounds until someone reaches {EndScore}, best score wins{margin}",
				Models.DuelEnd.BestOf => $"best of {DuelRounds} rounds",
				_ => $"each player plays {DuelTurns} {(DuelTurns == 1 ? "turn" : "turns")}, best score wins{margin}; a tie plays another turn"
			};
		}
	}

	/// <summary>Why a duel's end can't work, or null when it's fine.</summary>
	private string? DuelEndProblem() => EffectiveDuelEnd switch
	{
		Models.DuelEnd.Score when EndScore is null => "Enter the score the duel goes to.",
		Models.DuelEnd.Turns when DuelTurns < 1 => "Each player needs at least 1 turn.",
		Models.DuelEnd.BestOf when DuelRounds < 1 => "The duel needs at least 1 round.",
		_ when DuelMargin is < 1 => "The lead needed to win must be at least 1.",
		_ => null
	};

	/// <summary>The score has reached the end score, counted away from the starting score.</summary>
	public bool ReachesEnd(int score) => EndScore is { } end && (end >= StartScore ? score >= end : score <= end);

	/// <summary>"next subgame starts with: the loser · fixed input order · subgame ends automatically at 100 with an equalizing turn".</summary>
	[JsonIgnore]
	public string TurnSummary
	{
		get
		{
			var parts = new List<string>();
			if (StartRotation != StartRotation.None)
				parts.Add($"next subgame starts with: {StartRotationOption.LabelFor(StartRotation).ToLowerInvariant()}");
			if (FixedTurnOrder) parts.Add("fixed input order");
			if (EndScore is { } end)
				parts.Add(EndMode == EndMode.Automatic
					? $"subgame ends automatically at {end}{(EqualizingTurn ? " with an equalizing turn" : string.Empty)}"
					: $"subgame can end at {end}");
			return string.Join(" · ", parts);
		}
	}

	/// <summary>Ties after a subgame are played off.</summary>
	[JsonIgnore]
	public bool DuelsAfterSubgame => !AllowTies && TieBreakMoment != TieBreakMoment.FinalOnly;

	/// <summary>Ties in the final total are played off.</summary>
	[JsonIgnore]
	public bool DuelsAfterFinal => !AllowTies && TieBreakMoment != TieBreakMoment.SubgamesOnly;

	/// <summary>How a duel is scored, given the game's own scoring type.</summary>
	public (ScoringType Type, ScoringSettings Settings) DuelScoring(ScoringType gameType) =>
		DuelSameScoring || DuelSettings is null ? (gameType, this) : (DuelScoringType, DuelSettings);

	/// <summary>"ties allowed" or "no ties: duel after every subgame and the final total, for 1st place / the loser, same scoring as the game".</summary>
	public string TieSummaryFor(ScoringType gameType)
	{
		if (AllowTies) return "ties allowed";
		var (type, duel) = DuelScoring(gameType);
		var scoring = DuelSameScoring ? "same rules as the game"
			: type is ScoringType.PlusMinus or ScoringType.ManualPlusMinus
				? $"duel scoring: {ScoringTypeOption.LabelFor(type).ToLowerInvariant()}, {duel.SummaryFor(type, withResult: false)}, {(duel.LowestWins ? "lowest" : "highest")} score wins"
				: $"duel scoring: {ScoringTypeOption.LabelFor(type).ToLowerInvariant()}, {(duel.LowestWins ? "lowest" : "highest")} score wins";
		scoring += $", {duel.DuelEndSummary}";
		return $"no ties: duel {TieBreakMomentOption.LabelFor(TieBreakMoment).ToLowerInvariant()}, {TieBreakScopeOption.LabelFor(TieBreakScope, ResultMode).ToLowerInvariant()}, {scoring}";
	}

	public static ScoringSettings Default { get; } = new();

	/// <summary>"winner(s) only", "ranking (1st, 2nd, 3rd...)" or "loser(s) only".</summary>
	[JsonIgnore]
	public string ResultSummary => $"{ResultModeOption.LabelFor(ResultMode).ToLowerInvariant()} · {ScoreDirectionLabel}";

	/// <summary>"steps of 1 · start at 0 · max 10 · warning at 9 · ...", for admins reviewing a game.</summary>
	public string SummaryFor(ScoringType type, bool withResult = true)
	{
		var parts = new List<string>();
		if (type == ScoringType.PlusMinus) parts.Add($"steps of {Step}");
		if (type == ScoringType.ManualPlusMinus)
			parts.Add(AmountRules.Count == 0 ? "any amount" : $"amounts {AmountRulesText}");
		parts.Add($"start at {StartScore}");
		if (MinScore is { } min) parts.Add($"min {min}");
		if (MaxScore is { } max) parts.Add($"max {max}");
		if (WarningScore is { } warning) parts.Add($"warning at {warning}");
		if (withResult) parts.Add(ResultSummary);
		if (withResult && TurnSummary.Length > 0) parts.Add(TurnSummary);
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
		if (EndScore is { } end)
		{
			if (end == StartScore)
				return "The end score can't be the same as the starting score - every subgame would end at once.";
			if (MaxScore is { } m && end > m || MinScore is { } n && end < n)
				return "The end score must be between the minimum and maximum score.";
		}
		if (EqualizingTurn && (EndScore is null || EndMode != EndMode.Automatic || !FixedTurnOrder))
			return "An equalizing turn needs an end score that ends the subgame automatically, and a fixed input order (so the app knows whose turn it is).";
		if (type == ScoringType.ManualPlusMinus && AmountRulesProblem() is { } amountProblem)
			return amountProblem;
		if (!AllowTies && !DuelSameScoring && DuelSettings is { } duelSettings
			&& (duelSettings.DuelEndProblem() ?? duelSettings.Problem(DuelScoringType)) is { } duelProblem)
			return $"Duel: {duelProblem}";
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
		["amountRules"] = JsonSerializer.Serialize(AmountRules),
		["allowTies"] = AllowTies,
		["tieBreakMoment"] = TieBreakMoment.ToString(),
		["tieBreakScope"] = TieBreakScope.ToString(),
		["duelSameScoring"] = DuelSameScoring,
		["duelScoringType"] = DuelScoringType.ToString(),
		["duelSettings"] = DuelSettings?.ToJson(),
		["startRotation"] = StartRotation.ToString(),
		["fixedTurnOrder"] = FixedTurnOrder,
		["endScore"] = EndScore,
		["endMode"] = EndMode.ToString(),
		["equalizingTurn"] = EqualizingTurn
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
			AmountRules = map.TryGetValue("amountRules", out var rules) && rules is string json ? RulesFromJson(json) : [],
			// Games from before ties could be switched off allow them.
			AllowTies = !map.TryGetValue("allowTies", out var ties) || ties is not false,
			TieBreakMoment = map.TryGetValue("tieBreakMoment", out var moment) && Enum.TryParse<TieBreakMoment>(moment as string, out var tm) ? tm : TieBreakMoment.SubgamesAndFinal,
			TieBreakScope = map.TryGetValue("tieBreakScope", out var scope) && Enum.TryParse<TieBreakScope>(scope as string, out var ts) ? ts : TieBreakScope.Decisive,
			DuelSameScoring = !map.TryGetValue("duelSameScoring", out var same) || same is not false,
			DuelScoringType = map.TryGetValue("duelScoringType", out var duelType) && Enum.TryParse<ScoringType>(duelType as string, out var dt) ? dt : ScoringType.PlusMinus,
			DuelSettings = map.TryGetValue("duelSettings", out var duel) && duel is string duelJson && duelJson.Length > 0 ? FromJson(duelJson) : null,
			StartRotation = map.TryGetValue("startRotation", out var rotation) && Enum.TryParse<StartRotation>(rotation as string, out var sr) ? sr : StartRotation.None,
			FixedTurnOrder = map.TryGetValue("fixedTurnOrder", out var fixedOrder) && fixedOrder is true,
			EndScore = Number("endScore"),
			EndMode = map.TryGetValue("endMode", out var endMode) && Enum.TryParse<EndMode>(endMode as string, out var em) ? em : EndMode.Automatic,
			EqualizingTurn = map.TryGetValue("equalizingTurn", out var equalizing) && equalizing is true
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
