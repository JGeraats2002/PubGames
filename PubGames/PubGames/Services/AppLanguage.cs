using System.Globalization;

namespace PubGames.Services;

/// <summary>
/// One language the app can be shown in.
/// </summary>
/// <param name="Code">Short code, also used for the game translations in the cloud: en, nl, fr, ...</param>
/// <param name="CultureName">The .NET culture for texts, dates and numbers, e.g. pt-PT.</param>
/// <param name="EnglishName">The language's English name; translated like any other text (Spanish, Spaans, ...).</param>
/// <param name="IsRightToLeft">Written from right to left (Arabic): the screens are mirrored.</param>
public sealed record AppLanguageInfo(string Code, string CultureName, string EnglishName, bool IsRightToLeft = false)
{
	/// <summary>The language's name in the language the app is in now ("Spaans" when the app is in Dutch).</summary>
	public string Name => L.T(EnglishName);

	/// <summary>
	/// The language's flag, drawn for a circle (Resources/Images/flag_*.svg). English is half American, half
	/// British; Chinese has China's flag, Arabic the United Arab Emirates', Portuguese Brazil's.
	/// </summary>
	public string Flag => $"flag_{Code switch { "zh" => "cn", "ar" => "ae", "pt" => "br", _ => Code }}.png";

	/// <summary>Screens in this language run right to left.</summary>
	public FlowDirection FlowDirection => IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
}

/// <summary>
/// The language the app is shown in: chosen per account (several people can sign in on one phone, each in
/// their own language), the first time that account signs in, and changed under Account &gt; Settings. Signed
/// out, the language last used on this phone shows; before any choice the phone's language when the app has
/// it, otherwise English.
/// </summary>
public static class AppLanguage
{
	private const string PreferenceKey = "appLanguage";

	/// <summary>Every language the app is translated into, in the order the language screen shows them.</summary>
	public static IReadOnlyList<AppLanguageInfo> All { get; } =
	[
		new("en", "en-GB", "English"),
		new("nl", "nl-NL", "Dutch"),
		new("fr", "fr-FR", "French"),
		new("de", "de-DE", "German"),
		new("es", "es-ES", "Spanish"),
		new("it", "it-IT", "Italian"),
		new("pt", "pt-BR", "Portuguese"),
		new("zh", "zh-CN", "Chinese"),
		// ar-AE rather than plain ar: that one uses the Hijri calendar for dates.
		new("ar", "ar-AE", "Arabic", IsRightToLeft: true)
	];

	/// <summary>The language the app is in now.</summary>
	public static AppLanguageInfo Current { get; private set; } = All[0];

	/// <summary>The signed-in account whose language is used, or null when signed out (see UseAccount).</summary>
	public static string? AccountId { get; private set; }

	/// <summary>Where an account's language is remembered on this phone.</summary>
	private static string AccountKey(string accountId) => $"{PreferenceKey}:{accountId}";

	/// <summary>
	/// The signed-in account has picked a language on this phone (otherwise the language screen shows after
	/// signing in, unless the team list knows it). Signed out: whether one was ever picked on this phone.
	/// </summary>
	public static bool IsChosen
	{
		get
		{
			try { return Preferences.Default.ContainsKey(AccountId is { } id ? AccountKey(id) : PreferenceKey); }
			catch (Exception) { return true; }
		}
	}

	/// <summary>
	/// Someone signed in (or out, with null): from now on the language is theirs. Switches to the language
	/// this account chose on this phone, when it did; otherwise the language stays as it is until it's
	/// chosen (IsChosen is false then). Doesn't rebuild the screens: the caller shows new ones.
	/// </summary>
	public static void UseAccount(string? accountId)
	{
		AccountId = accountId;
		if (accountId is null) return;
		string? code = null;
		try { code = Preferences.Default.Get<string?>(AccountKey(accountId), null); }
		catch (Exception) { }
		if (Find(code) is { } language && language != Current)
			Apply(language);
	}

	/// <summary>Raised after the language changed, so the screens can be built again in it.</summary>
	public static event EventHandler? Changed;

	/// <summary>At app start: the chosen language, or the phone's when it isn't chosen yet.</summary>
	public static void Initialize()
	{
		string? code = null;
		try { code = Preferences.Default.Get<string?>(PreferenceKey, null); }
		catch (Exception) { }
		Apply(Find(code) ?? FromPhone());
	}

	/// <summary>
	/// Switches the app to this language and remembers it. With notify, the screens are built again in it
	/// (Changed); the first language screen does that itself.
	/// </summary>
	public static void Set(AppLanguageInfo language, bool notify = true)
	{
		Preferences.Default.Set(PreferenceKey, language.Code);
		if (AccountId is { } accountId)
			Preferences.Default.Set(AccountKey(accountId), language.Code);
		Chosen?.Invoke(null, EventArgs.Empty);
		var changed = language != Current;
		Apply(language);
		if (changed && notify)
			Changed?.Invoke(null, EventArgs.Empty);
	}

	/// <summary>A language was chosen (also the same one again, and on the first language screen).</summary>
	public static event EventHandler? Chosen;

	/// <summary>Tells the app the language changed, so the screens are built again in it.</summary>
	public static void RaiseChanged() => Changed?.Invoke(null, EventArgs.Empty);

	/// <summary>Switches the texts to this language for a moment, without remembering it (the language screen shows it).</summary>
	public static void Preview(AppLanguageInfo language) => Apply(language);

	/// <summary>The language with this code, or null.</summary>
	public static AppLanguageInfo? Find(string? code) => All.FirstOrDefault(l => l.Code == code);

	/// <summary>The phone's language when the app has it, otherwise English.</summary>
	private static AppLanguageInfo FromPhone() =>
		Find(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName) ?? All[0];

	/// <summary>Sets the texts, dates and numbers to the language.</summary>
	private static void Apply(AppLanguageInfo language)
	{
		Current = language;
		var culture = CultureInfo.GetCultureInfo(language.CultureName);
		CultureInfo.DefaultThreadCurrentCulture = culture;
		CultureInfo.DefaultThreadCurrentUICulture = culture;
		CultureInfo.CurrentCulture = culture;
		CultureInfo.CurrentUICulture = culture;
		L.Load(language.Code);
	}
}
