using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// Pick who a message goes to, searching by name or email. Await
/// ChooseAsync: it returns the chosen recipient, or null after going back.
/// </summary>
public partial class ChooseRecipientPage : ContentPage
{
	private readonly List<MessageRecipient> _all;
	private readonly TaskCompletionSource<MessageRecipient?> _result = new();

	/// <summary>Shows every possible recipient, to search and tap.</summary>
	private ChooseRecipientPage(List<MessageRecipient> recipients)
	{
		InitializeComponent();
		_all = recipients;
		List.ItemsSource = _all;
		InfoButton.Attach(this, () => Help.ChooseRecipient);
	}

	/// <summary>
	/// Shows the list of people to message over the page; returns the one tapped, or null when cancelled.
	/// </summary>
	public static async Task<MessageRecipient?> ChooseAsync(Page from, List<MessageRecipient> recipients)
	{
		var page = new ChooseRecipientPage(recipients);
		await from.Navigation.PushAsync(page);
		return await page._result.Task;
	}

	/// <summary>Shows the people whose name or email contains the search text.</summary>
	private void OnSearchChanged(object? sender, TextChangedEventArgs e)
	{
		var text = e.NewTextValue?.Trim() ?? string.Empty;
		List.ItemsSource = text.Length == 0
			? _all
			: _all.Where(r => r.Label.Contains(text, StringComparison.CurrentCultureIgnoreCase)
				|| r.Detail.Contains(text, StringComparison.CurrentCultureIgnoreCase)).ToList();
	}

	/// <summary>The tapped person is chosen; the list closes.</summary>
	private async void OnRecipientTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not MessageRecipient recipient) return;
		_result.TrySetResult(recipient);
		await Navigation.PopAsync();
	}

	/// <summary>Back without choosing: no recipient. (Opening the ⓘ page on top doesn't count.)</summary>
	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		if (args.NavigationType == NavigationType.Pop)
			_result.TrySetResult(null);
	}
}
