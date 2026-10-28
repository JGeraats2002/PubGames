using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// Players tab &gt; Who's playing?: the start of a game night - choose the players in seat order, then a
/// game.
/// </summary>
public partial class PlayerEntryPage : ContentPage
{
	private readonly PlayerEntryViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public PlayerEntryPage(PlayerEntryViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Players);
	}

	/// <summary>Every time the page shows: the saved players are loaded fresh.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	/// <summary>Saved games: continue an unfinished game, or delete games.</summary>
	private async void OnSavedGamesClicked(object? sender, EventArgs e) =>
		await Shell.Current.GoToAsync(nameof(SavedGamesPage));

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

		if (!player.IsLinked)
		{
			var delete = await DisplayAlertAsync(L.T("Delete player?"),
				L.F("{0} will be removed from your saved players. Earlier games will keep their name.", player.Name), L.T("Yes"), L.T("No"));
			if (delete)
				await _vm.DeleteSavedPlayerAsync(player);
			return;
		}

		// Added by their code: the link can go both ways, and they can be blocked.
		var onlyHere = L.T("Only from my players");
		var bothWays = L.T("Remove the link both ways");
		var block = L.T("Remove and block");
		var choice = await DisplayActionSheetAsync(L.F("Delete {0}?", player.Name), L.T("Cancel"), null, onlyHere, bothWays, block);
		PlayerRemoval? removal = choice == onlyHere ? PlayerRemoval.OnlyHere
			: choice == bothWays ? PlayerRemoval.BothWays
			: choice == block ? PlayerRemoval.Block
			: null;
		if (removal is null) return;
		if (removal == PlayerRemoval.Block && !await DisplayAlertAsync(L.F("Block {0}?", player.Name),
				L.F("{0} is removed from your players and you from theirs, and they can't add you by your code any more. Earlier games keep their names. You can unblock them under Account > Blocked players.", player.Name),
				L.T("Block"), L.T("Cancel")))
			return;
		await _vm.DeleteSavedPlayerAsync(player, removal.Value);
	}

	/// <summary>Your own name for a player, on this phone only.</summary>
	private async void OnRenameClicked(object? sender, EventArgs e)
	{
		if (sender is not BindableObject { BindingContext: Models.Player player }) return;

		var name = await DisplayPromptAsync(L.T("Name"), player.IsLinked
				? L.T("What do you call this player? Only on your phone - it doesn't change their profile.")
				: L.T("What's this player's name?"),
			L.T("Save"), L.T("Cancel"), initialValue: player.Name, maxLength: 30, keyboard: Keyboard.Text);
		if (name is not null)
			await _vm.RenameAsync(player, name);
	}

	/// <summary>Moves a player one seat up.</summary>
	private void OnMoveUpClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: Player player })
			_vm.Move(player, -1);
	}

	/// <summary>Moves a player one seat down.</summary>
	private void OnMoveDownClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: Player player })
			_vm.Move(player, +1);
	}

	/// <summary>Goes on to choosing a game with these players (at least one is needed).</summary>
	private async void OnChooseGameClicked(object? sender, EventArgs e)
	{
		if (_vm.SelectedPlayers.Count == 0)
		{
			await DisplayAlertAsync(L.T("Who's playing?"), L.T("Add at least one player first."), "OK");
			return;
		}

		await Shell.Current.GoToAsync(nameof(GameLibraryPage), new ShellNavigationQueryParameters
		{
			["players"] = _vm.SelectedPlayers.ToList()
		});
	}
}
