namespace PubGames.Models;

/// <summary>When a game without ties plays off a tie. Stored as text, so the order may change.</summary>
public enum TieBreakMoment
{
	SubgamesAndFinal, // after every subgame and after the final total
	SubgamesOnly,     // after every subgame; a tie in the final total offers an extra subgame
	FinalOnly         // subgames may end in a tie; only the final total is played off
}

/// <summary>Which ties a game without ties plays off. Stored as text, so the order may change.</summary>
public enum TieBreakScope
{
	Decisive, // only a shared 1st place (or, in a loser game, a shared loss)
	Every     // every shared place
}

/// <summary>A tie-break moment as the dropdown shows it.</summary>
public sealed record TieBreakMomentOption(TieBreakMoment Moment)
{
	public string Label => LabelFor(Moment);

	/// <summary>The Picker shows this.</summary>
	public override string ToString() => Label;

	public static List<TieBreakMomentOption> All { get; } = Enum.GetValues<TieBreakMoment>().Select(m => new TieBreakMomentOption(m)).ToList();

	public static string LabelFor(TieBreakMoment moment) => moment switch
	{
		TieBreakMoment.SubgamesOnly => "After every subgame",
		TieBreakMoment.FinalOnly => "After the final total",
		_ => "After every subgame and the final total"
	};
}

/// <summary>A tie-break scope as the dropdown shows it; the wording follows the result type.</summary>
public sealed record TieBreakScopeOption(TieBreakScope Scope, ResultMode Mode)
{
	public string Label => LabelFor(Scope, Mode);

	/// <summary>The Picker shows this.</summary>
	public override string ToString() => Label;

	public static List<TieBreakScopeOption> AllFor(ResultMode mode) =>
		Enum.GetValues<TieBreakScope>().Select(s => new TieBreakScopeOption(s, mode)).ToList();

	public static string LabelFor(TieBreakScope scope, ResultMode mode) => scope switch
	{
		TieBreakScope.Every => "Every shared place",
		_ => mode == ResultMode.Loser ? "Only a shared loss" : "Only a shared 1st place"
	};
}

/// <summary>
/// Works out a tie-break duel: who's still tied after the duel scores are in,
/// and the order the duel puts the players in.
/// </summary>
public static class TieBreak
{
	/// <summary>
	/// The players in groups from best to worst duel score; players in one group
	/// had the same score. LowestWins: the lowest duel score is best.
	/// </summary>
	public static List<List<string>> Order(IReadOnlyDictionary<string, int> scores, bool lowestWins) => scores
		.GroupBy(s => s.Value)
		.OrderBy(g => lowestWins ? g.Key : -g.Key)
		.Select(g => g.Select(s => s.Key).ToList())
		.ToList();

	/// <summary>
	/// Which groups of an order still need another duel: every group of two or
	/// more when the whole order counts, otherwise only the best group (or the
	/// worst one, when the loser is what counts).
	/// </summary>
	public static IEnumerable<int> StillTied(List<List<string>> order, bool wholeOrder, bool worstCounts)
	{
		for (var i = 0; i < order.Count; i++)
		{
			if (order[i].Count < 2) continue;
			if (wholeOrder || (worstCounts ? i == order.Count - 1 : i == 0))
				yield return i;
		}
	}

	/// <summary>"Jan, Sanne and Piet".</summary>
	public static string Names(IReadOnlyList<string> names) =>
		names.Count <= 1 ? string.Concat(names) : $"{string.Join(", ", names.SkipLast(1))} and {names[^1]}";
}
