using System.Globalization;

namespace PubGames.Models;

/// <summary>
/// A game that's being made and isn't finished yet (cloud "drafts"
/// collection). Every admin and moderator sees it, so nobody makes the same
/// game twice. Only its owner - who made it, or took it over - changes and
/// finishes it; the others can test it and send its owner a tip. A draft
/// nobody worked on for StaleAfter can be taken over.
/// </summary>
public sealed class GameDraft
{
	/// <summary>How long a draft can lie untouched before someone else can take it over.</summary>
	public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(3);

	/// <summary>The draft's id (also the document's).</summary>
	public string Id { get; init; } = Guid.NewGuid().ToString("N");

	/// <summary>The game as it's filled in so far.</summary>
	public required PubGame Game { get; init; }

	/// <summary>Lower-case email of who may change and finish it.</summary>
	public string OwnerEmail { get; set; } = string.Empty;

	/// <summary>The owner's name, for the list.</summary>
	public string OwnerName { get; set; } = string.Empty;

	/// <summary>Lower-case email of who started it.</summary>
	public string CreatedByEmail { get; init; } = string.Empty;

	/// <summary>When it was started (UTC).</summary>
	public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

	/// <summary>When its owner last saved it (or took it over), UTC.</summary>
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

	/// <summary>The game's name, or "Untitled draft".</summary>
	public string Name => string.IsNullOrWhiteSpace(Game.Name) ? L.T("Untitled draft") : Game.Name;

	/// <summary>Nobody worked on it for StaleAfter: someone else can take it over.</summary>
	public bool IsStale => DateTime.UtcNow - UpdatedAt > StaleAfter;

	/// <summary>"Jan · changed 2 days ago" for the list.</summary>
	public string InfoLine
	{
		get
		{
			var days = (int)(DateTime.UtcNow - UpdatedAt).TotalDays;
			var when = days switch
			{
				0 => L.T("changed today"),
				1 => L.T("changed yesterday"),
				_ => L.F("changed {0} days ago", days)
			};
			return $"{(string.IsNullOrEmpty(OwnerName) ? OwnerEmail : OwnerName)} · {when}";
		}
	}

	/// <summary>The line under a draft that can be taken over.</summary>
	public string StaleLabel => IsStale ? L.T("Untouched for 3 days or more: can be taken over") : string.Empty;

	/// <summary>When it was last changed, in the phone's time.</summary>
	public string UpdatedLabel => UpdatedAt.ToLocalTime().ToString("d MMM, HH:mm", CultureInfo.CurrentCulture);
}
