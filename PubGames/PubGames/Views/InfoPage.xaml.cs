using PubGames.Models;

namespace PubGames.Views;

/// <summary>
/// What's behind a page's ⓘ button: how to use that page, or the rules of
/// the game being played. Opened with InfoButton.
/// </summary>
public partial class InfoPage : ContentPage
{
	private readonly bool _modal;

	public InfoPage(PageHelp help, bool modal = false)
	{
		InitializeComponent();
		BindingContext = help;
		_modal = modal;
	}

	private async void OnCloseClicked(object? sender, EventArgs e)
	{
		if (_modal)
			await Navigation.PopModalAsync();
		else
			await Navigation.PopAsync();
	}
}

/// <summary>Adds the ⓘ button to a page's title bar.</summary>
public static class InfoButton
{
	/// <summary>
	/// The ⓘ in the title bar opens how to use this page. The help is built
	/// when tapped, so it can depend on what the page shows at that moment.
	/// </summary>
	public static void Attach(ContentPage page, Func<PageHelp> help) =>
		page.ToolbarItems.Add(new ToolbarItem
		{
			Text = "ⓘ",
			Order = ToolbarItemOrder.Primary,
			Command = new Command(async () => await ShowAsync(page, help()))
		});

	/// <summary>
	/// While playing: the ⓘ offers how to use this page or the rules of the
	/// game, which are separate pages.
	/// </summary>
	public static void AttachWithRules(ContentPage page, Func<PageHelp> help, Func<PageHelp?> rules) =>
		page.ToolbarItems.Add(new ToolbarItem
		{
			Text = "ⓘ",
			Order = ToolbarItemOrder.Primary,
			Command = new Command(async () =>
			{
				const string howTo = "How to use this page";
				const string gameRules = "Rules of the game";
				var choice = await page.DisplayActionSheetAsync("Info", "Cancel", null, howTo, gameRules);
				if (choice == howTo)
					await ShowAsync(page, help());
				else if (choice == gameRules && rules() is { } r)
					await ShowAsync(page, r);
			})
		});

	/// <summary>Pushed on the page's own stack; pages outside the shell (sign-in) show it as a modal page.</summary>
	public static Task ShowAsync(Page page, PageHelp help) =>
		Shell.Current is null
			? page.Navigation.PushModalAsync(new InfoPage(help, modal: true))
			: page.Navigation.PushAsync(new InfoPage(help));
}
