using System.Text.Json;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.Tests;

/// <summary>The examples in tests/shared-cases.json, which the Cloud Functions are tested with as well.</summary>
public static class SharedCases
{
	/// <summary>The file, read once.</summary>
	public static JsonElement Root { get; } =
		JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shared-cases.json"))).RootElement;
}

/// <summary>Game translations: fingerprints, check status and storing them.</summary>
public class TranslationTests
{
	[Fact]
	public void HashOf_matches_the_cloud()
	{
		foreach (var example in SharedCases.Root.GetProperty("sourceHash").EnumerateArray())
		{
			var cover = example.GetProperty("cover").ValueKind == JsonValueKind.Null ? null : example.GetProperty("cover").GetString();
			Assert.Equal(example.GetProperty("hash").GetString(),
				GameTranslation.HashOf(example.GetProperty("name").GetString()!, example.GetProperty("rulesText").GetString()!, cover));
		}
	}

	[Fact]
	public void HashOf_changes_with_name_rules_and_cover()
	{
		var hash = GameTranslation.HashOf("Toepen", "Rules", "fsimg:a");
		Assert.NotEqual(hash, GameTranslation.HashOf("Toepen!", "Rules", "fsimg:a"));
		Assert.NotEqual(hash, GameTranslation.HashOf("Toepen", "Rules.", "fsimg:a"));
		Assert.NotEqual(hash, GameTranslation.HashOf("Toepen", "Rules", "fsimg:b"));
	}

	[Fact]
	public void CheckFor_tells_not_checked_checked_and_check_again()
	{
		Assert.Equal(TranslationCheck.NotChecked, new GameTranslation().CheckFor("h1"));
		Assert.Equal(TranslationCheck.Checked, new GameTranslation { CheckedHash = "h1" }.CheckFor("h1"));
		Assert.Equal(TranslationCheck.CheckAgain, new GameTranslation { CheckedHash = "h1" }.CheckFor("h2"));
	}

	/// <summary>A translation with every field filled in.</summary>
	private static Dictionary<string, GameTranslation> Full() => new()
	{
		["fr"] = new GameTranslation
		{
			Name = "Toepen", RulesText = "Règles\n\n![image](fsimg:abc)", Manual = true, SourceHash = "s1", Engine = 2,
			CheckedHash = "s1", CheckedBy = "Jan", ImageTexts = new() { ["abc"] = ["Haut", "Bas", ""] }
		}
	};

	[Fact]
	public void Translations_survive_the_cloud()
	{
		var back = GameTranslation.FromMap(AsFirestoreReturnsIt(GameTranslation.ToMap(Full())))["fr"];
		var expected = Full()["fr"];

		Assert.Equal(expected.Name, back.Name);
		Assert.Equal(expected.RulesText, back.RulesText);
		Assert.Equal(expected.Manual, back.Manual);
		Assert.Equal(expected.SourceHash, back.SourceHash);
		Assert.Equal(expected.Engine, back.Engine);
		Assert.Equal(expected.CheckedHash, back.CheckedHash);
		Assert.Equal(expected.CheckedBy, back.CheckedBy);
		Assert.Equal(expected.ImageTexts["abc"], back.ImageTexts["abc"]);
	}

