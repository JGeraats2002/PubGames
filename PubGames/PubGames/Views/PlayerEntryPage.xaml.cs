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
			var delete = await DisplayAlertAsync("Delete player?",
				$"{player.Name} will be removed from your saved players. Earlier games will keep their name.", "Yes", "No");
			if (delete)
				await _vm.DeleteSavedPlayerAsync(player);
			return;
		}

		// Added by their code: the link can go both ways, and they can be blocked.
		const string onlyHere = "Only from my players";
		const string bothWays = "Remove the link both ways";
		const string block = "Remove and block";
		var choice = await DisplayActionSheetAsync($"Delete {player.Name}?", "Cancel", null, onlyHere, bothWays, block);
		var removal = choice switch
		{
			onlyHere => PlayerRemoval.OnlyHere,
			bothWays => PlayerRemoval.BothWays,
			block => PlayerRemoval.Block,
			_ => (PlayerRemoval?)null
		};
		if (removal is null) return;
		if (removal == PlayerRemoval.Block && !await DisplayAlertAsync($"Block {player.Name}?",
				$"{player.Name} is removed from your players and you from theirs, and they can't add you by your code any more. Earlier games keep their names. You can unblock them under Account > Blocked players.",
				"Block", "Cancel"))
			return;
		await _vm.DeleteSavedPlayerAsync(player, removal.Value);
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
