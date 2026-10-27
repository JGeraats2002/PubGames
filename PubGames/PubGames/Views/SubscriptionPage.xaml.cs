using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>Account > Pub Games Pass.</summary>
public partial class SubscriptionPage : ContentPage
{
	private readonly SubscriptionViewModel _vm;

	public SubscriptionPage(SubscriptionViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Subscription);
	}

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

	private async void OnRestoreClicked(object? sender, EventArgs e) => await _vm.RestoreAsync();

	/// <summary>Cancelling or changing the pass happens in Google Play's own subscription page.</summary>
	private async void OnManageClicked(object? sender, EventArgs e) =>
		await Launcher.Default.OpenAsync($"https://play.google.com/store/account/subscriptions?sku={Prices.PassProductId}&package={AppInfo.Current.PackageName}");
}
