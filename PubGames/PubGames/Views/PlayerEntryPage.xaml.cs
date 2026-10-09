using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class PlayerEntryPage : ContentPage
{
	private readonly PlayerEntryViewModel _vm;

	public PlayerEntryPage(PlayerEntryViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Players);
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	/// <summary>Tap a saved player: add them to the game.</summary>
	private void OnSavedPlayerTapped(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: Models.Player player })
			_vm.AddSavedPlayerCommand.Execute(player);
	}

	/// <summary>Hold a saved player: delete them from the saved players, after asking.</summary>
	private async void OnSavedPlayerHeld(object? sender, EventArgs e)
	{
		if (sender is not BindableObject { BindingContext: Models.Player player }) return;

		var delete = await DisplayAlertAsync("Delete player?",
			$"{player.Name} will be removed from your saved players. Earlier games will keep their name.", "Yes", "No");
		if (delete)
			await _vm.DeleteSavedPlayerAsync(player);
	}

	/// <summary>Your own name for a player, on this phone only.</summary>
	private async void OnRenameClicked(object? sender, EventArgs e)
	{
		if (sender is not BindableObject { BindingContext: Models.Player player }) return;

		var name = await DisplayPromptAsync("Name", player.IsLinked
				? "What do you call this player? Only on your phone - it doesn't change their profile."
				: "What's this player's name?",
			"Save", "Cancel", initialValue: player.Name, maxLength: 30, keyboard: Keyboard.Text);
		if (name is not null)
			await _vm.RenameAsync(player, name);
	}

	private void OnMoveUpClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: Player player })
			_vm.Move(player, -1);
	}

	private void OnMoveDownClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: Player player })
			_vm.Move(player, +1);
	}

	private async void OnChooseGameClicked(object? sender, EventArgs e)
	{
		if (_vm.SelectedPlayers.Count == 0)
		{
			await DisplayAlertAsync("Who's playing?", "Add at least one player first.", "OK");
			return;
		}

		await Shell.Current.GoToAsync(nameof(GameLibraryPage), new ShellNavigationQueryParameters
		{
			["players"] = _vm.SelectedPlayers.ToList()
		});
	}
}
