using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>Admin tab &gt; Requests: everything moderators proposed that's waiting for a decision.</summary>
public partial class ReviewRequestsPage : ContentPage
{
	private readonly ReviewRequestsViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public ReviewRequestsPage(ReviewRequestsViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.ReviewRequests);
	}

	/// <summary>Also runs when coming back from a review, so decided requests drop off the list.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	/// <summary>Opens the tapped request.</summary>
	private async void OnRequestTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not ReviewRequest request) return;

		// Adding games to a category is choosing games, not approving a proposal.
		var page = request.Type == ReviewRequestType.AddGamesToCategory ? nameof(CategoryGamesPage) : nameof(ReviewRequestPage);
		await Shell.Current.GoToAsync(page, new Dictionary<string, object> { ["requestId"] = request.Id });
	}
}
