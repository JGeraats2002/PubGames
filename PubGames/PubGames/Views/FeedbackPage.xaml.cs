using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>Settings > Feedback: send feedback to the admins and see what happened to earlier feedback.</summary>
public partial class FeedbackPage : ContentPage
{
	private readonly FeedbackViewModel _vm;

	public FeedbackPage(FeedbackViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Feedback);
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	private async void OnSendClicked(object? sender, EventArgs e) => await _vm.SendAsync();
}
