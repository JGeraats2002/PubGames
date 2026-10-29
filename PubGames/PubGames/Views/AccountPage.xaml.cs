using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>The Account tab's main page: Settings, with a row per heading.</summary>
public partial class AccountPage : ContentPage
{
	private readonly AccountViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public AccountPage(AccountViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Account(_vm.Me));
	}

	/// <summary>Every time the page shows: the profile and stats are loaded fresh.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
		await OfferGooglePhotoAsync();
	}

	/// <summary>Opens Profile: picture, name, nickname and player code.</summary>
	private async void OnProfileTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(nameof(ProfilePage));

	/// <summary>Opens the stats per game.</summary>
	private async void OnStatsTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(nameof(MyStatsPage));

	/// <summary>Opens the Pub Games Pass page.</summary>
	private async void OnPassTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(nameof(SubscriptionPage));

	/// <summary>Opens the list of blocked players.</summary>
	private async void OnBlockedTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(nameof(BlockedPlayersPage));

	/// <summary>Opens the language screen, to show the app in another language.</summary>
	private async void OnLanguageTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(nameof(LanguagePage));

	/// <summary>Opens Feedback, to send feedback to the admins.</summary>
	private async void OnFeedbackTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(nameof(FeedbackPage));

	/// <summary>
	/// Deleting the account can't be undone: the user first reads what goes,
	/// then types DELETE to confirm, so it never happens by a mistap.
	/// </summary>
	private async void OnDeleteAccountClicked(object? sender, EventArgs e)
	{
		var delete = L.T("Delete account");
		var choice = await ChoicePopup.AskAsync(this, L.T("Delete your account?"),
			L.T("Your profile, player code, stats, bought games, feedback and the players and games on this phone are deleted for good. ")
			+ L.T("A running Pub Games Pass isn't stopped by this: cancel it in Google Play first (Account > Pub Games Pass > Manage subscription)."),
			L.T("Cancel"), delete);
		if (choice != delete) return;

		var typed = await DisplayPromptAsync(L.T("Are you sure?"), L.T("Type DELETE to delete your account. This can't be undone."),
			L.T("Delete"), L.T("Cancel"), placeholder: "DELETE", maxLength: 6, keyboard: Keyboard.Text);
		if (typed?.Trim().ToUpperInvariant() != "DELETE") return;

		if (await _vm.DeleteAccountAsync() is { } problem)
			await DisplayAlertAsync(L.T("Account not deleted"), problem, "OK");
	}

	/// <summary>Signs out after asking, so it doesn't happen by a mistap.</summary>
	private async void OnSignOutClicked(object? sender, EventArgs e)
	{
		if (await DisplayAlertAsync(L.T("Sign out?"), L.T("You can sign in again with any Google account."), L.T("Sign out"), L.T("Cancel")))
			await _vm.SignOutCommand.ExecuteAsync(null);
	}

	/// <summary>
	/// Once, when the profile has no picture: asks whether to use the Google account's picture. It can be
	/// changed in the app at any time.
	/// </summary>
	private async Task OfferGooglePhotoAsync()
	{
		if (await _vm.GooglePhotoToOfferAsync() is not { } url) return;
		var use = await DisplayAlertAsync(L.T("Use your Google picture?"),
			L.T("Your Google account has a profile picture. Do you want to use it in Pub Games too? You can change it in the app at any time."),
			L.T("Use it"), L.T("Not now"));
		await _vm.AnswerGooglePhotoAsync(url, use);
	}
}
