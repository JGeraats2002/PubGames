using PubGames.Services;
using PubGames.Views;

namespace PubGames;

/// <summary>
/// The app itself: always starts on the sign-in page and switches to the main screens (AppShell) once signed
/// in - and back when signed out.
/// </summary>
public partial class App : Application
{
	private readonly IServiceProvider _services;
	private readonly IAuthService _auth;
	private readonly ITeamService _team;

	/// <summary>
	/// Starts the app: keeps the services it needs and shows the right page whenever someone signs in or out.
	/// </summary>
	public App(IServiceProvider services, IAuthService auth, ITeamService team)
	{
		// Before any page is made: every text is looked up in the app's language.
		AppLanguage.Initialize();
		InitializeComponent();
		_services = services;
		_auth = auth;
		_team = team;

		// Sign-in, restored session and sign-out all land here, so the root page always matches auth state.
		_auth.SignedInChanged += (_, _) => MainThread.BeginInvokeOnMainThread(async () => await ShowRootPageAsync());

		// Another language was chosen in Settings (the language page has already closed): every screen is
		// built again in it, back on Account.
		AppLanguage.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(async () =>
		{
			await ShowRootPageAsync();
			if (Windows.FirstOrDefault()?.Page is Shell shell)
				await shell.GoToAsync("//main/account");
		});
	}

	/// <summary>Always start on the login page; it restores a saved session and hands over to the shell.</summary>
	protected override Window CreateWindow(IActivationState? activationState) =>
		new(InLanguageDirection(_services.GetRequiredService<LoginPage>()));

	/// <summary>A page that runs right to left in a right-to-left language (Arabic), otherwise left to right.</summary>
	private static Page InLanguageDirection(Page page)
	{
		page.FlowDirection = AppLanguage.Current.FlowDirection;
		return page;
	}

	/// <summary>
	/// Once per account: many pub games involve alcohol, so the app is for
	/// adults (18+) only, and asks to drink responsibly. Not confirmed: the
	/// player is signed out again.
	/// </summary>
	private async Task<bool> ConfirmAdultAsync(Page? page)
	{
		var key = $"adultConfirmed:{_auth.AccountId}";
		if (Preferences.Default.Get(key, false) || page is null) return true;
		var confirmed = await page.DisplayAlertAsync(L.T("Pub Games is for adults"),
			L.T("Many games in Pub Games involve alcohol. You must be 18 or older to use the app.\n\nDrink responsibly, take water in between, and never drink and drive."),
			L.T("I'm 18 or older"), L.T("Sign out"));
		if (confirmed)
			Preferences.Default.Set(key, true);
		return confirmed;
	}

	/// <summary>
	/// Shows the page that matches the sign-in: the sign-in page when signed out, otherwise (after the 18+
	/// confirmation) the main screens with the tabs the user's role allows.
	/// </summary>
	private async Task ShowRootPageAsync()
	{
		var window = Windows.FirstOrDefault();
		if (window is null) return;

		if (!_auth.IsSignedIn)
		{
			AppLanguage.UseAccount(null);
			await SetRootPageAsync(window, _services.GetRequiredService<LoginPage>());
			return;
		}

		// Each account has its own language, also when several sign in on this phone.
		AppLanguage.UseAccount(_auth.AccountId);
		var me = await _team.RefreshMyMembershipAsync();

		// New on this phone: a team member's language comes from the team list (set on another phone);
		// otherwise it's chosen before anything else.
		if (!AppLanguage.IsChosen && me?.LanguageInfo is { } known)
			AppLanguage.Set(known, notify: false);
		if (!AppLanguage.IsChosen)
			await LanguagePage.ChooseFirstTimeAsync(window, _services);

		if (!await ConfirmAdultAsync(window.Page))
		{
			await _auth.SignOutAsync();
			return;
		}

		await SetRootPageAsync(window, new AppShell(me));
	}

	/// <summary>
	/// Makes a page the window's root, after the screen change that's going on (a dialog closing, a page
	/// being popped) has finished. Swapping right away can crash Android ("No view found for id").
	/// </summary>
	private static async Task SetRootPageAsync(Window window, Page page)
	{
		await Task.Delay(50);
		await window.Dispatcher.DispatchAsync(() => window.Page = InLanguageDirection(page));
	}
}
