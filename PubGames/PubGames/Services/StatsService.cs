using System.Text.Json;
using PubGames.Models;

namespace PubGames.Services;

public interface IStatsService
{
	/// <summary>
	/// Sends the result of every finished game on this phone that hasn't been
	/// sent yet to the stats of the linked players in it. Games finished
	/// offline go out the next time this runs online.
	/// </summary>
	Task PublishFinishedGamesAsync();

	/// <summary>Every finished game the signed-in user played in, newest first. Offline: the last copy seen.</summary>
	Task<(List<PlayedGame> Games, bool Offline)> GetMyGamesAsync();
}

/// <summary>
/// One cloud document per finished game (gameResults/{sessionId}) with each
/// linked player's outcome. Every player's stats are the documents they're
/// in; only players in a game can read it (see firestore.rules).
/// </summary>
public class StatsService(ILocalDatabaseService local, FirestoreClient firestore, IAuthService auth) : IStatsService
{
	private const string Collection = "gameResults";

	private string CacheKey => $"stats:{auth.AccountId}";

	public async Task PublishFinishedGamesAsync()
	{
		if (!auth.IsSignedIn) return;

		foreach (var session in await local.GetUnpublishedFinishedSessionsAsync(auth.AccountId))
		{
			await PublishAsync(session);
			session.IsSynced = true;
			await local.SaveSessionAsync(session);
		}
	}

	private async Task PublishAsync(GameSession session)
	{
		var results = await local.GetRoundResultsAsync(session.Id);
		var players = (await local.GetPlayersByIdsAsync(results.Select(r => r.PlayerId).Distinct())).ToDictionary(p => p.Id);

		// Only players linked to an account have stats to add to.
		var linked = players.Values.Where(p => p.IsLinked).ToList();
		if (linked.Count == 0) return;

		var game = await local.GetGameAsync(session.GameId);
		var mode = game?.ScoringSettings.ResultMode ?? ResultMode.Winner;
		var standings = Standings.Calculate(mode, results, players.ToDictionary(p => p.Key, p => p.Value.Name));
		var outcomes = Standings.Outcomes(mode, standings);

		// One entry per account, even if someone was on the list twice.
		var byAccount = linked
			.Where(p => outcomes.ContainsKey(p.Id))
			.GroupBy(p => p.LinkedAccountId!)
			.ToDictionary(g => g.Key, g => (object?)outcomes[g.First().Id].ToString());
		if (byAccount.Count == 0) return;

		await firestore.SetAsync(Collection, session.Id, new Dictionary<string, object?>
		{
			["id"] = session.Id,
			["hostUid"] = auth.AccountId,
			["gameId"] = session.GameId,
			["gameName"] = game?.Name ?? "Game",
			["playedAt"] = session.EndedAt ?? DateTime.UtcNow,
			["participantUids"] = byAccount.Keys.ToList(),
			["outcomes"] = byAccount
		});
	}

	public async Task<(List<PlayedGame> Games, bool Offline)> GetMyGamesAsync()
	{
		var uid = auth.AccountId;
		try
		{
			var docs = await firestore.WhereArrayContainsAsync(Collection, "participantUids", uid);
			var games = docs.Select(d => ToPlayedGame(d, uid)).OfType<PlayedGame>().OrderByDescending(g => g.PlayedAt).ToList();
			Preferences.Default.Set(CacheKey, JsonSerializer.Serialize(games));
			return (games, false);
		}
		catch (Exception ex) when (ex is HttpRequestException or CloudException)
		{
			try
			{
				var json = Preferences.Default.Get(CacheKey, string.Empty);
				return (json.Length == 0 ? [] : JsonSerializer.Deserialize<List<PlayedGame>>(json) ?? [], true);
			}
			catch (JsonException)
			{
				return ([], true);
			}
		}
	}

	private static PlayedGame? ToPlayedGame(Dictionary<string, object?> f, string uid)
	{
		if (f.GetValueOrDefault("outcomes") is not IDictionary<string, object?> outcomes
			|| !outcomes.TryGetValue(uid, out var mine)
			|| !Enum.TryParse<GameOutcome>(mine as string, out var outcome))
			return null;
		return new PlayedGame(
			f.GetValueOrDefault("gameId") as string ?? string.Empty,
			f.GetValueOrDefault("gameName") as string ?? "Game",
			f.GetValueOrDefault("playedAt") as DateTime? ?? DateTime.MinValue,
			outcome);
	}
}
