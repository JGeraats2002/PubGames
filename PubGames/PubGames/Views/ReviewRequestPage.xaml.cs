using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// One review request: what a moderator proposes, compared with the current game, to approve or reject.
/// </summary>
public partial class ReviewRequestPage : ContentPage
{
	private readonly ReviewRequestViewModel _vm;

	/// <summary>
	/// Sets up the page with its view model and the ⓘ help, and goes back once a decision is made.
	/// </summary>
	public ReviewRequestPage(ReviewRequestViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.ReviewRequest);
		_vm.Finished += async (_, message) =>
		{
			if (!string.IsNullOrEmpty(message))
				await DisplayAlertAsync(L.T("Done"), message, "OK");
			await Shell.Current.GoToAsync("..");
		};
	}

	/// <summary>Shows the game as it is now.</summary>
	private void OnShowOriginalClicked(object? sender, EventArgs e) => _vm.ShowVersion(original: true);

	/// <summary>Shows the game with the proposed changes.</summary>
	private void OnShowChangedClicked(object? sender, EventArgs e) => _vm.ShowVersion(original: false);

	/// <summary>A moderator who can't change this game for now gets in touch with the one who submitted the request.</summary>
	private async void OnMessageClicked(object? sender, EventArgs e)
	{
		var text = await DisplayPromptAsync(L.F("Message to {0}", _vm.SubmitterLabel),
			L.F("Keep it short ({0} characters at most):", InboxMessage.MaxLength),
			L.T("Send"), L.T("Cancel"), maxLength: InboxMessage.MaxLength, keyboard: Keyboard.Chat,
			initialValue: L.F("About \"{0}\": ", _vm.SubjectName));
		if (!string.IsNullOrWhiteSpace(text) && await _vm.SendMessageAsync(text))
			await DisplayAlertAsync(L.T("Sent"), L.F("Your message is in {0}'s Inbox.", _vm.SubmitterLabel), "OK");
	}

	/// <summary>
	/// Approves the request after asking; the change happens and the moderator hears about it.
	/// </summary>
	private async void OnApproveClicked(object? sender, EventArgs e)
	{
		var confirmed = await DisplayAlertAsync($"{_vm.ApproveButtonText}?",
			L.T("This will change what every player sees. The moderator will get a message in their Inbox."), _vm.ApproveButtonText, L.T("Cancel"));
		if (confirmed)
			await _vm.ApproveAsync();
	}

	/// <summary>A reason is required: the moderator sees it and knows what to fix.</summary>
	private async void OnRejectClicked(object? sender, EventArgs e)
	{
		while (true)
		{
			var reason = await DisplayPromptAsync(L.T("Reject request"),
				L.F("Tell {0} why, so they know what to change. It will be sent to their Inbox:", _vm.SubmitterLabel),
				L.T("Reject"), L.T("Cancel"), maxLength: 300, keyboard: Keyboard.Text);
			if (reason is null) return;

			if (!string.IsNullOrWhiteSpace(reason))
			{
				await _vm.RejectAsync(reason.Trim());
				return;
			}
			await DisplayAlertAsync(L.T("Reason needed"), L.T("Please give a reason for rejecting."), "OK");
		}
	}
}
