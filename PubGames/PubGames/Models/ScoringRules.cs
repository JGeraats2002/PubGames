namespace PubGames.Models;

/// <summary>
/// How the scoreboard behaves for a game's scoring type: what the number
/// counts, which quick buttons each player gets, and who's in the lead.
/// </summary>
/// <param name="Unit">What the score counts, e.g. "points" or "sips".</param>
/// <param name="Explanation">Short explanation shown above the scoreboard.</param>
/// <param name="MinusStep">Amount of the minus button (null: no minus button).</param>
/// <param name="PlusStep">Amount of the plus button.</param>
/// <param name="AllowsTypedAmount">Tapping the score lets you type any amount to add.</param>
/// <param name="HighestWins">Whether the highest or lowest score leads; null when there's no winner (e.g. sips).</param>
/// <param name="StartScore">Every player's score when they start playing.</param>
/// <param name="MinScore">Scores never go below this (null: no minimum).</param>
/// <param name="MaxScore">Scores never go above this (null: no maximum).</param>
/// <param name="WarningScore">A player's card is highlighted once their score reaches this (null: no warning).</param>
public record ScoringRules(
	string Unit,
	string Explanation,
	int? MinusStep,
	int PlusStep,
	bool AllowsTypedAmount,
	bool? HighestWins,
	int StartScore = 0,
	int? MinScore = null,
	int? MaxScore = null,
	int? WarningScore = null)
{
	/// <summary>The rules for a game; its scoring settings fill in Plus / minus.</summary>
	public static ScoringRules For(PubGame? game) => game is null
		? For(ScoringType.Custom)
		: For(game.ScoringType, game.ScoringSettings);

	public static ScoringRules For(ScoringType type, ScoringSettings? settings = null) => type switch
	{
		ScoringType.PlusMinus => PlusMinus(settings ?? ScoringSettings.Default),

		ScoringType.PointTally => new("points",
			"Add or subtract points each round. Tap a score to add any amount. Highest total wins.",
			MinusStep: -1, PlusStep: 1, AllowsTypedAmount: true, HighestWins: true),

		ScoringType.SipCounter => new("sips",
			"Count the sips everyone takes. There's no winner - just keep track.",
			MinusStep: -1, PlusStep: 1, AllowsTypedAmount: true, HighestWins: null),

		ScoringType.WinLoseRounds => new("rounds won",
			"Tap + for the player who wins a round. Most rounds won wins the game.",
			MinusStep: -1, PlusStep: 1, AllowsTypedAmount: false, HighestWins: true),

		ScoringType.Ranking => new("points",
			"After each round, add each player's placement points (tap a score to type them). Highest total wins.",
			MinusStep: -1, PlusStep: 1, AllowsTypedAmount: true, HighestWins: true),

		_ => new("points",
			"Keep score the way the rules describe. Tap a score to add any amount.",
			MinusStep: -1, PlusStep: 1, AllowsTypedAmount: true, HighestWins: null)
	};

	/// <summary>Only + and −, each by the game's step, kept between its min and max.</summary>
	private static ScoringRules PlusMinus(ScoringSettings s)
	{
		var explanation = $"Tap + or − to change a score by {s.Step}. Everyone starts at {s.StartScore}.";
		if (s.MinScore is { } min && s.MaxScore is { } max)
			explanation += $" Scores stay between {min} and {max}.";
		else if (s.MaxScore is { } maxOnly)
			explanation += $" Maximum is {maxOnly}.";
		else if (s.MinScore is { } minOnly)
			explanation += $" Minimum is {minOnly}.";
		if (s.WarningScore is { } warning)
			explanation += $" Warning at {warning}.";

		return new("points", explanation,
			MinusStep: -s.Step, PlusStep: s.Step, AllowsTypedAmount: false, HighestWins: null,
			StartScore: s.StartScore, MinScore: s.MinScore, MaxScore: s.MaxScore, WarningScore: s.WarningScore);
	}

	public string MinusLabel => MinusStep is { } m ? m.ToString("+0;−0") : string.Empty;
	public string PlusLabel => PlusStep.ToString("+0;−0");

	/// <summary>A score kept within the min and max.</summary>
	public int Clamp(int score)
	{
		if (MaxScore is { } max && score > max) score = max;
		if (MinScore is { } min && score < min) score = min;
		return score;
	}

	/// <summary>
	/// The score has reached the warning, counted in the direction of play:
	/// a warning above the start triggers at or above it, one below at or below it.
	/// </summary>
	public bool IsWarning(int score) => WarningScore is { } warning
		&& (warning > StartScore ? score >= warning : score <= warning);

	/// <summary>The score is at the min or max. A limit equal to the starting score doesn't count, or everyone would start there.</summary>
	public bool IsAtLimit(int score) =>
		(MaxScore is { } max && max != StartScore && score >= max)
		|| (MinScore is { } min && min != StartScore && score <= min);

	/// <summary>The badge on a player's card: "▲ MAX REACHED" / "▼ MIN REACHED" / "⚠ WARNING", or empty.</summary>
	public string StatusFor(int score) =>
		IsAtLimit(score) ? (MaxScore is { } max && score >= max ? "▲ MAX REACHED" : "▼ MIN REACHED")
		: IsWarning(score) ? "⚠ WARNING"
		: string.Empty;
}
