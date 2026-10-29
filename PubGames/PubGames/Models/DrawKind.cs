namespace PubGames.Models;

/// <summary>What can be placed on a drawing in the image editor.</summary>
public enum DrawKind
{
	Arrow,
	Text,
	Card,
	Die,
	Hand,
	Drink,
	Person,
	Coin,
	Number,
	Check,
	Cross,
	Coaster,
	Table
}

/// <summary>Rules for what may be in a picture, kept apart from the drawing itself so they can be tested.</summary>
public static class DrawRules
{
	/// <summary>
	/// A hand showing only the middle finger (with or without the thumb): an insult, never allowed in a
	/// picture. Fingers in the order thumb, index, middle, ring, pinky; Neutral is an open hand. Must match
	/// isRudeHand in functions/shared.js.
	/// </summary>
	public static bool IsRudeHand(DrawKind kind, bool neutral, bool[]? fingers) =>
		kind == DrawKind.Hand && !neutral && fingers is { Length: 5 } && fingers[2] && !fingers[1] && !fingers[3] && !fingers[4];
}
