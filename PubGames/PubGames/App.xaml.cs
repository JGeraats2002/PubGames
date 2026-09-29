using PubGames.Services;
using PubGames.Views;

namespace PubGames;

public partial class App : Application
{
	private readonly IServiceProvider _services;
	private readonly IAuthService _auth;
	private readonly ITeamService _team;

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

	private async Task ShowRootPageAsync()
	{
		var window = Windows.FirstOrDefault();
		if (window is null) return;

		if (!_auth.IsSignedIn)
		{
			window.Page = _services.GetRequiredService<LoginPage>();
			return;
		}

		var me = await _team.RefreshMyMembershipAsync();
		window.Page = new AppShell(me);
	}
}
