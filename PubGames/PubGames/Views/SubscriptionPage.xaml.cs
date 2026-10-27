using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>Account > Pub Games Pass.</summary>
public partial class SubscriptionPage : ContentPage
{
	private readonly SubscriptionViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public SubscriptionPage(SubscriptionViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Subscription);
	}

	/// <summary>Every time the page shows: the pass's status and prices are loaded fresh.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	/// <summary>Buy the plan with Google Play.</summary>
	private async void OnPlanClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: PassPlanRow row })
			await _vm.BuyAsync(row);
	}

	/// <summary>Checks every purchase of this account with Google Play again.</summary>
	private async void OnRestoreClicked(object? sender, EventArgs e) => await _vm.RestoreAsync();

	/// <summary>Cancelling or changing the pass happens in Google Play's own subscription page.</summary>
	private async void OnManageClicked(object? sender, EventArgs e) =>
		await Launcher.Default.OpenAsync($"https://play.google.com/store/account/subscriptions?sku={Prices.PassProductId}&package={AppInfo.Current.PackageName}");
}
