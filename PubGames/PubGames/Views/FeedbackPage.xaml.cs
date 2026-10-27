using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>Settings > Feedback: send feedback to the admins and see what happened to earlier feedback.</summary>
public partial class FeedbackPage : ContentPage
{
	private readonly FeedbackViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public FeedbackPage(FeedbackViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Feedback);
	}

	/// <summary>Every time the page shows: the games and the user's earlier feedback are loaded.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	/// <summary>Sends the feedback, once it's complete.</summary>
	private async void OnSendClicked(object? sender, EventArgs e) => await _vm.SendAsync();
}
