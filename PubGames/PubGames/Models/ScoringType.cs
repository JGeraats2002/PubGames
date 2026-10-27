namespace PubGames.Models;

/// <summary>
/// The scoring notation an admin picks when creating a game. Drives how the
/// live scoreboard behaves (what buttons/inputs it shows, how "winner" is
/// computed at the end of a session). Stored as a number on the phone, so
/// new types go at the end.
/// </summary>
public enum ScoringType
{
	PointTally,       // running total, add or subtract per round
	SipCounter,       // tally of drinks taken, no win condition
	WinLoseRounds,    // best-of-N round wins
	Ranking,          // 1st / 2nd / 3rd placement per round
	Custom,           // admin-defined notation (free text rules, manual score entry)
	PlusMinus,        // + and − by a set step, optional min/max and warning (see ScoringSettings)
	ManualPlusMinus,  // + and − by an amount typed in each time, within the game's amount rules
	TypedScore        // only for a tie-break duel: everyone's final duel score is typed in at once
}

/// <summary>A scoring type as the dropdown shows it.</summary>
public sealed record ScoringTypeOption(ScoringType Type)
{
	/// <summary>This scoring type, as the app calls it.</summary>
	public string Label => LabelFor(Type);

	/// <summary>The Picker shows this.</summary>
	public override string ToString() => Label;

	/// <summary>
	/// What admins can pick. Only Plus / minus for now; the other types still
	/// work for games that already use them, but can't be chosen any more.
	/// </summary>
	public static List<ScoringTypeOption> All { get; } = [new(ScoringType.PlusMinus), new(ScoringType.ManualPlusMinus)];

	/// <summary>Whether admins can still choose this scoring type for a new game.</summary>
	public static bool IsOffered(ScoringType type) => All.Any(o => o.Type == type);

	/// <summary>What admins can pick for a tie-break duel: the game's types, or typing in the duel's score.</summary>
	public static List<ScoringTypeOption> DuelOptions { get; } = [.. All, new(ScoringType.TypedScore)];

	/// <summary>What a scoring type is called in the app.</summary>
	public static string LabelFor(ScoringType type) => type switch
	{
		ScoringType.PlusMinus => "Plus / minus",
		ScoringType.ManualPlusMinus => "Manual plus / minus",
		ScoringType.TypedScore => "Type in the score",
		ScoringType.PointTally => "Point tally",
		ScoringType.SipCounter => "Sip counter",
		ScoringType.WinLoseRounds => "Win/lose rounds",
		ScoringType.Ranking => "Ranking",
		_ => "Custom"
	};
}
