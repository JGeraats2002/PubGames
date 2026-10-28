using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// Pick who a message goes to: whole groups, or people found by name or email, as many as you like. Await
/// ChooseAsync: it returns the people ticked, or null after going back.
/// </summary>
public partial class ChooseRecipientPage : ContentPage
{
	private readonly ChooseRecipientsViewModel _vm;
	private readonly TaskCompletionSource<List<MessageRecipient>?> _result = new();

	/// <summary>Shows every possible recipient, to search and tick.</summary>
	private ChooseRecipientPage(List<MessageRecipient> recipients)
	{
		InitializeComponent();
		BindingContext = _vm = new ChooseRecipientsViewModel(recipients);
		InfoButton.Attach(this, () => Help.ChooseRecipient);
	}

	/// <summary>
	/// Shows the list of people to message over the page; returns the ones ticked, or null when cancelled.
	/// </summary>
	public static async Task<List<MessageRecipient>?> ChooseAsync(Page from, List<MessageRecipient> recipients)
	{
		var page = new ChooseRecipientPage(recipients);
		await from.Navigation.PushAsync(page);
		return await page._result.Task;
	}

	/// <summary>Write message: the ticked people are chosen; the list closes.</summary>
	private async void OnWriteClicked(object? sender, EventArgs e)
	{
		if (_vm.Selected is not { Count: > 0 } chosen) return;
		_result.TrySetResult(chosen);
		await Navigation.PopAsync();
	}

	/// <summary>Back without choosing: no recipients. (Opening the ⓘ page on top doesn't count.)</summary>
	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		if (args.NavigationType == NavigationType.Pop)
			_result.TrySetResult(null);
	}
}
