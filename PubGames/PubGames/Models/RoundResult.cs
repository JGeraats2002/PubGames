using SQLite;

namespace PubGames.Models;

/// <summary>How the result of each subgame is noted, chosen by the game's maker.</summary>
public enum ResultMode
{
	Winner,   // tap the winner(s)
	Ranking,  // give everyone a place: 1st, 2nd, 3rd... (places can be shared)
	Loser     // tap the loser(s)
}

/// <summary>A result mode as the dropdown shows it.</summary>
public sealed record ResultModeOption(ResultMode Mode)
{
	/// <summary>This way of noting results, as the app calls it.</summary>
	public string Label => LabelFor(Mode);

	/// <summary>The Picker shows this.</summary>
	public override string ToString() => Label;

	/// <summary>Every way of noting results, as the picker shows them.</summary>
	public static List<ResultModeOption> All { get; } = Enum.GetValues<ResultMode>().Select(m => new ResultModeOption(m)).ToList();

	/// <summary>What a way of noting results is called in the app.</summary>
	public static string LabelFor(ResultMode mode) => mode switch
	{
		ResultMode.Ranking => "Ranking (1st, 2nd, 3rd...)",
		ResultMode.Loser => "Loser(s) only",
		_ => "Winner(s) only"
	};
}

/// <summary>
/// One player's result in one subgame of a session, saved when the players
/// move on to the next subgame (or finish). Which fields count depends on the
/// game's ResultMode; several players can share a win, loss or place.
/// </summary>
public class RoundResult
{
	/// <summary>The row's number in the phone's database.</summary>
	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }

	/// <summary>The game night it belongs to.</summary>
	[Indexed]
	public string SessionId { get; set; } = string.Empty;

	/// <summary>1-based subgame number, or FinalDuelRound for a duel that broke a tie in the final total.</summary>
	public int Round { get; set; }

	/// <summary>
	/// The Round of a duel after the final total. Its Place is the player's rank
	/// in that duel (1 = best); it doesn't count as a subgame, it only orders
	/// players whose totals are equal.
	/// </summary>
	public const int FinalDuelRound = -1;

	/// <summary>A subgame's result, not the duel after the final total.</summary>
	[Ignore]
	public bool IsSubgame => Round != FinalDuelRound;

	/// <summary>The player.</summary>
	public string PlayerId { get; set; } = string.Empty;

	/// <summary>The player's score when the subgame ended, for the record.</summary>
	public int Score { get; set; }

	/// <summary>Ranking: 1 = first. Shared places have the same number.</summary>
	public int? Place { get; set; }

	/// <summary>Marked as a winner of the subgame.</summary>
	public bool IsWinner { get; set; }

	/// <summary>Marked as a loser of the subgame.</summary>
	public bool IsLoser { get; set; }
}

/// <summary>One player's totals over all subgames played so far.</summary>
public class PlayerStanding
{
	/// <summary>The player.</summary>
	public required string PlayerId { get; init; }
	/// <summary>The player's name.</summary>
	public required string Name { get; init; }

	/// <summary>How many subgames the player played.</summary>
	public int Subgames { get; set; }
	/// <summary>How many subgames the player won.</summary>
	public int Wins { get; set; }
	/// <summary>How many subgames the player lost.</summary>
	public int Losses { get; set; }

	/// <summary>Ranking: place points, 1st of 4 players = 4 points, last = 1.</summary>
	public int Points { get; set; }

	/// <summary>Overall place, 1 = best; shared when totals are equal.</summary>
	public int Place { get; set; }

	/// <summary>The place as text: 1st, 2nd ...</summary>
	public string PlaceLabel => Standings.Ordinal(Place);

	/// <summary>Only on the final result; during the game the ranking stays hidden.</summary>
	public bool ShowPlace { get; set; }

	/// <summary>"3 wins", "2 losses" or "11 points", plus how many subgames they played.</summary>
	public string Total { get; set; } = string.Empty;
}

/// <summary>
/// Works out the standings of a game from its subgame results: points, wins or losses, places, and each
/// player's outcome for their stats.
/// </summary>
public static class Standings
{
	/// <summary>Place points in one subgame: with 4 players 1st = 4, 2nd = 3, ... last = 1.</summary>
	public static int PointsFor(int place, int players) => Math.Max(1, players - place + 1);

	/// <summary>1st, 2nd, 3rd, 4th, 11th, 21st ...</summary>
	public static string Ordinal(int n) => (n % 100) switch
	{
		11 or 12 or 13 => $"{n}th",
		_ => (n % 10) switch { 1 => $"{n}st", 2 => $"{n}nd", 3 => $"{n}rd", _ => $"{n}th" }
	};

