using PubGames.ViewModels;

namespace PubGames.Views;

public partial class LoginPage : ContentPage
{
	private readonly LoginViewModel _vm;

	public LoginPage(LoginViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}
}
