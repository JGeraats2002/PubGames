using PubGames.Models;
using PubGames.Services;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>Settings > Blocked players.</summary>
public partial class BlockedPlayersPage : ContentPage
{
	private readonly BlockedPlayersViewModel _vm;

	public BlockedPlayersPage(BlockedPlayersViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.BlockedPlayers);
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	private async void OnUnblockClicked(object? sender, EventArgs e)
	{
		if (sender is not BindableObject { BindingContext: BlockedPlayer player }) return;
		if (await DisplayAlertAsync($"Unblock {player.Name}?", "They can add you by your code again.", "Unblock", "Cancel"))
			await _vm.UnblockAsync(player);
	}
}
