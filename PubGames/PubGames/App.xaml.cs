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
		InitializeComponent();
		_services = services;
		_auth = auth;
		_team = team;

		// Sign-in, restored session and sign-out all land here, so the root page always matches auth state.
		_auth.SignedInChanged += (_, _) => MainThread.BeginInvokeOnMainThread(async () => await ShowRootPageAsync());
	}

	/// <summary>Always start on the login page; it restores a saved session and hands over to the shell.</summary>
	protected override Window CreateWindow(IActivationState? activationState) =>
		new(_services.GetRequiredService<LoginPage>());

	/// <summary>
	/// Once per account: many pub games involve alcohol, so the app is for
	/// adults (18+) only, and asks to drink responsibly. Not confirmed: the
	/// player is signed out again.
	/// </summary>
	private async Task<bool> ConfirmAdultAsync(Page? page)
	{
		var key = $"adultConfirmed:{_auth.AccountId}";
		if (Preferences.Default.Get(key, false) || page is null) return true;
		var confirmed = await page.DisplayAlertAsync("Pub Games is for adults",
			"Many games in Pub Games involve alcohol. You must be 18 or older to use the app.\n\nDrink responsibly, take water in between, and never drink and drive.",
			"I'm 18 or older", "Sign out");
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
			window.Page = _services.GetRequiredService<LoginPage>();
			return;
		}

		if (!await ConfirmAdultAsync(window.Page))
		{
			await _auth.SignOutAsync();
			return;
		}

		var me = await _team.RefreshMyMembershipAsync();
		window.Page = new AppShell(me);
	}
}
