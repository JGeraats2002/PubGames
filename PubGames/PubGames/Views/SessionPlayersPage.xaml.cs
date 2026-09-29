using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class SessionPlayersPage : ContentPage
{
	private const string NewPlayerOption = "Someone new...";

	private readonly SessionPlayersViewModel _vm;

	public SessionPlayersPage(SessionPlayersViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	private async void OnMoveUpClicked(object? sender, EventArgs e)
	{
		if (sender is Button { BindingContext: ParticipantRow row })
			await _vm.MoveAsync(row, -1);
	}

	private async void OnMoveDownClicked(object? sender, EventArgs e)
	{
		if (sender is Button { BindingContext: ParticipantRow row })
			await _vm.MoveAsync(row, +1);
	}

	private async void OnRemoveClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: ParticipantRow row }) return;

		var confirmed = await DisplayAlertAsync($"Remove {row.Player.Name}?",
			$"Their score ({row.Score}) is kept: if they join again later in this game, they continue from it.", "Remove", "Cancel");
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

		var takeOver = row.Score != 0 && await DisplayAlertAsync("Score",
			$"Does {replacement.Name} continue with {row.Player.Name}'s score ({row.Score})?",
			$"Yes, continue at {row.Score}", "No, start at 0");
		await _vm.ReplaceAsync(row, replacement, takeOver);
	}

	private async void OnSavedPlayerTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is Player player)
			await _vm.AddAsync(player);
	}

	private async void OnAddNewClicked(object? sender, EventArgs e) => await _vm.AddNewPlayerAsync();

	private async void OnDoneClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
