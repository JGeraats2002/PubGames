using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PubGames.Models;

/// <summary>
/// One sentence (or picture) of a translation, as the cloud keeps it (functions/shared.js, planSegments): the
/// original sentence, what stood in front of it, its translation and whether a person has looked at it.
/// </summary>
public sealed record TranslationSegment
{
	/// <summary>Status: translated by the cloud, nobody checked it yet.</summary>
	public const string Auto = "auto";
	/// <summary>Status: a person said the translation is right.</summary>
	public const string Approved = "approved";
	/// <summary>Status: a person corrected it (counts as checked).</summary>
	public const string Edited = "edited";
	/// <summary>Status: a person had checked it, but the original sentence changed since.</summary>
	public const string Review = "review";
	/// <summary>
	/// Status: a person added this sentence in this language. After saving, the cloud puts it (translated) into
	/// the original and from there into every language; then it becomes Edited, with Origin = Added.
	/// </summary>
	public const string Added = "added";

	/// <summary>The sentence in the original, or a picture tag.</summary>
	public string Src { get; init; } = string.Empty;

	/// <summary>What stands in front of it: nothing, a space, a line break or an empty line.</summary>
	public string Sep { get; init; } = string.Empty;

	/// <summary>The sentence in this language (the picture tag for a picture).</summary>
	public string Text { get; init; } = string.Empty;

	/// <summary>auto, approved, edited or review.</summary>
	public string Status { get; init; } = Auto;

	/// <summary>For review: a fresh automatic translation of the changed sentence, to compare with.</summary>
	public string? Suggestion { get; init; }

	/// <summary>
	/// Added: the sentence was added in this language and put into the original by the cloud; someone who
	/// speaks the original's language still has to approve it there. Null otherwise.
	/// </summary>
	public string? Origin { get; init; }

	/// <summary>A picture rather than a sentence.</summary>
	[System.Text.Json.Serialization.JsonIgnore]
	public bool IsPicture => (Src.Length > 0 ? Src : Text).StartsWith("![", StringComparison.Ordinal);

	/// <summary>A person has checked this sentence for the original as it is now.</summary>
	[System.Text.Json.Serialization.JsonIgnore]
	public bool IsChecked => Status is Approved or Edited;

	/// <summary>As the cloud stores it.</summary>
	public Dictionary<string, object?> ToMap()
	{
		var map = new Dictionary<string, object?> { ["src"] = Src, ["sep"] = Sep, ["text"] = Text, ["status"] = Status };
		if (Suggestion is not null) map["suggestion"] = Suggestion;
		if (Origin is not null) map["origin"] = Origin;
		return map;
	}

	/// <summary>From the cloud; null when it isn't a segment.</summary>
	public static TranslationSegment? FromMap(object? value) => value is Dictionary<string, object?> fields
		? new TranslationSegment
		{
			Src = fields.GetValueOrDefault("src") as string ?? string.Empty,
			Sep = fields.GetValueOrDefault("sep") as string ?? string.Empty,
			Text = fields.GetValueOrDefault("text") as string ?? string.Empty,
			Status = fields.GetValueOrDefault("status") as string ?? Auto,
			Suggestion = fields.GetValueOrDefault("suggestion") as string,
			Origin = fields.GetValueOrDefault("origin") as string
		}
		: null;
}

/// <summary>Whether a person has checked a translation (see GameTranslation.CheckedHash).</summary>
public enum TranslationCheck
{
	/// <summary>Nobody has checked it yet (new, or translated automatically).</summary>
	NotChecked,

	/// <summary>Checked against the original as it is now.</summary>
	Checked,

	/// <summary>Checked once, but the original has changed since.</summary>
	CheckAgain
}

/// <summary>
/// A game's name and rules in one other language. Made by the cloud (functions/index.js, translateGame) the
/// first time a game is saved and again whenever its name or rules change. A team member can correct it by
/// hand; then it's Manual and the cloud leaves it alone.
/// </summary>
public sealed record GameTranslation
{
	/// <summary>The game's name in this language.</summary>
	public string Name { get; init; } = string.Empty;

	/// <summary>The rules in this language, with the same picture tags as the original.</summary>
	public string RulesText { get; init; } = string.Empty;

	/// <summary>
	/// The text in pictures made with Create an image, translated: per picture id (the part after "fsimg:"),
	/// the texts in the order they're on the drawing.
	/// </summary>
	public Dictionary<string, List<string>> ImageTexts { get; init; } = [];

	/// <summary>
	/// The translation per sentence, with a status per sentence (made by the cloud since translator version 3).
	/// RulesText is these put together. Empty for older translations.
	/// </summary>
	public List<TranslationSegment> Segments { get; init; } = [];

	/// <summary>Corrected by hand: the cloud doesn't translate it again.</summary>
	public bool Manual { get; init; }

