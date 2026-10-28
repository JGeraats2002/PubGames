using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>Admin tab &gt; Inbox: messages from the team and review decisions.</summary>
public partial class InboxPage : ContentPage
{
	private readonly InboxViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public InboxPage(InboxViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Inbox);
	}

	/// <summary>Every time the page shows: the messages are loaded fresh.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	/// <summary>Chooses who on the team gets it (one person, several or whole groups) and writes them a message.</summary>
	private async void OnNewMessageClicked(object? sender, EventArgs e)
	{
		var recipients = await _vm.GetRecipientsAsync();
		if (await ChooseRecipientPage.ChooseAsync(this, recipients) is { Count: > 0 } chosen)
			await WriteAndSendAsync(chosen, chosen.Count == 1 ? chosen[0].Label : L.F("{0} people", chosen.Count));
	}

	/// <summary>Writes a message back to the sender.</summary>
	private async void OnReplyClicked(object? sender, EventArgs e)
	{
		if (sender is Button { BindingContext: InboxMessage message })
			await WriteAndSendAsync([new MessageRecipient(message.FromEmail, message.FromLabel)], message.FromLabel);
	}

	/// <summary>Asks for the message's text and sends it to each of them.</summary>
	private async Task WriteAndSendAsync(List<MessageRecipient> recipients, string toLabel)
	{
		var text = await DisplayPromptAsync(L.F("Message to {0}", toLabel),
			L.F("Keep it short ({0} characters at most):", InboxMessage.MaxLength),
			L.T("Send"), L.T("Cancel"), maxLength: InboxMessage.MaxLength, keyboard: Keyboard.Chat);
		if (string.IsNullOrWhiteSpace(text)) return;
		await _vm.SendAsync(recipients, text);
	}

	/// <summary>The recipient chooses: delete it, or just leave it in the Inbox.</summary>
	private async void OnDeleteClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: InboxMessage message }) return;

		var confirmed = await DisplayAlertAsync(L.T("Delete message?"),
			L.T("It will be removed from your Inbox. This can't be undone."), L.T("Delete"), L.T("Keep"));
		if (confirmed)
			await _vm.DeleteAsync(message);
	}

	/// <summary>A translation to check: opens the game on the Languages page, in that language.</summary>
	private async void OnCheckTranslationClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: InboxMessage { GameId: { } gameId } message }) return;
		await Shell.Current.GoToAsync(nameof(AdminCreateGamePage), new Dictionary<string, object>
		{
			["gameId"] = gameId,
			["language"] = message.Language ?? string.Empty
		});
	}

	/// <summary>Opens the rejected request in the editor, with the admin's reason shown at the top.</summary>
	private async void OnResubmitClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: InboxMessage message }) return;

		if (await _vm.FindRejectedRequestAsync(message) is { } request)
			await Shell.Current.GoToAsync(nameof(AdminCreateGamePage), new Dictionary<string, object> { ["requestId"] = request.Id });
	}
}
