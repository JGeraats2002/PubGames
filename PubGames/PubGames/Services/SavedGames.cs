using PubGames.Models;

namespace PubGames.Services;

/// <summary>How long games stay saved on this phone. Stored as text, so the order may change.</summary>
public enum SavedGamesRetention
{
	DontSave,
	Week,
	Month,
	SixMonths,
	Year,
	Forever
}

/// <summary>A retention as the dropdown shows it.</summary>
public sealed record SavedGamesRetentionOption(SavedGamesRetention Retention)
{
	public string Label => Retention switch
	{
		SavedGamesRetention.DontSave => "Don't save games",
		SavedGamesRetention.Week => "1 week",
		SavedGamesRetention.Month => "1 month",
		SavedGamesRetention.SixMonths => "6 months",
		SavedGamesRetention.Year => "1 year",
		_ => "Never delete"
	};

	/// <summary>The Picker shows this.</summary>
	public override string ToString() => Label;

	public static List<SavedGamesRetentionOption> All { get; } = Enum.GetValues<SavedGamesRetention>().Select(r => new SavedGamesRetentionOption(r)).ToList();
}

/// <summary>A saved game as the lists show it.</summary>
public sealed class SavedGame
{
	public required GameSession Session { get; init; }
	public required string GameName { get; init; }

	/// <summary>"Jan 120 · Sanne 80 · Piet 40" while playing; the players' names once finished.</summary>
	public required string Players { get; init; }

	public bool IsFinished => Session.EndedAt is not null;

	/// <summary>"Unfinished · subgame 2 of 3 · today 21:14" or "Finished · 12 Oct".</summary>
	public string Details
	{
		get
		{
			var state = IsFinished ? "Finished"
				: Session.SubgameCount is { } count ? $"Unfinished · subgame {Math.Max(1, Session.CurrentRound)} of {count}"
				: $"Unfinished · subgame {Math.Max(1, Session.CurrentRound)}";
			var when = Session.LastActivity.ToLocalTime();
			var date = when.Date == DateTime.Today ? $"today {when:HH:mm}"
				: when.Date == DateTime.Today.AddDays(-1) ? $"yesterday {when:HH:mm}"
				: when.ToString(when.Year == DateTime.Today.Year ? "d MMM" : "d MMM yyyy");
			return $"{state} · {date}";
		}
	}
}

/// <summary>
/// The games played on this phone, kept for the account's Saved games page:
/// unfinished ones can be continued, finished ones looked back at. Only
/// games in which at least one score was entered are kept. They're only on
/// this phone (the cloud just gets each finished game's result for the
/// stats), and are cleaned up after the chosen time - 1 month unless changed.
/// </summary>
public class SavedGames(ILocalDatabaseService local, IAuthService auth)
{
	private const string RetentionKey = "savedGamesRetention";

	public SavedGamesRetention Retention
	{
		get => Enum.TryParse<SavedGamesRetention>(Preferences.Default.Get(RetentionKey, nameof(SavedGamesRetention.Month)), out var r)
			? r : SavedGamesRetention.Month;
		set => Preferences.Default.Set(RetentionKey, value.ToString());
	}

	/// <summary>Games older than this are deleted; null keeps them.</summary>
	private static TimeSpan? MaxAge(SavedGamesRetention retention) => retention switch
	{
		SavedGamesRetention.Week => TimeSpan.FromDays(7),
		SavedGamesRetention.Month => TimeSpan.FromDays(31),
		SavedGamesRetention.SixMonths => TimeSpan.FromDays(183),
		SavedGamesRetention.Year => TimeSpan.FromDays(366),
		_ => null
	};

	/// <summary>
	/// The saved games, newest first, after cleaning up: games without a single
	/// score, and games older than the chosen time (with Don't save: every
	/// game). A finished game whose result hasn't reached the stats yet is
	/// kept until it has. Call when no game is open, e.g. on the Players page.
	/// </summary>
	public async Task<List<SavedGame>> GetAsync()
	{
		var sessions = await local.GetSessionsAsync(auth.AccountId);
		var retention = Retention;
		var maxAge = MaxAge(retention);
		var games = new Dictionary<string, PubGame?>();
		var saved = new List<SavedGame>();
		var delete = new List<string>();

		foreach (var session in sessions)
		{
			var waitingForStats = session.EndedAt is not null && !session.IsSynced;
			var tooOld = retention == SavedGamesRetention.DontSave
				|| maxAge is { } age && DateTime.UtcNow - session.LastActivity > age;
			if (tooOld && !waitingForStats)
			{
				delete.Add(session.Id);
				continue;
			}

			if (!games.TryGetValue(session.GameId, out var game))
				games[session.GameId] = game = await local.GetGameAsync(session.GameId);
			var participants = await local.GetParticipantsAsync(session.Id);
			var results = await local.GetRoundResultsAsync(session.Id);
			var startScore = ScoringRules.For(game).StartScore;
			var hasScore = session.EndedAt is not null || results.Count > 0 || participants.Any(p => p.Score != startScore);
			if (!hasScore)
			{
				if (!waitingForStats) delete.Add(session.Id);
				continue;
			}

			var active = participants.Where(p => p.IsActive).ToList();
			var names = (await local.GetPlayersByIdsAsync(active.Select(p => p.PlayerId))).ToDictionary(p => p.Id, p => p.Name);
			string Name(SessionParticipant p) => names.GetValueOrDefault(p.PlayerId) ?? "Unknown player";
			saved.Add(new SavedGame
			{
				Session = session,
				GameName = game?.Name ?? "Game",
				Players = session.EndedAt is null
					? string.Join(" · ", active.Select(p => $"{Name(p)} {p.Score}"))
					: string.Join(", ", active.Select(Name))
			});
		}

		await local.DeleteSessionsAsync(delete);
		return saved.OrderByDescending(g => g.Session.LastActivity).ToList();
	}

	public Task DeleteAsync(IEnumerable<string> sessionIds) => local.DeleteSessionsAsync(sessionIds);
}
