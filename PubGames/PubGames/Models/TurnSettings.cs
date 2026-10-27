namespace PubGames.Models;

/// <summary>
/// Who sits in seat 1 - and so starts - the next subgame. The seats are
/// turned around the table, so everyone keeps their neighbours. "Left of" a
/// player is the next seat (play goes clockwise), "right of" the seat before.
/// Stored as text, so the order may change.
/// </summary>
public enum StartRotation
{
	None,         // the seats stay as they are
	NextPlayer,   // the start passes one seat on every subgame
	Winner,
	LeftOfWinner,
	RightOfWinner,
	Loser,
	LeftOfLoser,
	RightOfLoser
}

/// <summary>A start rotation as the dropdown shows it.</summary>
public sealed record StartRotationOption(StartRotation Rotation)
{
	/// <summary>This choice of who starts, as the app calls it.</summary>
	public string Label => LabelFor(Rotation);

	/// <summary>The Picker shows this.</summary>
	public override string ToString() => Label;

	/// <summary>Every choice of who starts the next subgame, as the picker shows them.</summary>
	public static List<StartRotationOption> All { get; } = Enum.GetValues<StartRotation>().Select(r => new StartRotationOption(r)).ToList();

	/// <summary>What a choice of who starts the next subgame is called in the app.</summary>
	public static string LabelFor(StartRotation rotation) => rotation switch
	{
		StartRotation.NextPlayer => "The next player (one seat on)",
		StartRotation.Winner => "The winner of the last subgame",
		StartRotation.LeftOfWinner => "The player left of the winner",
		StartRotation.RightOfWinner => "The player right of the winner",
		StartRotation.Loser => "The loser of the last subgame",
		StartRotation.LeftOfLoser => "The player left of the loser",
		StartRotation.RightOfLoser => "The player right of the loser",
		_ => "Nobody changes seats"
	};
}

/// <summary>How a subgame ends at the end score.</summary>
public enum EndMode
{
	Manual,    // the players see it's reached and end the subgame themselves
	Automatic  // the subgame ends by itself (after an equalizing turn, if chosen)
}

/// <summary>An end mode as the dropdown shows it.</summary>
public sealed record EndModeOption(EndMode Mode)
{
	/// <summary>How a subgame ends at its end score, as the picker shows it.</summary>
	public string Label => Mode == EndMode.Automatic ? "Automatically" : "The players end it themselves";

	/// <summary>The Picker shows this.</summary>
	public override string ToString() => Label;

	/// <summary>Both ways a subgame can end at its end score, as the picker shows them.</summary>
	public static List<EndModeOption> All { get; } = [new(EndMode.Automatic), new(EndMode.Manual)];
}

/// <summary>Works out who starts the next subgame from the subgame's winners and losers.</summary>
public static class SeatRotation
{
	/// <summary>
	/// The seat (index into the players in seat order) who starts the next
	/// subgame, or null when the seats stay. Several winners or losers: the
	/// first of them in seat order.
	/// </summary>
	public static int? NextStarter(StartRotation rotation, int players, IReadOnlyList<bool> isWinner, IReadOnlyList<bool> isLoser)
	{
		if (players < 2) return null;
		int? First(IReadOnlyList<bool> flags)
		{
			for (var seat = 0; seat < flags.Count; seat++)
				if (flags[seat]) return seat;
			return null;
		}
		int Wrap(int seat) => ((seat % players) + players) % players;

		return rotation switch
		{
			StartRotation.NextPlayer => 1,
			StartRotation.Winner => First(isWinner),
			StartRotation.LeftOfWinner => First(isWinner) is { } w ? Wrap(w + 1) : null,
			StartRotation.RightOfWinner => First(isWinner) is { } w ? Wrap(w - 1) : null,
			StartRotation.Loser => First(isLoser),
			StartRotation.LeftOfLoser => First(isLoser) is { } l ? Wrap(l + 1) : null,
			StartRotation.RightOfLoser => First(isLoser) is { } l ? Wrap(l - 1) : null,
			_ => null
		};
	}
}
