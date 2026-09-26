using SQLite;

namespace PubGames.Models;

/// <summary>A tag like "Card", "Dice", "Drinking". A game can carry several.</summary>
public class GameCategory
{
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Join row implementing the many-to-many between games and categories, so
/// e.g. "Kings cup" can be both Card and Drinking at once.
/// </summary>
public class GameCategoryLink
{
	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }

	[Indexed]
	public string GameId { get; set; } = string.Empty;

	[Indexed]
	public string CategoryId { get; set; } = string.Empty;
}
