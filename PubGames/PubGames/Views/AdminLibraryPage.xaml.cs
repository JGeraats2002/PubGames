using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// Admin tab &gt; Library: every game, to add, change or delete (moderators: to propose), with requests and
/// messages waiting at the top.
/// </summary>
public partial class AdminLibraryPage : ContentPage
{
	private readonly AdminLibraryViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public AdminLibraryPage(AdminLibraryViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.AdminLibrary(_vm.IsModerator));
	}

	/// <summary>Also runs when coming back from the edit page, so saved changes and deletions show immediately.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	/// <summary>Opens the filters (number of players, categories).</summary>
	private async void OnFilterClicked(object? sender, EventArgs e) => await FilterPage.ShowAsync(Navigation, _vm.Filters);

	/// <summary>Opens an empty game form for a new game.</summary>
	private async void OnNewGameClicked(object? sender, EventArgs e) =>
		await Shell.Current.GoToAsync(nameof(AdminCreateGamePage));

	/// <summary>
	/// Opens a game in the form to change it; a moderator's own waiting request for it opens instead, so they
	/// change that.
	/// </summary>
	private async void OnGameTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not PubGame game) return;

		// Another moderator's changes are waiting: it can't be changed now, but what they submitted can be looked at.
		if (game.TakenBy is { } request)
		{
			await Shell.Current.GoToAsync(nameof(ReviewRequestPage), new Dictionary<string, object> { ["requestId"] = request.Id });
			return;
		}
		await Shell.Current.GoToAsync(nameof(AdminCreateGamePage), new Dictionary<string, object> { ["gameId"] = game.Id });
	}

	/// <summary>Opens the requests waiting for review.</summary>
	private async void OnRequestsToReviewTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(AppShell.RequestsRoute);

	/// <summary>Opens the Inbox.</summary>
	private async void OnUnreadMessagesTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(AppShell.InboxRoute);

	/// <summary>
	/// A waiting new game or change opens in the editor, to change and submit
	/// again (or withdraw); deletions and new categories have nothing to edit,
	/// so they can only be withdrawn.
	/// </summary>
	private async void OnMyRequestTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not ReviewRequest request) return;

		if (request.HasGameProposal)
		{
			await Shell.Current.GoToAsync(nameof(AdminCreateGamePage), new Dictionary<string, object> { ["requestId"] = request.Id });
			return;
		}

		var withdraw = await DisplayAlertAsync("Withdraw request?",
			$"Your request \"{request.Summary}\" will be taken back before an admin reviews it.", "Withdraw", "Keep");
		if (withdraw)
			await _vm.WithdrawAsync(request);
	}
}
