using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace PubGames.Services;

/// <summary>
/// The app's texts in the language that's set (see AppLanguage). Texts in C# are written in English and
/// looked up by that English text: L.T("Save") or L.F("{0} is now an admin.", name). Texts in XAML use a short
/// key: {loc:T Team_AddModerator}; Resources/Strings/en.json gives their English text. A text that isn't
/// translated (yet) shows in English.
/// </summary>
public static class L
{
	/// <summary>English texts of the XAML keys.</summary>
	private static readonly Dictionary<string, string> English = Read("en");

	/// <summary>The texts of the language that's set, by English text or XAML key.</summary>
	private static Dictionary<string, string> _texts = [];

	/// <summary>Loads the texts of a language (en: none needed).</summary>
	public static void Load(string code) => _texts = code == "en" ? [] : Read(code);

	/// <summary>The text in the app's language.</summary>
	/// <param name="text">The English text, or a XAML key.</param>
	public static string T(string text) =>
		_texts.TryGetValue(text, out var translated) && translated.Length > 0 ? translated
		: English.TryGetValue(text, out var english) ? english
		: text;

	/// <summary>A text with values filled in ({0}, {1}, ...), in the app's language.</summary>
	/// <param name="text">The English text with {0}, {1}, ... where the values go.</param>
	/// <param name="values">The values, in order.</param>
	public static string F(string text, params object?[] values) =>
		string.Format(CultureInfo.CurrentCulture, T(text), values);

	/// <summary>A language's texts from Resources/Strings/{code}.json; empty when there's no such file.</summary>
	private static Dictionary<string, string> Read(string code)
	{
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"PubGames.Strings.{code}.json");
		if (stream is null) return [];
		return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
	}
}

/// <summary>A XAML text in the app's language: Text="{loc:T Team_AddModerator}".</summary>
[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
public class TExtension : IMarkupExtension<string>
{
	/// <summary>The text's key in Resources/Strings/en.json.</summary>
	public string Key { get; set; } = string.Empty;

	/// <inheritdoc />
	public string ProvideValue(IServiceProvider serviceProvider) => L.T(Key);

	/// <inheritdoc />
	object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