	/// <summary>Which version of the original this is a translation of (see HashOf).</summary>
	public string SourceHash { get; init; } = string.Empty;

	/// <summary>
	/// Checked by a person: the SourceHash of the original it was checked against (Translation is fine, or
	/// corrected by hand). When the original changes since, it has to be looked at again. Empty: not checked.
	/// </summary>
	public string CheckedHash { get; init; } = string.Empty;

	/// <summary>Who checked it last (their name or email).</summary>
	public string CheckedBy { get; init; } = string.Empty;

	/// <summary>How far a person has checked this translation against the original (see CheckedHash).</summary>
	public TranslationCheck CheckFor(string originalHash) =>
		CheckedHash.Length == 0 ? TranslationCheck.NotChecked
		: CheckedHash == originalHash ? TranslationCheck.Checked
		: TranslationCheck.CheckAgain;

	/// <summary>
	/// Which version of the cloud's translator made it (TRANSLATOR_VERSION in functions/index.js): older ones
	/// are made again the next time the game is saved. Kept as it is when the game is saved.
	/// </summary>
	public int Engine { get; init; }

	/// <summary>
	/// A short fingerprint of a game's name, rules and cover. When it differs from a translation's SourceHash,
	/// the original changed since. Must match sourceHash in functions/index.js.
	/// </summary>
	public static string HashOf(string name, string rulesText, string? cover) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{name}\u001F{rulesText}\u001F{cover}")))[..16].ToLowerInvariant();

	/// <summary>The translations stored as JSON on the phone, by language code.</summary>
	public static Dictionary<string, GameTranslation> FromJson(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return [];
		try
		{
			return JsonSerializer.Deserialize<Dictionary<string, GameTranslation>>(json) ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}

	/// <summary>Translations by language code, as JSON for the phone.</summary>
	public static string ToJson(Dictionary<string, GameTranslation> translations) => JsonSerializer.Serialize(translations);

	/// <summary>The translations as a cloud map: { nl: { name, rulesText, manual, sourceHash }, ... }.</summary>
	public static Dictionary<string, object?> ToMap(Dictionary<string, GameTranslation> translations) =>
		translations.ToDictionary(t => t.Key, t => (object?)new Dictionary<string, object?>
		{
			["name"] = t.Value.Name,
			["rulesText"] = t.Value.RulesText,
			["manual"] = t.Value.Manual,
			["imageTexts"] = t.Value.ImageTexts.ToDictionary(i => i.Key, i => (object?)i.Value),
			["sourceHash"] = t.Value.SourceHash,
			["engine"] = t.Value.Engine,
			["checkedHash"] = t.Value.CheckedHash,
			["checkedBy"] = t.Value.CheckedBy,
			["segments"] = t.Value.Segments.Select(segment => segment.ToMap()).ToList()
		});

	/// <summary>The translations from their cloud map; empty when there are none.</summary>
	public static Dictionary<string, GameTranslation> FromMap(object? value)
	{
		if (value is not Dictionary<string, object?> map) return [];
		var result = new Dictionary<string, GameTranslation>();
		foreach (var (code, entry) in map)
		{
			if (entry is not Dictionary<string, object?> fields) continue;
			result[code] = new GameTranslation
			{
				Name = fields.GetValueOrDefault("name") as string ?? string.Empty,
				RulesText = fields.GetValueOrDefault("rulesText") as string ?? string.Empty,
				Manual = fields.GetValueOrDefault("manual") as bool? ?? false,
				ImageTexts = fields.GetValueOrDefault("imageTexts") is Dictionary<string, object?> images
					? images.ToDictionary(i => i.Key, i => Services.FirestoreClient.AsStringList(i.Value))
					: [],
				SourceHash = fields.GetValueOrDefault("sourceHash") as string ?? string.Empty,
				Engine = fields.GetValueOrDefault("engine") is long engine ? (int)engine : 0,
				CheckedHash = fields.GetValueOrDefault("checkedHash") as string ?? string.Empty,
				CheckedBy = fields.GetValueOrDefault("checkedBy") as string ?? string.Empty,
				Segments = fields.GetValueOrDefault("segments") is List<object?> segments
					? segments.Select(TranslationSegment.FromMap).OfType<TranslationSegment>().ToList()
					: []
			};
		}
		return result;
	}

	/// <summary>A category's name per language, from its cloud map { nl: "Kaarten", ... }.</summary>
	public static Dictionary<string, string> NamesFromMap(object? value) =>
		value is Dictionary<string, object?> map
			? map.Where(n => n.Value is string).ToDictionary(n => n.Key, n => (string)n.Value!)
			: [];

	/// <summary>Names per language as JSON for the phone.</summary>
	public static string NamesToJson(Dictionary<string, string> names) => JsonSerializer.Serialize(names);

	/// <summary>Names per language from their JSON on the phone.</summary>
	public static Dictionary<string, string> NamesFromJson(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return [];
		try
		{
			return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}
}
