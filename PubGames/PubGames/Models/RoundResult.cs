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
	public string Label => LabelFor(Mode);

	/// <summary>The Picker shows this.</summary>
	public override string ToString() => Label;

	public static List<ResultModeOption> All { get; } = Enum.GetValues<ResultMode>().Select(m => new ResultModeOption(m)).ToList();

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
	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }

	[Indexed]
	public string SessionId { get; set; } = string.Empty;

	/// <summary>1-based subgame number.</summary>
	public int Round { get; set; }

	public string PlayerId { get; set; } = string.Empty;

	/// <summary>The player's score when the subgame ended, for the record.</summary>
	public int Score { get; set; }

	/// <summary>Ranking: 1 = first. Shared places have the same number.</summary>
	public int? Place { get; set; }

	public bool IsWinner { get; set; }

	public bool IsLoser { get; set; }
}

/// <summary>One player's totals over all subgames played so far.</summary>
public class PlayerStanding
{
	public required string PlayerId { get; init; }
	public required string Name { get; init; }

	public int Subgames { get; set; }
	public int Wins { get; set; }
	public int Losses { get; set; }

	/// <summary>Ranking: place points, 1st of 4 players = 4 points, last = 1.</summary>
	public int Points { get; set; }

	/// <summary>Overall place, 1 = best; shared when totals are equal.</summary>
	public int Place { get; set; }

	public string PlaceLabel => Standings.Ordinal(Place);

	/// <summary>Only on the final result; during the game the ranking stays hidden.</summary>
	public bool ShowPlace { get; set; }

	/// <summary>"3 wins", "2 losses" or "11 points", plus how many subgames they played.</summary>
	public string Total { get; set; } = string.Empty;
}

public static class Standings
{
	/// <summary>Place points in one subgame: with 4 players 1st = 4, 2nd = 3, ... last = 1.</summary>
	public static int PointsFor(int place, int players) => Math.Max(1, players - place + 1);

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
		var all = results.ToList();
		var standings = new Dictionary<string, PlayerStanding>();
		foreach (var round in all.GroupBy(r => r.Round))
		{
			var players = round.Count();
			foreach (var r in round)
			{
				if (!standings.TryGetValue(r.PlayerId, out var s))
					standings[r.PlayerId] = s = new PlayerStanding { PlayerId = r.PlayerId, Name = names.GetValueOrDefault(r.PlayerId) ?? "Unknown player" };
				s.Subgames++;
				if (r.IsWinner) s.Wins++;
				if (r.IsLoser) s.Losses++;
				if (r.Place is { } place) s.Points += PointsFor(place, players);
			}
		}

		Func<PlayerStanding, int> score = mode switch
		{
			ResultMode.Ranking => s => s.Points,
			ResultMode.Loser => s => -s.Losses,
			_ => s => s.Wins
		};
		var sorted = standings.Values.OrderByDescending(score).ThenBy(s => s.Name).ToList();
		for (var i = 0; i < sorted.Count; i++)
		{
			// Equal totals share a place: 1, 1, 3.
			sorted[i].Place = i > 0 && score(sorted[i]) == score(sorted[i - 1]) ? sorted[i - 1].Place : i + 1;
			var s = sorted[i];
			var total = mode switch
			{
				ResultMode.Ranking => $"{s.Points} {(s.Points == 1 ? "point" : "points")}",
				ResultMode.Loser => $"{s.Losses} {(s.Losses == 1 ? "loss" : "losses")}",
				_ => $"{s.Wins} {(s.Wins == 1 ? "win" : "wins")}"
			};
			s.Total = $"{total} · {s.Subgames} {(s.Subgames == 1 ? "subgame" : "subgames")}";
		}
		return sorted;
	}

	/// <summary>"🏆 Winner: Jan", "🏆 Winners: Jan &amp; Sanne" or "Loser: Piet" for the final result.</summary>
	public static string Headline(ResultMode mode, List<PlayerStanding> sorted)
	{
		if (sorted.Count == 0) return "No results yet.";

		if (mode == ResultMode.Loser)
		{
			var mostLosses = sorted.Max(s => s.Losses);
			if (mostLosses == 0) return "Nobody lost a subgame.";
			var losers = sorted.Where(s => s.Losses == mostLosses).Select(s => s.Name).ToList();
			return $"✖ {(losers.Count == 1 ? "Loser" : "Losers")}: {Names(losers)}";
		}

		var winners = sorted.Where(s => s.Place == 1).Select(s => s.Name).ToList();
		return $"🏆 {(winners.Count == 1 ? "Winner" : "Winners")}: {Names(winners)}";
	}

	private static string Names(List<string> names) =>
		names.Count <= 1 ? string.Concat(names) : $"{string.Join(", ", names.SkipLast(1))} & {names[^1]}";
}
