using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class GameLibraryPage : ContentPage
{
	private readonly GameLibraryViewModel _vm;

	public GameLibraryPage(GameLibraryViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	private async void OnGameTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not PubGame game) return;

		if (!await _vm.IsOwnedAsync(game))
		{
			// TODO: show the unlock/paywall dialog and kick off Google Play
			// Billing here before allowing navigation into the scoreboard.
			await DisplayAlert(game.Name, $"Unlock for €{game.Price:0.00} to play.", "OK");
			return;
		}

		// TODO: create a GameSession + SessionParticipant rows for the chosen
		// players (from PlayerEntryPage) before navigating.
		await Shell.Current.GoToAsync(nameof(ScoreboardPage));
	}
}
