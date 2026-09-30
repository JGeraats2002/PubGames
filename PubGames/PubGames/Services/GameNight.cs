using PubGames.Models;

namespace PubGames.Services;

/// <summary>
/// Starts game sessions, and remembers who was playing at the end of the last
/// game (seat changes included), so the Players page shows that line-up again
/// after "Quit".
/// </summary>
public class GameNight(ILocalDatabaseService local)
{
	/// <summary>Set when a game ends; the Players page takes it once.</summary>
	public List<Player>? NextLineup { get; set; }

	/// <summary>
	/// "Play again": the new session the scoreboard switches to when it shows
	/// again. Taken once.
	/// </summary>
	public string? NextSessionId { get; set; }

	/// <summary>
	/// A new session of the game with these players, seated in this order, all
	/// at the game's starting score. Returns the session id for the scoreboard.
	/// </summary>
	public async Task<string> StartSessionAsync(PubGame game, IReadOnlyList<Player> players, int subgameCount, string accountId)
	{
		var session = new GameSession
		{
			GameId = game.Id,
			AccountId = accountId,
			SubgameCount = subgameCount
		};
		await local.SaveSessionAsync(session);

		var startScore = ScoringRules.For(game).StartScore;
		for (var i = 0; i < players.Count; i++)
		{
			await local.SaveParticipantAsync(new SessionParticipant
			{
				SessionId = session.Id,
				PlayerId = players[i].Id,
				Position = i,
				Score = startScore
			});
		}
		return session.Id;
	}
}
