using SQLite;

namespace PubGames.Models;

/// <summary>
/// A main category: a question every game answers, like "Play style" (Cards,
/// Dice, ...) or "Duration" (15-30 min, ...). Categories are filed under one
/// (GameCategory.GroupId), and a game needs at least one category from each.
/// Added by an admin directly, or by a moderator through a review request.
/// Stored in the cloud "categoryGroups" collection.
/// </summary>
public class CategoryGroup
{
	/// <summary>
	/// Created, with their categories, the first time an admin opens the app
	/// when no main categories exist yet. Categories that already exist under
	/// the same name (e.g. Cards) are moved into their main category.
	/// </summary>
	public static readonly (string Id, string Name, string[] Categories)[] Defaults =
	[
		("alcohol", "Alcohol", ["Alcohol free", "Drinking game"]),
		("difficulty", "Difficulty", ["Easy", "Medium", "Hard"]),
		("duration", "Duration", ["-15 min", "15-30 min", "30-45 min", "45+ min"]),
		("playstyle", "Play style", ["Cards", "Dice", "Hands", "Mouth", "Other"])
	];

	/// <summary>
	/// "Number of players" used to be a default main category. It's a setting
	/// of each game now (PubGame.MinPlayers / MaxPlayers), so an admin's app
	/// removes the main category with this id.
	/// </summary>
	public const string RetiredPlayersId = "players";

	/// <summary>The main category's unique id.</summary>
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString("N");

	/// <summary>The main category's name, e.g. Duration.</summary>
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

	/// <summary>Place in the list, lowest first; an admin can move it up and down. Equal places go oldest first.</summary>
	public int SortOrder { get; set; }

	/// <summary>When the main category was made (UTC).</summary>
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	/// <summary>
	/// The categories under their main category, both in the order an admin
	/// gave them (see InOrder). Categories without a main category (or with
	/// one that no longer exists) come last, under a null group.
	/// </summary>
	/// <param name="inAppLanguage">
	/// For players and the game form: a list that is alphabetical in its own language stays alphabetical in
	/// the app's language (see ShownOrder). The categories page keeps the admins' order as it is.
	/// </param>
	public static List<(CategoryGroup? Group, List<GameCategory> Categories)> Arrange(
		IEnumerable<CategoryGroup> groups, IEnumerable<GameCategory> categories, bool inAppLanguage = false)
	{
		var orderedGroups = InOrder(groups);
		if (inAppLanguage)
			orderedGroups = ShownOrder(orderedGroups, g => g.Name, g => g.ShownName);
		var groupIds = orderedGroups.Select(g => g.Id).ToHashSet();
		var byGroup = categories
			.OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
			.ToLookup(c => !string.IsNullOrEmpty(c.GroupId) && groupIds.Contains(c.GroupId) ? c.GroupId : string.Empty);

		var arranged = orderedGroups
			.Select(g => ((CategoryGroup?)g, inAppLanguage ? ShownOrder(byGroup[g.Id].ToList(), c => c.Name, c => c.ShownName) : byGroup[g.Id].ToList()))
			.ToList();
		if (byGroup[string.Empty].Any())
			arranged.Add((null, byGroup[string.Empty]
				.OrderBy(c => inAppLanguage ? c.ShownName : c.Name, StringComparer.CurrentCultureIgnoreCase).ToList()));
		return arranged;
	}

	/// <summary>
	/// A list in the app's language: when it's in alphabetical order by its own names (Cards, Dice, Hands,
	/// ...), it's sorted again by the translated names, so it stays alphabetical. Any other order (Easy,
	/// Medium, Hard) was chosen on purpose and is kept. A last "Other" stays last.
	/// </summary>
	public static List<T> ShownOrder<T>(List<T> items, Func<T, string> name, Func<T, string> shown) where T : class
	{
		var last = items.Count > 0 && name(items[^1]).Trim().Equals("Other", StringComparison.OrdinalIgnoreCase) ? items[^1] : default;
		var sorted = last is null ? items : items.Take(items.Count - 1).ToList();
		if (sorted.Count < 2) return items;

		var names = sorted.Select(name).ToList();
		if (!names.SequenceEqual(names.OrderBy(n => n, StringComparer.InvariantCultureIgnoreCase))) return items;

		var result = sorted.OrderBy(shown, StringComparer.CurrentCultureIgnoreCase).ToList();
		if (last is not null) result.Add(last);
		return result;
	}

	/// <summary>
	/// Whether categories have an order of their own that A-Z would break:
	/// numbers (15-30 min, 3-4 players) or a scale (Easy, Medium, Hard).
	/// </summary>
	public static bool HaveOwnOrder(IEnumerable<GameCategory> categories)
	{
		string[] scale = ["easy", "medium", "hard", "beginner", "intermediate", "advanced", "expert",
			"low", "high", "small", "large", "short", "long", "few", "many"];
		var names = categories.Select(c => c.Name.Trim().ToLowerInvariant()).ToList();
		return names.Any(n => n.Any(char.IsDigit)) || names.Count(scale.Contains) >= 2;
	}

	/// <summary>Main categories in their order: by place, and oldest first where that's equal.</summary>
	public static List<CategoryGroup> InOrder(IEnumerable<CategoryGroup> groups) =>
		groups.OrderBy(g => g.SortOrder).ThenBy(g => g.CreatedAt).ToList();
}

/// <summary>One main category's clouds in the game editor.</summary>
public class CategoryChoiceGroup
{
	/// <summary>Null for the categories without a main category.</summary>
	public CategoryGroup? Group { get; init; }

	/// <summary>The categories in this main category, each with whether it's selected.</summary>
	public required List<CategoryChoice> Choices { get; init; }

	/// <summary>The main category's name; "Other categories" for categories without one.</summary>
	public string Name => Group?.ShownName ?? L.T("Other categories");

	/// <summary>A game needs at least one category from every main category that has any.</summary>
	public bool IsRequired => Group is not null && Choices.Count > 0;

	/// <summary>The heading: the name, with "(at least one)" when a game needs a category from it.</summary>
	public string Title => IsRequired ? L.F("{0} (at least one)", Name) : L.F("{0} (optional)", Name);

	/// <summary>Whether a category in it is selected.</summary>
	public bool HasSelection => Choices.Any(c => c.IsSelected);
}
