using SQLite;

namespace PubGames.Models;

/// <summary>
/// One played "round" of a game with a specific group of friends. This is
/// what makes score history possible: many sessions can exist for the same
/// game, each with its own set of participants and scores.
/// </summary>
public class GameSession
{
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	[Indexed]
	public string GameId { get; set; } = string.Empty;

	/// <summary>The account that started/played this session (for "my history").</summary>
	public string AccountId { get; set; } = string.Empty;

	public DateTime StartedAt { get; set; } = DateTime.UtcNow;

	/// <summary>Set when the players finish the whole game; the final result is shown from then on.</summary>
	public DateTime? EndedAt { get; set; }

	/// <summary>The subgame being played, 1-based. Sessions from before subgames existed have 0: treat as 1.</summary>
	public int CurrentRound { get; set; } = 1;

	/// <summary>How many subgames the players chose to play; null means they finish whenever they like.</summary>
	public int? SubgameCount { get; set; }

	/// <summary>When a score was last changed; how long a saved game is kept counts from here (see SavedGames).</summary>
	public DateTime? LastPlayedAt { get; set; }

	/// <summary>The last time anything happened in this game.</summary>
	[SQLite.Ignore]
	public DateTime LastActivity => new[] { LastPlayedAt, EndedAt, StartedAt }.Max() ?? StartedAt;

	/// <summary>Fixed input order: whose turn it is, as an index into the active players in seat order.</summary>
	public int TurnIndex { get; set; }

	/// <summary>
	/// Equalizing turn: the turn on which a player reached the end score in
	/// this subgame. The subgame ends when the turn comes back round to seat 1.
	/// Null while nobody reached it.
	/// </summary>
	public int? FinalRoundFrom { get; set; }

	/// <summary>True once synced to the cloud; lets local-only sessions be created offline first.</summary>
	public bool IsSynced { get; set; }
}

/// <summary>
/// One player's row within a session. Score and seat position are stored as
/// SEPARATE fields on purpose: reordering the seating (drag to reorder) only
/// touches Position, and removing/adding a player only touches IsActive -
/// neither operation ever recalculates or shifts anyone else's Score.
/// </summary>
public class SessionParticipant
{
	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }

	[Indexed]
	public string SessionId { get; set; } = string.Empty;

	[Indexed]
	public string PlayerId { get; set; } = string.Empty;

	public int Score { get; set; }

	/// <summary>Seat/display order, independent of score - safe to change freely mid-game.</summary>
	public int Position { get; set; }

	/// <summary>False if this player was removed mid-session; their past score stays on record.</summary>
	public bool IsActive { get; set; } = true;
}
