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
public record ScoringRules(
	string Unit,
	string Explanation,
	int? MinusStep,
	int PlusStep,
	bool AllowsTypedAmount,
	bool? HighestWins)
{
	public static ScoringRules For(ScoringType type) => type switch
	{
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

	public string MinusLabel => MinusStep is { } m ? m.ToString("+0;−0") : string.Empty;
	public string PlusLabel => PlusStep.ToString("+0;−0");
}
