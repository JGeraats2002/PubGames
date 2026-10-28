using PubGames.Models;
using PubGames.Services;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>Settings > Blocked players.</summary>
public partial class BlockedPlayersPage : ContentPage
{
	private readonly BlockedPlayersViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public BlockedPlayersPage(BlockedPlayersViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.BlockedPlayers);
	}

	/// <summary>Every time the page shows: the blocked players are loaded fresh.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	/// <summary>Unblocks a player, after asking.</summary>
	private async void OnUnblockClicked(object? sender, EventArgs e)
	{
		if (sender is not BindableObject { BindingContext: BlockedPlayer player }) return;
		if (await DisplayAlertAsync(L.F("Unblock {0}?", player.Name), L.T("They can add you by your code again."), L.T("Unblock"), L.T("Cancel")))
			await _vm.UnblockAsync(player);
	}
}
