using PubGames.Models;
using PubGames.Services;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// A game's rules, read before playing; then Start game (or Skip rules) starts the scoreboard, or for a paid
/// game that isn't unlocked, Unlock to play.
/// </summary>
public partial class GameRulesPage : ContentPage
{
	private readonly GameRulesViewModel _vm;

	/// <summary>
	/// Sets up the rules page with its view model and the ⓘ help, and opens the scoreboard once the game has
	/// started.
	/// </summary>
	public GameRulesPage(GameRulesViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.GameStart);

		// "../" replaces the rules page with the scoreboard, so Back from the
		// scoreboard returns to the game list to pick the next game.
		vm.SessionStarted += async (_, sessionId) =>
			await Shell.Current.GoToAsync($"../{nameof(ScoreboardPage)}", new ShellNavigationQueryParameters
			{
				["sessionId"] = sessionId
			});
	}

	/// <summary>Back from the pass page: perhaps the game is unlocked now.</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_vm.CheckUnlocked();
	}

	/// <summary>Buy the game once, a friend code, or the Pub Games Pass. Nothing is charged unless a purchase is completed.</summary>
	private async void OnUnlockClicked(object? sender, EventArgs e)
	{
		var buy = $"Buy {await _vm.PriceAsync()}";
		const string pass = "Pub Games Pass";
		const string code = "I have a code";
		var choice = await ChoicePopup.AskAsync(this, $"Unlock {_vm.GameName}", null, "Cancel", code, pass, buy);
		if (choice == pass)
		{
			await Shell.Current.GoToAsync(nameof(SubscriptionPage));
			return;
		}
		if (choice == code)
		{
			await RedeemAsync();
			return;
		}
		if (choice != buy) return;

		var (outcome, problem) = await _vm.BuyAsync();
		if (outcome == PurchaseOutcome.Pending)
			await DisplayAlertAsync("Payment pending", $"{_vm.GameName} unlocks as soon as Google Play confirms the payment.", "OK");
		else if (outcome == PurchaseOutcome.Failed)
			await DisplayAlertAsync("Not bought", problem ?? "The purchase didn't go through.", "OK");
	}

	/// <summary>Asks for a friend code and unlocks the game with it, or says why it didn't work.</summary>
	private async Task RedeemAsync()
	{
		var typed = await DisplayPromptAsync("Code", "Type the code you were given.", "Use code", "Cancel",
			placeholder: "XXXX-XXXX", maxLength: 9, keyboard: Keyboard.Text);
		if (string.IsNullOrWhiteSpace(typed)) return;
		if (await _vm.RedeemAsync(typed) is { } problem)
			await DisplayAlertAsync("Code not used", problem, "OK");
	}

	/// <summary>One subgame fewer.</summary>
	private void OnFewerSubgamesClicked(object? sender, EventArgs e) => _vm.ChangeSubgameCount(-1);

	/// <summary>One subgame more.</summary>
	private void OnMoreSubgamesClicked(object? sender, EventArgs e) => _vm.ChangeSubgameCount(+1);
}
