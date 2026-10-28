using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace PubGames.Models;

/// <summary>
/// A way to group games, like "Cards", "15-30 min" or "Drinking game". Categories
/// are filed under a main category (CategoryGroup) and every game is in at least
/// one category of each; players filter the game list by category. Added by an
/// admin directly, or by a moderator through a review request. Stored in the
/// cloud "categories" collection; which games are in it is kept on each game
/// (PubGame.CategoryIds).
/// </summary>
public class GameCategory
{
	/// <summary>The category's unique id.</summary>
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	/// <summary>The category's name, e.g. Short.</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>The name in other languages, as JSON by language code; made by the cloud (translateCategory).</summary>
	public string? TranslationsText { get; set; }

	/// <summary>
	/// The name in the app's language: the translation made by hand in the string files ("Category: name")
	/// when there is one, otherwise the cloud's translation, otherwise the name itself.
	/// </summary>
	[Ignore]
	public string ShownName =>
		Services.L.Known($"Category: {Name}")
		?? (GameTranslation.NamesFromJson(TranslationsText).TryGetValue(Services.AppLanguage.Current.Code, out var translated)
			&& !string.IsNullOrWhiteSpace(translated) ? translated : Name);

	/// <summary>The main category (CategoryGroup) this is filed under; empty for none.</summary>
	public string GroupId { get; set; } = string.Empty;

	/// <summary>Place in the list, lowest first; an admin can move it up and down. Equal places go oldest first.</summary>
	public int SortOrder { get; set; }

	/// <summary>When the category was made (UTC).</summary>
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	/// <summary>
	/// A category name with single spaces and no spaces at the ends, so "Short  game " and "Short game" are
	/// the same name.
	/// </summary>
	public static string NormalizeName(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

/// <summary>A category with a tick box, e.g. in the game editor or the players' filter.</summary>
public partial class CategoryChoice : ObservableObject
{
	/// <summary>The category this choice is for.</summary>
	public required GameCategory Category { get; init; }

	/// <summary>Whether the category is chosen (for a game, or as a filter).</summary>
	[ObservableProperty]
	private bool isSelected;
}
