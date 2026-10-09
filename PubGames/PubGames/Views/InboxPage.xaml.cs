using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class InboxPage : ContentPage
{
	private readonly InboxViewModel _vm;

	public InboxPage(InboxViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Inbox);
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	private async void OnNewMessageClicked(object? sender, EventArgs e)
	{
		var recipients = await _vm.GetRecipientsAsync();
		if (await ChooseRecipientPage.ChooseAsync(this, recipients) is { } to)
			await WriteAndSendAsync(to.Email, to.Label);
	}

	private async void OnReplyClicked(object? sender, EventArgs e)
	{
		if (sender is Button { BindingContext: InboxMessage message })
			await WriteAndSendAsync(message.FromEmail, message.FromLabel);
	}

	private async Task WriteAndSendAsync(string toEmail, string toLabel)
	{
		var text = await DisplayPromptAsync($"Message to {toLabel}",
			$"Keep it short ({InboxMessage.MaxLength} characters at most):",
			"Send", "Cancel", maxLength: InboxMessage.MaxLength, keyboard: Keyboard.Chat);
		if (string.IsNullOrWhiteSpace(text)) return;
		await _vm.SendAsync(toEmail, text);
	}

	/// <summary>The recipient chooses: delete it, or just leave it in the Inbox.</summary>
	private async void OnDeleteClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: InboxMessage message }) return;

		var confirmed = await DisplayAlertAsync("Delete message?",
			"It will be removed from your Inbox. This can't be undone.", "Delete", "Keep");
		if (confirmed)
			await _vm.DeleteAsync(message);
	}

	/// <summary>Opens the rejected request in the editor, with the admin's reason shown at the top.</summary>
	private async void OnResubmitClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: InboxMessage message }) return;

		if (await _vm.FindRejectedRequestAsync(message) is { } request)
			await Shell.Current.GoToAsync(nameof(AdminCreateGamePage), new Dictionary<string, object> { ["requestId"] = request.Id });
	}
}
