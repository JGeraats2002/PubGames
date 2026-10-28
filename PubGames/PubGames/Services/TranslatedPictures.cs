using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace PubGames.Services;

/// <summary>
/// Pictures made with Create image, shown with their text in another language. A game in the app's language
/// (PubGame.InAppLanguage) points to "fsimg:ID@nl" instead of "fsimg:ID"; the image store then draws the
/// picture again from its drawing, with the translated texts it finds here.
/// </summary>
public static partial class TranslatedPictures
{
	/// <summary>The translated texts per "ID@language".</summary>
	private static readonly ConcurrentDictionary<string, List<string>> Texts = new();

	/// <summary>
	/// Points every picture in a text (rules or a cover reference) that has translated texts to its translated
	/// version, and remembers those texts for drawing it.
	/// </summary>
	/// <param name="text">The rules or the cover reference.</param>
	/// <param name="language">The language code, e.g. nl.</param>
	/// <param name="pictureTexts">The translated texts per picture id.</param>
	public static string Translate(string text, string language, IReadOnlyDictionary<string, List<string>> pictureTexts)
	{
		if (pictureTexts.Count == 0 || string.IsNullOrEmpty(text)) return text;
		return Reference().Replace(text, match =>
		{
			var id = match.Groups[1].Value;
			if (!pictureTexts.TryGetValue(id, out var texts) || texts.Count == 0) return match.Value;
			Texts[$"{id}@{language}"] = texts;
			return $"{match.Value}@{language}";
		});
	}

	/// <summary>
	/// A picture shown with other texts, for the Languages page while they're being corrected: a reference of
	/// its own for every set of texts, so the picture is drawn again when a text changes.
	/// </summary>
	/// <param name="id">The picture's id (after "fsimg:").</param>
	/// <param name="texts">The texts, in their order on the drawing.</param>
	public static string Preview(string id, IReadOnlyList<string> texts)
	{
		var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
			System.Text.Encoding.UTF8.GetBytes(string.Join("", texts))))[..12].ToLowerInvariant();
		var key = $"{id}@preview{fingerprint}";
		Texts[key] = texts.ToList();
		return FirestoreImageStore.Scheme + key;
	}

	/// <summary>The translated texts for "ID@language", or null when there are none (then the picture shows as it is).</summary>
	public static List<string>? TextsFor(string idAndLanguage) => Texts.GetValueOrDefault(idAndLanguage);

	/// <summary>A picture reference without a language behind it: fsimg:ID.</summary>
	[GeneratedRegex(@"fsimg:([A-Za-z0-9_]+)(?![A-Za-z0-9_@])")]
	private static partial Regex Reference();
}
