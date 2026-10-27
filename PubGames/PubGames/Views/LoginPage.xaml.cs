using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>The sign-in page, shown when nobody is signed in; a saved session signs in by itself.</summary>
public partial class LoginPage : ContentPage
{
	private readonly LoginViewModel _vm;

	/// <summary>Sets up the sign-in page with its view model.</summary>
	public LoginPage(LoginViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	/// <summary>
	/// Every time the page shows: a saved session is restored, or the sign-in button shows.
	/// </summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	/// <summary>Opens the help about signing in.</summary>
	private async void OnInfoClicked(object? sender, EventArgs e) => await InfoButton.ShowAsync(this, Help.Login);
}
