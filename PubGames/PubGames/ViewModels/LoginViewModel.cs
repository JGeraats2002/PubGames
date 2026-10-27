using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// The sign-in page: signing in with Google, or straight on when a saved session is restored.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
	private readonly IAuthService _auth;

	/// <summary>True while checking for a saved session on startup - hides the sign-in button meanwhile.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowSignIn))]
	private bool isRestoring = true;

	/// <summary>Signing in: the sign-in button hides and a spinner shows.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowSignIn))]
	[NotifyCanExecuteChangedFor(nameof(SignInCommand))]
	private bool isBusy;

	/// <summary>Why signing in didn't work.</summary>
	[ObservableProperty]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	/// <summary>The sign-in button shows when nothing is being restored or signed in.</summary>
	public bool ShowSignIn => !IsRestoring && !IsBusy;

	/// <summary>
	/// Gets the services it works with - the sign-in - from the app (dependency injection).
	/// </summary>
	public LoginViewModel(IAuthService auth)
	{
		_auth = auth;
	}

	/// <summary>
	/// A successful restore raises IAuthService.SignedInChanged, and App swaps
	/// this page out for the shell - so there's nothing to do here on success.
	/// </summary>
	public async Task LoadAsync()
	{
		IsRestoring = true;
		await _auth.InitializeAsync();
		IsRestoring = _auth.IsSignedIn;
	}

	/// <summary>Signing in can start when nothing is busy.</summary>
	private bool CanSignIn() => !IsBusy;

	/// <summary>
	/// Signs in with the Google account the user picks; the app then shows the main screens.
	/// </summary>
	[RelayCommand(CanExecute = nameof(CanSignIn))]
	private async Task SignInAsync()
	{
		IsBusy = true;
		StatusMessage = string.Empty;
		try
		{
			await _auth.SignInWithGoogleAsync();
		}
		catch (OperationCanceledException)
		{
			// User closed the account picker - not an error.
		}
		catch (Exception ex)
		{
			StatusMessage = ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}
}
