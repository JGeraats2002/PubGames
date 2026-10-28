using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PubGames.Models;

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
			["checkedBy"] = t.Value.CheckedBy
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
				CheckedBy = fields.GetValueOrDefault("checkedBy") as string ?? string.Empty
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
