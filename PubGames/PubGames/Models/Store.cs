using System.Globalization;

namespace PubGames.Models;

/// <summary>
/// The fixed prices. A game costs as much as a week of the Pub Games Pass
/// (a big game twice that), and each longer pass is about 30% cheaper than
/// the shorter one before it. These are the prices set in the Play Console;
/// the app shows the price Google Play gives (in the player's own currency)
/// and falls back to these.
/// </summary>
public static class Prices
{
	private static readonly CultureInfo Euro = CultureInfo.GetCultureInfo("nl-NL");

	/// <summary>An amount in euros, the Dutch way: € 0,99.</summary>
	public static string Format(decimal euros) => euros.ToString("C", Euro);

	/// <summary>What an admin can choose for a game: free or one of the fixed prices.</summary>
	public static IReadOnlyList<GamePriceOption> GameOptions { get; } =
	[
		new(0m, "Free"),
		new(0.99m, $"{Format(0.99m)} - standard game"),
		new(1.99m, $"{Format(1.99m)} - big game")
	];

	/// <summary>The Pub Games Pass: every paid game, for as long as it runs.</summary>
	public static IReadOnlyList<PassPlan> PassPlans { get; } =
	[
		new("week", "Week", 0.99m),
		new("month", "Month", 2.99m),
		new("year", "Year", 24.99m)
	];

	public const string PassProductId = "pubgames_pass";

	/// <summary>Google Play's product id for a game - the server (functions/index.js) makes the same one.</summary>
	public static string ProductIdFor(string gameId) =>
		"game_" + new string(gameId.ToLowerInvariant().Select(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '_' or '.' ? c : '_').ToArray());
}

/// <summary>A price an admin can give a game.</summary>
public sealed record GamePriceOption(decimal Price, string Label)
{
	/// <summary>Whether this is a price (not Free).</summary>
	public bool IsPaid => Price > 0;

	/// <summary>The price's text, as the picker shows it.</summary>
	public override string ToString() => Label;
}

/// <summary>One way to take the Pub Games Pass: its base plan in the Play Console, its name and its fixed price.</summary>
public sealed record PassPlan(string BasePlanId, string Name, decimal Price);

/// <summary>
/// What the signed-in account owns, as the server recorded it
/// (entitlements/{uid}): bought games (or unlocked with a friend code), and
/// until when the pass runs.
/// </summary>
public sealed record Entitlements(IReadOnlySet<string> Games, DateTime? PassUntil, string? PassPlan, bool PassRenews)
{
	/// <summary>An account that owns nothing.</summary>
	public static Entitlements None { get; } = new(new HashSet<string>(), null, null, false);

	/// <summary>Until when every game is unlocked.</summary>
	public DateTime? PassRunsUntil => PassUntil;

	/// <summary>Whether the pass runs now.</summary>
	public bool HasPass => PassUntil is { } until && until > DateTime.UtcNow;

	/// <summary>The pass runs and renews by itself (it wasn't cancelled).</summary>
	public bool Renews => PassRenews && HasPass;

	/// <summary>Free games, bought games, and every game while the pass runs.</summary>
	public bool Owns(PubGame game) => !game.IsPaid || HasPass || Games.Contains(game.Id);
}
