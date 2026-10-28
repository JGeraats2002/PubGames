using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>Admin tab > Feedback (admins only): handle the players' feedback.</summary>
public partial class AdminFeedbackPage : ContentPage
{
	private readonly AdminFeedbackViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public AdminFeedbackPage(AdminFeedbackViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.AdminFeedback);
	}

	/// <summary>Every time the page shows: the feedback is loaded fresh.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	/// <summary>Pulling the list down loads the feedback again.</summary>
	private async void OnRefreshing(object? sender, EventArgs e) => await _vm.LoadAsync();

	/// <summary>A new status and/or a reply the sender sees under Settings > Feedback.</summary>
	private async void OnHandleClicked(object? sender, EventArgs e)
	{
		if (sender is not BindableObject { BindingContext: Feedback item }) return;

		var reply = L.T("Only reply");
		// By the choice as shown ("Mark as: Resolved", in the app's language).
		var statuses = new[] { FeedbackStatus.InProgress, FeedbackStatus.Resolved, FeedbackStatus.Declined, FeedbackStatus.New }
			.Where(s => s != item.Status)
			.ToDictionary(s => L.F("Mark as: {0}", Feedback.StatusLabel(s)));
		var choice = await DisplayActionSheetAsync(L.T("Handle feedback"), L.T("Cancel"), null,
			statuses.Keys.Append(reply).ToArray());
		if (choice is null || choice == L.T("Cancel")) return;

		var status = choice == reply ? item.Status : statuses[choice];
		var text = await DisplayPromptAsync(L.T("Reply to the sender"),
			L.T("Optional: the sender sees this with their feedback. Leave it empty for no reply."),
			L.T("Save"), L.T("Cancel"), maxLength: Feedback.MaxReplyLength, keyboard: Keyboard.Chat, initialValue: item.Reply ?? string.Empty);
		if (text is null) return; // cancelled
		await _vm.UpdateAsync(item, status, text);
	}

	/// <summary>Opens the game in the editor, to fix what the feedback is about.</summary>
	private async void OnOpenGameClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: Feedback { GameId: { } gameId } })
			await Shell.Current.GoToAsync(nameof(AdminCreateGamePage), new Dictionary<string, object> { ["gameId"] = gameId });
	}

	/// <summary>Deletes feedback for everyone, after asking.</summary>
	private async void OnDeleteClicked(object? sender, EventArgs e)
	{
		if (sender is not BindableObject { BindingContext: Feedback item }) return;
		if (await DisplayAlertAsync(L.T("Delete feedback?"), L.T("It's removed for everyone, also for the sender. This can't be undone."), L.T("Delete"), L.T("Keep")))
			await _vm.DeleteAsync(item);
	}
}
