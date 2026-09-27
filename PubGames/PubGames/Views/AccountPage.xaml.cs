using PubGames.ViewModels;

namespace PubGames.Views;

public partial class AccountPage : ContentPage
{
	private readonly AccountViewModel _vm;

	public AccountPage(AccountViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_vm.Load();
	}
}
