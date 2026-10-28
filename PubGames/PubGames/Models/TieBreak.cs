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
	/// <summary>This tie-break moment, as the app calls it.</summary>
	public string Label => LabelFor(Moment);

	/// <summary>The Picker shows this.</summary>
	public override string ToString() => Label;

	/// <summary>Every tie-break moment, as the picker shows them.</summary>
	public static List<TieBreakMomentOption> All { get; } = Enum.GetValues<TieBreakMoment>().Select(m => new TieBreakMomentOption(m)).ToList();

	/// <summary>What a tie-break moment is called in the app.</summary>
	public static string LabelFor(TieBreakMoment moment) => moment switch
	{
		TieBreakMoment.SubgamesOnly => L.T("After every subgame"),
		TieBreakMoment.FinalOnly => L.T("After the final total"),
		_ => L.T("After every subgame and the final total")
	};
}

/// <summary>A tie-break scope as the dropdown shows it; the wording follows the result type.</summary>
public sealed record TieBreakScopeOption(TieBreakScope Scope, ResultMode Mode)
{
	/// <summary>This choice of which ties are played off, as the app calls it.</summary>
	public string Label => LabelFor(Scope, Mode);

	/// <summary>The Picker shows this.</summary>
	public override string ToString() => Label;

	/// <summary>The choices of which ties are played off, worded for this way of noting results.</summary>
	public static List<TieBreakScopeOption> AllFor(ResultMode mode) =>
		Enum.GetValues<TieBreakScope>().Select(s => new TieBreakScopeOption(s, mode)).ToList();

	/// <summary>
	/// What a choice of which ties are played off is called, worded for this way of noting results.
	/// </summary>
	public static string LabelFor(TieBreakScope scope, ResultMode mode) => scope switch
	{
		TieBreakScope.Every => L.T("Every shared place"),
		_ => mode == ResultMode.Loser ? L.T("Only a shared loss") : L.T("Only a shared 1st place")
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
		for (var groupIndex = 0; groupIndex < order.Count; groupIndex++)
		{
			if (order[groupIndex].Count < 2) continue;
			if (wholeOrder || (worstCounts ? groupIndex == order.Count - 1 : groupIndex == 0))
				yield return groupIndex;
		}
	}

	/// <summary>"Jan, Sanne and Piet".</summary>
	public static string Names(IReadOnlyList<string> names) =>
		names.Count <= 1 ? string.Concat(names) : L.F("{0} and {1}", string.Join(", ", names.SkipLast(1)), names[^1]);
}

/// <summary>How a tie-break duel ends. Stored as text, so the order may change.</summary>
public enum DuelEnd
{
	Turns,  // every player plays a number of turns; best score wins, a tie plays another turn
	Score,  // rounds are played until someone has reached the end score; best score wins
	BestOf  // every round's best player wins that round; first to most of the rounds wins
}

/// <summary>A way of ending a duel as the dropdown shows it.</summary>
public sealed record DuelEndOption(DuelEnd End)
{
	/// <summary>How a duel ends, as the picker shows it.</summary>
	public string Label => End switch
	{
		DuelEnd.Score => L.T("First to reach a score"),
		DuelEnd.BestOf => L.T("Best of a number of rounds"),
		_ => L.T("A number of turns each")
	};

	/// <summary>The Picker shows this.</summary>
	public override string ToString() => Label;

	/// <summary>Every way a duel can end, as the picker shows them.</summary>
	public static List<DuelEndOption> All { get; } = Enum.GetValues<DuelEnd>().Select(e => new DuelEndOption(e)).ToList();
}