	[Fact]
	public void Sentences_survive_the_cloud_and_the_phone()
	{
		var translations = new Dictionary<string, GameTranslation>
		{
			["de"] = new GameTranslation
			{
				Name = "Toepen", RulesText = "Hallo. Welt", Engine = 3,
				Segments =
				[
					new() { Src = "Hoi.", Sep = "", Text = "Hallo.", Status = TranslationSegment.Edited },
					new() { Src = "Wereld", Sep = " ", Text = "Welt", Status = TranslationSegment.Review, Suggestion = "Die Welt" },
					new() { Src = "![image](fsimg:abc)", Sep = "\n\n", Text = "![image](fsimg:abc)", Status = TranslationSegment.Auto }
				]
			}
		};

		var fromCloud = GameTranslation.FromMap(AsFirestoreReturnsIt(GameTranslation.ToMap(translations)))["de"];
		var fromPhone = GameTranslation.FromJson(GameTranslation.ToJson(translations))["de"];
		foreach (var back in new[] { fromCloud, fromPhone })
		{
			Assert.Equal(3, back.Segments.Count);
			Assert.Equal(translations["de"].Segments, back.Segments);
			Assert.True(back.Segments[0].IsChecked);
			Assert.False(back.Segments[1].IsChecked);
			Assert.True(back.Segments[2].IsPicture);
			Assert.Equal("Die Welt", back.Segments[1].Suggestion);
		}
	}

	[Fact]
	public void A_segment_without_suggestion_is_stored_without_one()
	{
		Assert.False(new TranslationSegment { Src = "a" }.ToMap().ContainsKey("suggestion"));
	}

	[Fact]
	public void Translations_survive_the_phone()
	{
		var back = GameTranslation.FromJson(GameTranslation.ToJson(Full()))["fr"];
		Assert.Equal("s1", back.CheckedHash);
		Assert.Equal("Jan", back.CheckedBy);
		Assert.Equal(2, back.Engine);
		Assert.Equal(["Haut", "Bas", ""], back.ImageTexts["abc"]);
	}

	[Fact]
	public void Old_translations_without_the_new_fields_still_load()
	{
		var map = new Dictionary<string, object?>
		{
			["nl"] = new Dictionary<string, object?> { ["name"] = "Mexen", ["rulesText"] = "x", ["manual"] = false, ["sourceHash"] = "s" }
		};
		var back = GameTranslation.FromMap(map)["nl"];
		Assert.Equal(0, back.Engine);
		Assert.Equal(TranslationCheck.NotChecked, back.CheckFor("s"));
		Assert.Empty(back.ImageTexts);
	}

	[Fact]
	public void Broken_json_gives_no_translations()
	{
		Assert.Empty(GameTranslation.FromJson("{not json"));
		Assert.Empty(GameTranslation.FromJson(null));
	}

	/// <summary>
	/// What FirestoreClient hands back for a map the app wrote: lists come back as List&lt;object?&gt; and whole
	/// numbers as long.
	/// </summary>
	private static object? AsFirestoreReturnsIt(object? value) => value switch
	{
		IDictionary<string, object?> map => map.ToDictionary(e => e.Key, e => AsFirestoreReturnsIt(e.Value)),
		string text => text,
		IEnumerable<IDictionary<string, object?>> maps => maps.Select(AsFirestoreReturnsIt).ToList(),
		IEnumerable<string> list => list.Select(item => (object?)item).ToList(),
		int number => (long)number,
		_ => value
	};
}

/// <summary>Pictures made with Create image, shown with translated texts.</summary>
public class TranslatedPictureTests
{
	[Fact]
	public void Pictures_with_texts_point_to_their_translated_version()
	{
		var texts = new Dictionary<string, List<string>> { ["abc"] = ["Hoch", "Niedrig"] };
		var rules = TranslatedPictures.Translate("Text\n\n![image](fsimg:abc)\n\n![image](fsimg:other)", "de", texts);

		Assert.Contains("![image](fsimg:abc@de)", rules);
		Assert.Contains("![image](fsimg:other)", rules);
		Assert.Equal(["Hoch", "Niedrig"], TranslatedPictures.TextsFor("abc@de"));
		Assert.Null(TranslatedPictures.TextsFor("other@de"));
	}

	[Fact]
	public void A_picture_is_never_translated_twice()
	{
		var texts = new Dictionary<string, List<string>> { ["abc"] = ["Hoch"] };
		Assert.Equal("fsimg:abc@de", TranslatedPictures.Translate("fsimg:abc@de", "de", texts));
	}

