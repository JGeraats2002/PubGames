using PubGames.Models;
using PubGames.Services;

namespace PubGames.Tests;

/// <summary>Category names in the app's language, and lists that stay alphabetical after translation.</summary>
[Collection("App language")]
public class CategoryOrderTests : IDisposable
{
	/// <summary>Every test starts in Dutch, with the hand-made category translations.</summary>
	public CategoryOrderTests() => AppLanguage.Use("nl", new()
	{
		["Category: Cards"] = "Kaarten", ["Category: Dice"] = "Dobbelstenen", ["Category: Hands"] = "Handen",
		["Category: Mouth"] = "Mond", ["Category: Other"] = "Overig",
		["Category: Easy"] = "Makkelijk", ["Category: Medium"] = "Gemiddeld", ["Category: Hard"] = "Moeilijk"
	});

	/// <summary>Back to English for whatever runs next.</summary>
	public void Dispose() => AppLanguage.Use("en", []);

	/// <summary>Categories in a group, in this order as the admins gave it.</summary>
	private static List<GameCategory> InGroup(string group, params string[] names) =>
		names.Select((name, index) => new GameCategory { Id = $"{group}-{name}", Name = name, GroupId = group, SortOrder = index }).ToList();

	[Fact]
	public void An_alphabetical_list_is_sorted_again_by_the_translated_names()
	{
		var shown = CategoryGroup.ShownOrder(InGroup("play", "Cards", "Dice", "Hands", "Mouth", "Other"), c => c.Name, c => c.ShownName);
		Assert.Equal(["Dobbelstenen", "Handen", "Kaarten", "Mond", "Overig"], shown.Select(c => c.ShownName));
	}

	[Fact]
	public void Other_stays_last()
	{
		AppLanguage.Use("fr", new() { ["Category: Cards"] = "Cartes", ["Category: Dice"] = "Dés", ["Category: Other"] = "Autre" });
		var shown = CategoryGroup.ShownOrder(InGroup("play", "Cards", "Dice", "Other"), c => c.Name, c => c.ShownName);
		Assert.Equal(["Cartes", "Dés", "Autre"], shown.Select(c => c.ShownName));
	}

	[Fact]
	public void An_order_of_its_own_is_kept()
	{
		var shown = CategoryGroup.ShownOrder(InGroup("difficulty", "Easy", "Medium", "Hard"), c => c.Name, c => c.ShownName);
		Assert.Equal(["Makkelijk", "Gemiddeld", "Moeilijk"], shown.Select(c => c.ShownName));
	}

	[Fact]
	public void The_categories_page_keeps_the_admins_order()
	{
		var groups = new[] { new CategoryGroup { Id = "play", Name = "Play style" } };
		var arranged = CategoryGroup.Arrange(groups, InGroup("play", "Cards", "Dice", "Hands"));
		Assert.Equal(["Cards", "Dice", "Hands"], arranged[0].Categories.Select(c => c.Name));

		var forPlayers = CategoryGroup.Arrange(groups, InGroup("play", "Cards", "Dice", "Hands"), inAppLanguage: true);
		Assert.Equal(["Dobbelstenen", "Handen", "Kaarten"], forPlayers[0].Categories.Select(c => c.ShownName));
	}

	[Fact]
	public void A_hand_made_translation_goes_before_the_clouds()
	{
		var cards = new GameCategory { Name = "Cards", TranslationsText = GameTranslation.NamesToJson(new() { ["nl"] = "Kaartspel" }) };
		Assert.Equal("Kaarten", cards.ShownName);
	}

	[Fact]
	public void A_new_category_uses_the_clouds_translation_or_its_name()
	{
		var bluff = new GameCategory { Name = "Bluff", TranslationsText = GameTranslation.NamesToJson(new() { ["nl"] = "Bluffen" }) };
		Assert.Equal("Bluffen", bluff.ShownName);
		Assert.Equal("Quiz", new GameCategory { Name = "Quiz" }.ShownName);
	}
}
