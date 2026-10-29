// Stand-ins for the parts of the app the tested files use but that need a phone (MAUI, the cloud). They
// behave like the real ones as far as the tests go; the real ones are in PubGames/PubGames/Services.
namespace PubGames.Services;

/// <summary>The app's texts (see Services/L.cs): here a dictionary the tests fill in for "the app's language".</summary>
public static class L
{
	/// <summary>The translations of the language that's set; empty is English.</summary>
	public static Dictionary<string, string> Texts { get; set; } = [];

	/// <summary>The text in the language that's set, or the text itself.</summary>
	public static string T(string text) => Texts.TryGetValue(text, out var translated) ? translated : text;

	/// <summary>The same, with values filled in.</summary>
	public static string F(string text, params object?[] values) => string.Format(T(text), values);

	/// <summary>A translation made by hand, or null (see L.Known in the app).</summary>
	public static string? Known(string key) => Texts.TryGetValue(key, out var translated) ? translated : null;
}

/// <summary>A language of the app (see Services/AppLanguage.cs).</summary>
public sealed record AppLanguageInfo(string Code, string EnglishName)
{
	/// <summary>Its name in the language that's set.</summary>
	public string Name => L.T(EnglishName);
}

/// <summary>The app's languages (see Services/AppLanguage.cs); the tests set Current.</summary>
public static class AppLanguage
{
	/// <summary>The language the app is in.</summary>
	public static AppLanguageInfo Current { get; set; } = new("en", "English");

	/// <summary>Switches the "app" to a language with these translations.</summary>
	public static void Use(string code, Dictionary<string, string> texts)
	{
		Current = new(code, code);
		L.Texts = texts;
	}
}

/// <summary>The cloud (see Services/FirestoreClient.cs): only what the tested files use.</summary>
public static class FirestoreClient
{
	/// <summary>An array field of strings; empty when missing.</summary>
	public static List<string> AsStringList(object? value) =>
		value is List<object?> items ? items.OfType<string>().ToList() : [];
}

/// <summary>The image store (see Services/ImageStore.cs): only its reference prefix.</summary>
public static class FirestoreImageStore
{
	/// <summary>A game image's reference starts with this.</summary>
	public const string Scheme = "fsimg:";
}