	[Fact]
	public void Without_texts_nothing_changes()
	{
		Assert.Equal("![image](fsimg:abc)", TranslatedPictures.Translate("![image](fsimg:abc)", "de", new Dictionary<string, List<string>>()));
	}

	[Fact]
	public void A_preview_changes_with_its_texts()
	{
		var first = TranslatedPictures.Preview("abc", ["Hoch"]);
		Assert.Equal(first, TranslatedPictures.Preview("abc", ["Hoch"]));
		Assert.NotEqual(first, TranslatedPictures.Preview("abc", ["Hoch!"]));
		Assert.StartsWith("fsimg:abc@preview", first);
	}

	[Fact]
	public void Rules_with_translated_pictures_still_split_into_text_and_pictures()
	{
		var blocks = RulesBlock.Parse("Deal.\n\n![image](fsimg:abc@nl)\n\nPlay.");
		Assert.Equal(3, blocks.Count);
		Assert.Equal("fsimg:abc@nl", blocks[1].ImageRef);
	}
}

/// <summary>The middle-finger rule (also in the Cloud Functions).</summary>
public class DrawRulesTests
{
	[Fact]
	public void Rude_hands_are_recognised_like_in_the_cloud()
	{
		foreach (var example in SharedCases.Root.GetProperty("rudeHand").EnumerateArray())
		{
			var fingers = example.GetProperty("fingers").EnumerateArray().Select(f => f.GetBoolean()).ToArray();
			var rude = DrawRules.IsRudeHand((DrawKind)example.GetProperty("kind").GetInt32(), example.GetProperty("neutral").GetBoolean(), fingers);
			Assert.True(rude == example.GetProperty("rude").GetBoolean(), example.GetProperty("about").GetString());
		}
	}

	[Fact]
	public void A_hand_without_fingers_is_not_rude()
	{
		Assert.False(DrawRules.IsRudeHand(DrawKind.Hand, false, null));
		Assert.False(DrawRules.IsRudeHand(DrawKind.Hand, false, new bool[3]));
	}

	[Fact]
	public void The_hand_kind_has_the_number_the_cloud_expects()
	{
		// functions/shared.js checks Kind === 4 in the saved drawing.
		Assert.Equal(4, (int)DrawKind.Hand);
		Assert.Equal(1, (int)DrawKind.Text);
	}
}

/// <summary>Sentences cut and put together the same way as in the cloud.</summary>
public class SentenceTests
{
	[Fact]
	public void Rules_are_cut_like_in_the_cloud()
	{
		foreach (var example in SharedCases.Root.GetProperty("segments").EnumerateArray())
		{
			var segments = Sentences.Of(example.GetProperty("rules").GetString()!);
			Assert.Equal(example.GetProperty("src").EnumerateArray().Select(e => e.GetString()), segments.Select(s => s.Src));
			Assert.Equal(example.GetProperty("sep").EnumerateArray().Select(e => e.GetString()), segments.Select(s => s.Sep));
		}
	}

	[Fact]
	public void Emptied_sentences_are_left_out_like_in_the_cloud()
	{
		foreach (var example in SharedCases.Root.GetProperty("join").EnumerateArray())
		{
			var texts = example.GetProperty("texts").EnumerateArray().Select(e => e.GetString()!).ToList();
			var seps = example.GetProperty("sep").EnumerateArray().Select(e => e.GetString()!).ToList();
			var segments = texts.Select((text, index) => new TranslationSegment { Src = text, Sep = seps[index], Text = text });
			Assert.Equal(example.GetProperty("joined").GetString(), Sentences.Join(segments));
		}
	}

	[Fact]
	public void Rules_put_together_again_read_the_same()
	{
		const string rules = "Deal 4 cards. Play one!\nThe highest wins.\n\n![image](fsimg:abc)\n\nDone.";
		Assert.Equal(rules, Sentences.Join(Sentences.Of(rules)));
	}
}
