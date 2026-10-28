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
		var buy = L.F("Buy {0}", await _vm.PriceAsync());
		var pass = L.T("Pub Games Pass");
		var code = L.T("I have a code");
		var choice = await ChoicePopup.AskAsync(this, L.F("Unlock {0}", _vm.GameName), null, L.T("Cancel"), code, pass, buy);
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
			await DisplayAlertAsync(L.T("Payment pending"), L.F("{0} unlocks as soon as Google Play confirms the payment.", _vm.GameName), "OK");
		else if (outcome == PurchaseOutcome.Failed)
			await DisplayAlertAsync(L.T("Not bought"), problem ?? L.T("The purchase didn't go through."), "OK");
	}

	/// <summary>Asks for a friend code and unlocks the game with it, or says why it didn't work.</summary>
	private async Task RedeemAsync()
	{
		var typed = await DisplayPromptAsync(L.T("Code"), L.T("Type the code you were given."), L.T("Use code"), L.T("Cancel"),
			placeholder: "XXXX-XXXX", maxLength: 9, keyboard: Keyboard.Text);
		if (string.IsNullOrWhiteSpace(typed)) return;
		if (await _vm.RedeemAsync(typed) is { } problem)
			await DisplayAlertAsync(L.T("Code not used"), problem, "OK");
	}

	/// <summary>One subgame fewer.</summary>
	private void OnFewerSubgamesClicked(object? sender, EventArgs e) => _vm.ChangeSubgameCount(-1);

	/// <summary>One subgame more.</summary>
	private void OnMoreSubgamesClicked(object? sender, EventArgs e) => _vm.ChangeSubgameCount(+1);
}
