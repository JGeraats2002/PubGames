using PubGames.ViewModels;

namespace PubGames.Views;

public partial class ReviewRequestPage : ContentPage
{
	private readonly ReviewRequestViewModel _vm;

	public ReviewRequestPage(ReviewRequestViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_vm.Finished += async (_, message) =>
		{
			if (!string.IsNullOrEmpty(message))
				await DisplayAlertAsync("Done", message, "OK");
			await Shell.Current.GoToAsync("..");
		};
	}

	private async void OnApproveClicked(object? sender, EventArgs e)
	{
		var confirmed = await DisplayAlertAsync($"{_vm.ApproveButtonText}?",
			"This changes what every player sees. The moderator gets a message in their Inbox.", _vm.ApproveButtonText, "Cancel");
		if (confirmed)
			await _vm.ApproveAsync();
	}

	/// <summary>A reason is required: the moderator sees it and knows what to fix.</summary>
	private async void OnRejectClicked(object? sender, EventArgs e)
	{
		while (true)
		{
			var reason = await DisplayPromptAsync("Reject request",
				$"Tell {_vm.SubmitterLabel} why, so they know what to change. It's sent to their Inbox:",
				"Reject", "Cancel", maxLength: 300, keyboard: Keyboard.Text);
			if (reason is null) return;

			if (!string.IsNullOrWhiteSpace(reason))
			{
				await _vm.RejectAsync(reason.Trim());
				return;
			}
			await DisplayAlertAsync("Reason needed", "Please give a reason for rejecting.", "OK");
		}
	}
}
