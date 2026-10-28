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
	/// <summary>How long games are kept, as the picker shows it.</summary>
	public string Label => Retention switch
	{
		SavedGamesRetention.DontSave => L.T("Don't save games"),
		SavedGamesRetention.Week => L.T("1 week"),
		SavedGamesRetention.Month => L.T("1 month"),
		SavedGamesRetention.SixMonths => L.T("6 months"),
		SavedGamesRetention.Year => L.T("1 year"),
		_ => L.T("Never delete")
	};

	/// <summary>The Picker shows this.</summary>
	public override string ToString() => Label;

	/// <summary>Every choice of how long games are kept, as the picker shows them.</summary>
	public static List<SavedGamesRetentionOption> All { get; } = Enum.GetValues<SavedGamesRetention>().Select(r => new SavedGamesRetentionOption(r)).ToList();
}

/// <summary>A saved game as the lists show it.</summary>
public sealed class SavedGame
{
	/// <summary>The saved game night.</summary>
	public required GameSession Session { get; init; }
	/// <summary>The name of the game that was played.</summary>
	public required string GameName { get; init; }

	/// <summary>"Jan 120 · Sanne 80 · Piet 40" while playing; the players' names once finished.</summary>
	public required string Players { get; init; }

	/// <summary>Whether the game was finished.</summary>
	public bool IsFinished => Session.EndedAt is not null;

	/// <summary>"Unfinished · subgame 2 of 3 · today 21:14" or "Finished · 12 Oct".</summary>
	public string Details
	{
		get
		{
			var state = IsFinished ? L.T("Finished")
				: Session.SubgameCount is { } count ? L.F("Unfinished · subgame {0} of {1}", Math.Max(1, Session.CurrentRound), count)
				: L.F("Unfinished · subgame {0}", Math.Max(1, Session.CurrentRound));
			var when = Session.LastActivity.ToLocalTime();
			var date = when.Date == DateTime.Today ? L.F("today {0:HH:mm}", when)
				: when.Date == DateTime.Today.AddDays(-1) ? L.F("yesterday {0:HH:mm}", when)
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
/// stats), and are cleaned up after the chosen time - 1 month unless changed,
/// with a separate time for unfinished and for finished games.
/// </summary>
public class SavedGames(ILocalDatabaseService local, IAuthService auth)
{
	// Unfinished games keep the key they always had; finished games got their own setting later.
	private const string RetentionKey = "savedGamesRetention";
	private const string FinishedRetentionKey = "finishedGamesRetention";

	/// <summary>How long unfinished games are kept.</summary>
	public SavedGamesRetention Retention
	{
		get => Read(RetentionKey, SavedGamesRetention.Month);
		set => Preferences.Default.Set(RetentionKey, value.ToString());
	}

	/// <summary>How long finished games are kept; until it's chosen, the same as for unfinished games.</summary>
	public SavedGamesRetention FinishedRetention
	{
		get => Read(FinishedRetentionKey, Retention);
		set => Preferences.Default.Set(FinishedRetentionKey, value.ToString());
	}

	/// <summary>A saved choice of how long games are kept, or the default when none was saved.</summary>
	private static SavedGamesRetention Read(string key, SavedGamesRetention fallback) =>
		Enum.TryParse<SavedGamesRetention>(Preferences.Default.Get(key, fallback.ToString()), out var r) ? r : fallback;

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
	/// score, and games older than the time chosen for unfinished or for
	/// finished games (with Don't save: every such game). A finished game whose result hasn't reached the stats yet is
	/// kept until it has. Call when no game is open, e.g. on the Players page.
	/// </summary>
	public async Task<List<SavedGame>> GetAsync()
	{
		var sessions = await local.GetSessionsAsync(auth.AccountId);
		var games = new Dictionary<string, PubGame?>();
		var saved = new List<SavedGame>();
		var delete = new List<string>();

		foreach (var session in sessions)
		{
			var waitingForStats = session.EndedAt is not null && !session.IsSynced;
			// Finished and unfinished games each have their own time.
			var retention = session.EndedAt is null ? Retention : FinishedRetention;
			var maxAge = MaxAge(retention);
			var tooOld = retention == SavedGamesRetention.DontSave
				|| maxAge is { } age && DateTime.UtcNow - session.LastActivity > age;
			if (tooOld && !waitingForStats)
			{
				delete.Add(session.Id);
				continue;
			}

			if (!games.TryGetValue(session.GameId, out var game))
				games[session.GameId] = game = (await local.GetGameAsync(session.GameId))?.InAppLanguage();
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
			string Name(SessionParticipant p) => names.GetValueOrDefault(p.PlayerId) ?? L.T("Unknown player");
			saved.Add(new SavedGame
			{
				Session = session,
				GameName = game?.Name ?? L.T("Game"),
				Players = session.EndedAt is null
					? string.Join(" · ", active.Select(p => $"{Name(p)} {p.Score}"))
					: string.Join(", ", active.Select(Name))
			});
		}

		await local.DeleteSessionsAsync(delete);
		return saved.OrderByDescending(g => g.Session.LastActivity).ToList();
	}

	/// <summary>Deletes these games from the phone.</summary>
	public Task DeleteAsync(IEnumerable<string> sessionIds) => local.DeleteSessionsAsync(sessionIds);
}
