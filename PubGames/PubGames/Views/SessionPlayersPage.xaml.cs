using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>Change players, during a game: players join, leave, swap seats or are replaced.</summary>
public partial class SessionPlayersPage : ContentPage
{
	private const string NewPlayerOption = "Someone new...";

	private readonly SessionPlayersViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public SessionPlayersPage(SessionPlayersViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.SessionPlayers);
	}

	/// <summary>Moves a player one seat up.</summary>
	private async void OnMoveUpClicked(object? sender, EventArgs e)
	{
		if (sender is Button { BindingContext: ParticipantRow row })
			await _vm.MoveAsync(row, -1);
	}

	/// <summary>Moves a player one seat down.</summary>
	private async void OnMoveDownClicked(object? sender, EventArgs e)
	{
		if (sender is Button { BindingContext: ParticipantRow row })
			await _vm.MoveAsync(row, +1);
	}

	/// <summary>A player leaves the game, after asking; their scores so far stay.</summary>
	private async void OnRemoveClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: ParticipantRow row }) return;

		var confirmed = await DisplayAlertAsync($"Remove {row.Player.Name}?",
			$"Their score ({row.Score}) will be kept: if they join again later in this game, they'll continue from it.", "Remove", "Cancel");
		if (confirmed)
			await _vm.RemoveAsync(row);
	}

	/// <summary>Pick who takes the seat (a saved player or someone new), then whether they take over the score.</summary>
	private async void OnReplaceClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: ParticipantRow row }) return;

		var options = _vm.AvailablePlayers.Select(p => p.Name).Append(NewPlayerOption).ToArray();
		var choice = await DisplayActionSheetAsync($"Who takes {row.Player.Name}'s seat?", "Cancel", null, options);

		Player? replacement;
		if (choice == NewPlayerOption)
		{
			var name = await DisplayPromptAsync("New player", "Name:", "Next", "Cancel");
			if (string.IsNullOrWhiteSpace(name)) return;
			replacement = await _vm.CreatePlayerAsync(name);
		}
		else
			replacement = _vm.AvailablePlayers.FirstOrDefault(p => p.Name == choice);
		if (replacement is null) return;

		await _vm.ReplaceAsync(row, replacement);
	}

	/// <summary>A saved player joins the game.</summary>
	private async void OnSavedPlayerTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is Player player)
			await _vm.AddAsync(player);
	}

	/// <summary>A new player joins the game by name.</summary>
	private async void OnAddNewClicked(object? sender, EventArgs e) => await _vm.AddNewPlayerAsync();

	/// <summary>Back to the scoreboard with the new line-up.</summary>
	private async void OnDoneClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
