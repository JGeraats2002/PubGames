using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace PubGames.Models;

/// <summary>
/// A way to group games, like "Family", "Cards" or "Pub". Every game is in at
/// least one category; players filter the game list by category. Added by an
/// admin directly, or by a moderator through a review request. Stored in the
/// cloud "categories" collection; which games are in it is kept on each game
/// (PubGame.CategoryIds).
/// </summary>
public class GameCategory
{
	/// <summary>Created the first time an admin opens the app when no categories exist yet.</summary>
	public static readonly string[] Defaults =
		["Family", "Friends", "Pub", "Home", "Cards", "Dice", "Analog", "Digital"];

	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	public string Name { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public static string NormalizeName(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

/// <summary>A category with a tick box, e.g. in the game editor or the players' filter.</summary>
public partial class CategoryChoice : ObservableObject
{
	public required GameCategory Category { get; init; }

	[ObservableProperty]
	private bool isSelected;
}
