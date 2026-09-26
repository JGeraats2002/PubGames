namespace PubGames.Models;

/// <summary>
/// The scoring notation a host picks when creating a game. Drives how the
/// live scoreboard behaves (what buttons/inputs it shows, how "winner" is
/// computed at the end of a session).
/// </summary>
public enum ScoringType
{
	PointTally,       // running total, add or subtract per round
	SipCounter,       // tally of drinks taken, no win condition
	WinLoseRounds,    // best-of-N round wins
	Ranking,          // 1st / 2nd / 3rd placement per round
	Custom            // host-defined notation (free text rules, manual score entry)
}
