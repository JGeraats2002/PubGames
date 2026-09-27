using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>Profile of the signed-in user. Signing in happens on LoginPage; this page is only reachable once signed in.</summary>
public partial class AccountViewModel : ObservableObject
{
	private readonly IAuthService _auth;

	[ObservableProperty]
	private bool isAdmin;

	[ObservableProperty]
	private string displayName = string.Empty;

	[ObservableProperty]
	private string email = string.Empty;

	public AccountViewModel(IAuthService auth)
	{
		_auth = auth;
	}

	public void Load()
	{
		var user = _auth.CurrentUser;
		IsAdmin = user?.IsAdmin ?? false;
		DisplayName = user?.DisplayName ?? string.Empty;
		Email = user?.Email ?? string.Empty;
	}

	/// <summary>App reacts to the sign-out and returns to LoginPage.</summary>
	[RelayCommand]
	private Task SignOutAsync() => _auth.SignOutAsync();
}
