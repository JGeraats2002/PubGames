using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class AccountPage : ContentPage
{
	private readonly AccountViewModel _vm;

	public AccountPage(AccountViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Account(_vm.Me));
	}

	private async void OnStatsTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(nameof(MyStatsPage));

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}
}
