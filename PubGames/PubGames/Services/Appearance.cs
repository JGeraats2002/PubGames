namespace PubGames.Services;

/// <summary>How the app looks: like the phone (light or dark, whatever the phone is set to), always light, or always dark.</summary>
public enum AppearanceChoice
{
	/// <summary>Follows the phone's own dark mode setting; the default.</summary>
	Phone,

	/// <summary>Always light.</summary>
	Light,

	/// <summary>Always dark.</summary>
	Dark
}

/// <summary>
/// Light or dark. Like the phone by default, as Android and Material Design advise; chosen per account under
/// Account &gt; Appearance (several people can sign in on one phone), like the language. Signed out, the choice
/// last made on this phone is used. The colours in App.xaml are the light ones; when the app is dark they're
/// swapped for the dark ones below, and pages pick that up by using them as DynamicResource. Pictures keep
/// their own colours.
/// </summary>
public static class Appearance
{
	private const string PreferenceKey = "appearance";

	/// <summary>Every colour that differs between light and dark: its key in App.xaml, the light and the dark value.</summary>
	private static readonly (string Key, string Light, string Dark)[] Palette =
	[
		("Primary", "#3C3489", "#B3ADF7"),
		("PrimaryDark", "#26215C", "#D6D2FB"),
		("OnPrimary", "#FFFFFF", "#1B1838"),
		("Accent", "#0F6E56", "#6FD4B2"),
		("Danger", "#A32D2D", "#F4A3A3"),
		("Warning", "#854F0B", "#F2B866"),
		("PageBackground", "#FFFFFF", "#1C1B1F"),
		("SurfaceLight", "#FFFFFF", "#24232A"),
		("SurfaceMuted", "#F1EFE8", "#302F36"),
		("TextPrimary", "#2C2C2A", "#ECEAE4"),
		("TextSecondary", "#5F5E5A", "#B9B7AF"),
		("Border", "#D3D1C7", "#4D4C55"),
		("ScoreWarningTint", "#FFF1CC", "#4A3B12"),
		("ScoreLimit", "#0072B2", "#6CC0F0")
	];

	/// <summary>The choice in use now.</summary>
	public static AppearanceChoice Current { get; private set; } = AppearanceChoice.Phone;

	/// <summary>The signed-in account whose choice is used, or null when signed out (see UseAccount).</summary>
	private static string? _accountId;

	/// <summary>The choices in the order Account &gt; Appearance shows them.</summary>
	public static IReadOnlyList<AppearanceChoice> All { get; } = [AppearanceChoice.Phone, AppearanceChoice.Light, AppearanceChoice.Dark];

	/// <summary>A choice's name in the app's language.</summary>
	public static string Name(AppearanceChoice choice) => choice switch
	{
		AppearanceChoice.Light => L.T("Light mode"),
		AppearanceChoice.Dark => L.T("Dark mode"),
		_ => L.T("Same as phone")
	};

	/// <summary>Raised after another choice was made, so the screens can be built again in it.</summary>
	public static event EventHandler? Changed;

	/// <summary>Where an account's choice is remembered on this phone.</summary>
	private static string AccountKey(string accountId) => $"{PreferenceKey}:{accountId}";

	/// <summary>
	/// At app start, before any page is made: the choice last made on this phone, and from then on the colours
	/// follow the phone whenever it switches between light and dark.
	/// </summary>
	public static void Initialize(Application app)
	{
		Current = Read(PreferenceKey);
		app.RequestedThemeChanged += (_, _) => MainThread.BeginInvokeOnMainThread(ApplyColors);
		Apply();
	}

	/// <summary>Someone signed in (or out, with null): from now on their choice is used, when they made one on this phone.</summary>
	public static void UseAccount(string? accountId)
	{
		_accountId = accountId;
		if (accountId is null) return;
		var choice = Read(AccountKey(accountId));
		if (choice == Current) return;
		Current = choice;
		Apply();
	}

	/// <summary>Uses and remembers this choice, for this account and as the phone's last choice; the screens are built again in it.</summary>
	public static void Set(AppearanceChoice choice)
	{
		try
		{
			Preferences.Default.Set(PreferenceKey, choice.ToString());
			if (_accountId is { } accountId)
				Preferences.Default.Set(AccountKey(accountId), choice.ToString());
		}
		catch (Exception) { }
		if (choice == Current) return;
		Current = choice;
		Apply();
		Changed?.Invoke(null, EventArgs.Empty);
	}

	/// <summary>A remembered choice; like the phone when there's none.</summary>
	private static AppearanceChoice Read(string key)
	{
		try { return Enum.TryParse<AppearanceChoice>(Preferences.Default.Get<string?>(key, null), out var choice) ? choice : AppearanceChoice.Phone; }
		catch (Exception) { return AppearanceChoice.Phone; }
	}

	/// <summary>Tells Android the choice (so its own dialogs and keyboards match) and sets the colours.</summary>
	private static void Apply()
	{
		if (Application.Current is not { } app) return;
		app.UserAppTheme = Current switch
		{
			AppearanceChoice.Light => AppTheme.Light,
			AppearanceChoice.Dark => AppTheme.Dark,
			_ => AppTheme.Unspecified
		};
		ApplyColors();
	}

	/// <summary>Puts the light or dark colours in the app's resources; pages using them as DynamicResource follow.</summary>
	private static void ApplyColors()
	{
		if (Application.Current is not { } app) return;
		var dark = app.RequestedTheme == AppTheme.Dark;
		foreach (var (key, light, darkValue) in Palette)
			app.Resources[key] = Color.FromArgb(dark ? darkValue : light);
	}
}
