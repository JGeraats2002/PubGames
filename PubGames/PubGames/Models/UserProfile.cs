namespace PubGames.Models;

/// <summary>
/// A user's public profile (profiles/{uid} in the cloud): what other players
/// see when they add you by your personal code. Code never changes once made.
/// </summary>
public sealed record UserProfile(string Uid, string Name, string Nickname, string Code, string? PhotoRef)
{
	/// <summary>What a player is called when someone adds them: the nickname, or the name without one.</summary>
	public string DisplayName => string.IsNullOrWhiteSpace(Nickname) ? Name : Nickname;

	/// <summary>
	/// 31^8 ≈ 852 billion codes: over 100 for every person on earth, so a new
	/// random code is practically always free on the first try.
	/// </summary>
	public const int CodeLength = 8;

	/// <summary>No 0/O or 1/I/L, so a code read out loud or copied by hand can't be mistaken.</summary>
	public const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

	/// <summary>"ABCDEFGH" → "ABCD-EFGH", easier to read out. Typing it with or without the dash both work.</summary>
	public static string FormatCode(string code) =>
		code.Length == CodeLength ? $"{code[..4]}-{code[4..]}" : code;

	public static string NewCode() =>
		new(Enumerable.Range(0, CodeLength).Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)]).ToArray());

	/// <summary>"ab3k-9qx7" → "AB3K9QX7"; null when it can't be a code.</summary>
	public static string? NormalizeCode(string? text)
	{
		var code = new string((text ?? string.Empty).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
		return code.Length == CodeLength && code.All(CodeAlphabet.Contains) ? code : null;
	}
}

/// <summary>How a finished game ended for one player, for their stats.</summary>
public enum GameOutcome
{
	Won,
	Lost,
	Tied
}

/// <summary>One finished game on a player's account (from gameResults in the cloud).</summary>
public sealed record PlayedGame(string GameId, string GameName, DateTime PlayedAt, GameOutcome Outcome);

/// <summary>Played / won / lost / tied, with percentages, for all games or one game.</summary>
public sealed record GameStats(string Title, int Played, int Won, int Lost, int Tied)
{
	public static GameStats From(string title, IEnumerable<PlayedGame> games)
	{
		var list = games.ToList();
		return new(title, list.Count,
			list.Count(g => g.Outcome == GameOutcome.Won),
			list.Count(g => g.Outcome == GameOutcome.Lost),
			list.Count(g => g.Outcome == GameOutcome.Tied));
	}

	private string Percent(int n) => Played == 0 ? "0%" : $"{Math.Round(100.0 * n / Played)}%";

	public string WonText => $"{Won} ({Percent(Won)})";
	public string LostText => $"{Lost} ({Percent(Lost)})";
	public string TiedText => $"{Tied} ({Percent(Tied)})";

	/// <summary>"12 played · 5 won (42%) · 6 lost (50%) · 1 tied (8%)".</summary>
	public string Summary => $"{Played} played · {Won} won ({Percent(Won)}) · {Lost} lost ({Percent(Lost)}) · {Tied} tied ({Percent(Tied)})";
}
