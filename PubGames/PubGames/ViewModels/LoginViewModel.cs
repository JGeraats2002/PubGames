using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Services;

namespace PubGames.ViewModels;

public partial class LoginViewModel : ObservableObject
{
	private readonly IAuthService _auth;

	/// <summary>True while checking for a saved session on startup - hides the sign-in button meanwhile.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowSignIn))]
	private bool isRestoring = true;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowSignIn))]
	[NotifyCanExecuteChangedFor(nameof(SignInCommand))]
	private bool isBusy;

	[ObservableProperty]
	private string statusMessage = string.Empty;

	public bool ShowSignIn => !IsRestoring && !IsBusy;

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

	private bool CanSignIn() => !IsBusy;

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
