using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>Admin tab > Feedback (admins only): handle the players' feedback.</summary>
public partial class AdminFeedbackPage : ContentPage
{
	private readonly AdminFeedbackViewModel _vm;

	public AdminFeedbackPage(AdminFeedbackViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.AdminFeedback);
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	private async void OnRefreshing(object? sender, EventArgs e) => await _vm.LoadAsync();

	/// <summary>A new status and/or a reply the sender sees under Settings > Feedback.</summary>
	private async void OnHandleClicked(object? sender, EventArgs e)
	{
		if (sender is not BindableObject { BindingContext: Feedback item }) return;

		const string reply = "Only reply";
		var statuses = new[] { FeedbackStatus.InProgress, FeedbackStatus.Resolved, FeedbackStatus.Declined, FeedbackStatus.New }
			.Where(s => s != item.Status)
			.ToDictionary(Feedback.StatusLabel);
		var choice = await DisplayActionSheetAsync("Handle feedback", "Cancel", null,
			statuses.Keys.Select(k => $"Mark as: {k}").Append(reply).ToArray());
		if (choice is null || choice == "Cancel") return;

		var status = choice == reply ? item.Status : statuses[choice["Mark as: ".Length..]];
		var text = await DisplayPromptAsync("Reply to the sender",
			"Optional: the sender sees this with their feedback. Leave it empty for no reply.",
			"Save", "Cancel", maxLength: Feedback.MaxReplyLength, keyboard: Keyboard.Chat, initialValue: item.Reply ?? string.Empty);
		if (text is null) return; // cancelled
		await _vm.UpdateAsync(item, status, text);
	}

	/// <summary>Opens the game in the editor, to fix what the feedback is about.</summary>
	private async void OnOpenGameClicked(object? sender, EventArgs e)
	{
		if (sender is BindableObject { BindingContext: Feedback { GameId: { } gameId } })
			await Shell.Current.GoToAsync(nameof(AdminCreateGamePage), new Dictionary<string, object> { ["gameId"] = gameId });
	}

	private async void OnDeleteClicked(object? sender, EventArgs e)
	{
		if (sender is not BindableObject { BindingContext: Feedback item }) return;
		if (await DisplayAlertAsync("Delete feedback?", "It's removed for everyone, also for the sender. This can't be undone.", "Delete", "Keep"))
			await _vm.DeleteAsync(item);
	}
}
