using PubGames.Services;
using PubGames.Views;

namespace PubGames;

public partial class App : Application
{
	private readonly IServiceProvider _services;
	private readonly IAuthService _auth;
	private readonly ILocalDatabaseService _local;
	private readonly IPermissionService _permissions;

	public App(IServiceProvider services, IAuthService auth, ILocalDatabaseService local, IPermissionService permissions)
	{
		InitializeComponent();
		_services = services;
		_auth = auth;
		_local = local;
		_permissions = permissions;

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

		var members = await _local.GetHostMembersAsync(AuthService.DefaultHostOrgId);
		var canHost = _permissions.ResolveMember(members, _auth.CurrentUser, AuthService.DefaultHostOrgId) is not null;
		window.Page = new AppShell(canHost);
	}
}
