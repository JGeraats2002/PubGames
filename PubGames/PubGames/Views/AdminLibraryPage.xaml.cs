using PubGames.Models;
using PubGames.Services;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// Admin tab &gt; Library: every game, to add, change or delete (moderators: to propose), with requests and
/// messages waiting at the top.
/// </summary>
public partial class AdminLibraryPage : ContentPage
{
	private readonly AdminLibraryViewModel _vm;
	private readonly TestRun _testRun;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public AdminLibraryPage(AdminLibraryViewModel vm, TestRun testRun)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_testRun = testRun;
		InfoButton.Attach(this, () => Help.AdminLibrary(_vm.IsModerator));
		ReportButton.Attach(this, () => new ReportContext(L.T("Library (games and drafts)")));
	}

	/// <summary>Also runs when coming back from the edit page, so saved changes and deletions show immediately.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		// Back from testing someone's draft: nothing of the test is kept.
		if (_testRun.IsActive && !_testRun.IsRestarting)
			await _testRun.StopAsync();
		await _vm.LoadAsync();
	}

	/// <summary>
	/// A draft: your own opens to change and finish. Someone else's can be
	/// tested or given a tip; after 3 days without changes it can be taken over.
	/// </summary>
	private async void OnDraftTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not GameDraft draft) return;
		if (_vm.IsMine(draft))
		{
			await Shell.Current.GoToAsync(nameof(AdminCreateGamePage), new Dictionary<string, object> { ["draftId"] = draft.Id });
			return;
		}

		var owner = string.IsNullOrEmpty(draft.OwnerName) ? draft.OwnerEmail : draft.OwnerName;
		var test = L.T("▶ Test as player");
		var tip = L.F("Send {0} a tip", owner);
		var takeOver = L.T("Take over");
		var choice = draft.IsStale
			? await ChoicePopup.AskAsync(this, draft.Name, L.F("{0} is making this game; only they can change and finish it. Nobody has worked on it for 3 days, so you can take it over.", owner), L.T("Cancel"), test, tip, takeOver)
			: await ChoicePopup.AskAsync(this, draft.Name, L.F("{0} is making this game; only they can change and finish it. You can test it and send them a tip.", owner), L.T("Cancel"), test, tip);

		if (choice == test)
			await TestDraftAsync(draft);
		else if (choice == tip)
		{
			var text = await DisplayPromptAsync(L.F("Tip for {0}", owner), L.F("About \"{0}\" ({1} characters at most):", draft.Name, InboxMessage.MaxLength),
				L.T("Send"), L.T("Cancel"), maxLength: InboxMessage.MaxLength, keyboard: Keyboard.Chat);
			if (string.IsNullOrWhiteSpace(text)) return;
			var problem = await _vm.SendTipAsync(draft, text);
			await DisplayAlertAsync(problem is null ? L.T("Tip sent") : L.T("Not sent"), problem ?? L.F("{0} finds your tip in their Inbox.", owner), "OK");
		}
		else if (choice == takeOver)
		{
			if (await _vm.TakeOverAsync(draft) is { } problem)
			{
				await DisplayAlertAsync(L.T("Not taken over"), problem, "OK");
				await _vm.LoadAsync();
				return;
			}
			await Shell.Current.GoToAsync(nameof(AdminCreateGamePage), new Dictionary<string, object> { ["draftId"] = draft.Id });
		}
	}

	/// <summary>Tests someone's draft as a player, with made-up players; nothing is saved.</summary>
	private async Task TestDraftAsync(GameDraft draft)
	{
		var game = draft.Game;
		var min = Math.Clamp(game.MinPlayers ?? 2, 1, 12);
		var max = Math.Clamp(game.MaxPlayers ?? 8, min, 12);
		var counts = Enumerable.Range(min, max - min + 1).Select(n => L.F("{0} players", n)).ToArray();
		var count = min;
		if (counts.Length > 1)
		{
			var index = Array.IndexOf(counts, await DisplayActionSheetAsync(L.T("Test with how many players?"), L.T("Cancel"), null, counts));
			if (index < 0) return;
			count = min + index;
		}
		await _testRun.StartAsync(game, count);
		await Shell.Current.GoToAsync(nameof(GameRulesPage), new ShellNavigationQueryParameters
		{
			["game"] = game,
			["players"] = _testRun.Players
		});
	}

	/// <summary>Opens the filters (number of players, categories).</summary>
	private async void OnFilterClicked(object? sender, EventArgs e) => await FilterPage.ShowAsync(Navigation, _vm.Filters);

	/// <summary>Opens an empty game form for a new game.</summary>
	private async void OnNewGameClicked(object? sender, EventArgs e) =>
		await Shell.Current.GoToAsync(nameof(AdminCreateGamePage));

	/// <summary>
	/// Opens a game in the form to change it; a moderator's own waiting request for it opens instead, so they
	/// change that.
	/// </summary>
	private async void OnGameTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not PubGame game) return;

		// Another moderator's changes are waiting: it can't be changed now, but what they submitted can be looked at.
		if (game.TakenBy is { } request)
		{
			await Shell.Current.GoToAsync(nameof(ReviewRequestPage), new Dictionary<string, object> { ["requestId"] = request.Id });
			return;
		}
		await Shell.Current.GoToAsync(nameof(AdminCreateGamePage), new Dictionary<string, object> { ["gameId"] = game.Id });
	}

	/// <summary>Opens the requests waiting for review.</summary>
	private async void OnRequestsToReviewTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(AppShell.RequestsRoute);

	/// <summary>Opens the Inbox.</summary>
	private async void OnUnreadMessagesTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync(AppShell.InboxRoute);

	/// <summary>
	/// A waiting new game or change opens in the editor, to change and submit
	/// again (or withdraw); deletions and new categories have nothing to edit,
	/// so they can only be withdrawn.
	/// </summary>
	private async void OnMyRequestTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not ReviewRequest request) return;

		if (request.HasGameProposal)
		{
			await Shell.Current.GoToAsync(nameof(AdminCreateGamePage), new Dictionary<string, object> { ["requestId"] = request.Id });
			return;
		}

		var withdraw = await DisplayAlertAsync(L.T("Withdraw request?"),
			L.F("Your request \"{0}\" will be taken back before an admin reviews it.", request.Summary), L.T("Withdraw"), L.T("Keep"));
		if (withdraw)
			await _vm.WithdrawAsync(request);
	}
}
