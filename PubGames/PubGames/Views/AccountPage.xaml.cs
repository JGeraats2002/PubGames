using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>The Account tab's main page: Settings, with a row per heading.</summary>
public partial class AccountPage : ContentPage
{
	private readonly AccountViewModel _vm;

	public AccountPage(AccountViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Account(_vm.Me));
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	private async void OnProfileTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(nameof(ProfilePage));

	private async void OnStatsTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(nameof(MyStatsPage));

	private async void OnPassTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(nameof(SubscriptionPage));

	private async void OnBlockedTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(nameof(BlockedPlayersPage));

	private async void OnFeedbackTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(nameof(FeedbackPage));

	private async void OnSignOutClicked(object? sender, EventArgs e)
	{
		if (await DisplayAlertAsync("Sign out?", "You can sign in again with any Google account.", "Sign out", "Cancel"))
			await _vm.SignOutCommand.ExecuteAsync(null);
	}
}