	/// <summary>
	/// Totals per player, best first. Winner mode: most wins is best. Ranking:
	/// most place points. Loser mode: fewest losses is best, so the player with
	/// the most losses ends up last - the real loser.
	/// </summary>
	public static List<PlayerStanding> Calculate(ResultMode mode, IEnumerable<RoundResult> results, IReadOnlyDictionary<string, string> names)
	{
		var all = results.Where(r => r.IsSubgame).ToList();
		var duelRank = results.Where(r => !r.IsSubgame && r.Place is not null).ToDictionary(r => r.PlayerId, r => r.Place!.Value);
		var standings = new Dictionary<string, PlayerStanding>();
		foreach (var round in all.GroupBy(r => r.Round))
		{
			var players = round.Count();
			foreach (var roundResult in round)
			{
				if (!standings.TryGetValue(roundResult.PlayerId, out var standing))
					standings[roundResult.PlayerId] = standing = new PlayerStanding { PlayerId = roundResult.PlayerId, Name = names.GetValueOrDefault(roundResult.PlayerId) ?? "Unknown player" };
				standing.Subgames++;
				if (roundResult.IsWinner) standing.Wins++;
				if (roundResult.IsLoser) standing.Losses++;
				if (roundResult.Place is { } place) standing.Points += PointsFor(place, players);
			}
		}

		Func<PlayerStanding, int> score = mode switch
		{
			ResultMode.Ranking => s => s.Points,
			ResultMode.Loser => s => -s.Losses,
			_ => s => s.Wins
		};
		// A duel after the final total decides between equal totals; players who didn't duel come after those who did.
		int Rank(PlayerStanding s) => duelRank.GetValueOrDefault(s.PlayerId, int.MaxValue);
		var sorted = standings.Values.OrderByDescending(score).ThenBy(Rank).ThenBy(s => s.Name).ToList();
		for (var position = 0; position < sorted.Count; position++)
		{
			// Equal totals share a place: 1, 1, 3 - unless a duel split them.
			var shared = position > 0 && score(sorted[position]) == score(sorted[position - 1]) && Rank(sorted[position]) == Rank(sorted[position - 1]);
			sorted[position].Place = shared ? sorted[position - 1].Place : position + 1;
			var standing = sorted[position];
			var total = mode switch
			{
				ResultMode.Ranking => $"{standing.Points} {(standing.Points == 1 ? "point" : "points")}",
				ResultMode.Loser => $"{standing.Losses} {(standing.Losses == 1 ? "loss" : "losses")}",
				_ => $"{standing.Wins} {(standing.Wins == 1 ? "win" : "wins")}"
			};
			standing.Total = $"{total} · {standing.Subgames} {(standing.Subgames == 1 ? "subgame" : "subgames")}";
		}
		return sorted;
	}

	/// <summary>
	/// How the game ended for each player (by player id), for their stats.
	/// Winner and ranking games: the only one in 1st place won, players sharing
	/// 1st tied, everyone else lost. Loser games: the only player in last place
	/// (the most losses, after any duel) lost, players sharing last place tied,
	/// everyone else won. When everyone ends equal, everyone tied.
	/// </summary>
	public static Dictionary<string, GameOutcome> Outcomes(ResultMode mode, List<PlayerStanding> sorted)
	{
		var outcomes = new Dictionary<string, GameOutcome>();
		if (sorted.Count == 0) return outcomes;

		if (mode == ResultMode.Loser)
		{
			var most = sorted.Max(s => s.Losses);
			var losers = Losers(sorted);
			foreach (var standing in sorted)
				outcomes[standing.PlayerId] = most == 0 || losers.Count == sorted.Count ? GameOutcome.Tied
					: !losers.Contains(standing) ? GameOutcome.Won
					: losers.Count == 1 ? GameOutcome.Lost
					: GameOutcome.Tied;
			return outcomes;
		}

		var first = sorted.Count(s => s.Place == 1);
		foreach (var standing in sorted)
			outcomes[standing.PlayerId] = standing.Place != 1 ? GameOutcome.Lost
				: first == 1 ? GameOutcome.Won
				: GameOutcome.Tied;
		return outcomes;
	}

	/// <summary>"🏆 Winner: Jan", "🏆 Winners: Jan &amp; Sanne" or "Loser: Piet" for the final result.</summary>
	public static string Headline(ResultMode mode, List<PlayerStanding> sorted)
	{
		if (sorted.Count == 0) return "No results yet.";

		if (mode == ResultMode.Loser)
		{
			if (sorted.Max(s => s.Losses) == 0) return "Nobody lost a subgame.";
			var losers = Losers(sorted).Select(s => s.Name).ToList();
			return $"✖ {(losers.Count == 1 ? "Loser" : "Losers")}: {Names(losers)}";
		}

		var winners = sorted.Where(s => s.Place == 1).Select(s => s.Name).ToList();
		return $"🏆 {(winners.Count == 1 ? "Winner" : "Winners")}: {Names(winners)}";
	}

	/// <summary>Loser games: the players in last place - the most losses, unless a duel decided between them.</summary>
	public static List<PlayerStanding> Losers(List<PlayerStanding> sorted)
	{
		if (sorted.Count == 0) return [];
		var last = sorted.Max(s => s.Place);
		return sorted.Where(s => s.Place == last).ToList();
	}

	/// <summary>"Jan", "Jan &amp; Sanne" or "Jan, Sanne &amp; Piet".</summary>
	private static string Names(List<string> names) =>
		names.Count <= 1 ? string.Concat(names) : $"{string.Join(", ", names.SkipLast(1))} & {names[^1]}";
}
