using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class AdminLibraryPage : ContentPage
{
	private readonly AdminLibraryViewModel _vm;

	public AdminLibraryPage(AdminLibraryViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	/// <summary>Also runs when coming back from the edit page, so saved changes and deletions show immediately.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	private async void OnNewGameClicked(object? sender, EventArgs e) =>
		await Shell.Current.GoToAsync(nameof(AdminCreateGamePage));

	private async void OnGameTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is PubGame game)
			await Shell.Current.GoToAsync(nameof(AdminCreateGamePage), new Dictionary<string, object> { ["gameId"] = game.Id });
	}

	private async void OnRequestsToReviewTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(AppShell.RequestsRoute);

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
			$"Your request \"{request.Summary}\" is taken back before an admin reviews it.", "Withdraw", "Keep");
		if (withdraw)
			await _vm.WithdrawAsync(request);
	}
}
