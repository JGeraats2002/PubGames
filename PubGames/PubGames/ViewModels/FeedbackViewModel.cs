using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>A choice in "Which game?": none, or one of the games in the library.</summary>
public sealed record FeedbackGameOption(PubGame? Game)
{
	/// <summary>The game's name, or "Not about a game".</summary>
	public string Label => Game?.Name ?? L.T("Not about a game");

	/// <summary>The game's name (or "Not about a game"), as the picker shows it.</summary>
	public override string ToString() => Label;
}

/// <summary>
/// Settings > Feedback: send feedback to the admins - first what it's about
/// (bug, typo, rules mistake, ...), then whether it's about a game and which,
/// then the explanation, which is required. Below it the user's earlier
/// feedback, with how far the admins are and their reply.
/// </summary>
public partial class FeedbackViewModel : ObservableObject
{
	private readonly IFeedbackService _feedback;
	private readonly ILocalDatabaseService _local;

	/// <summary>
	/// Gets the services it works with - feedback and the phone's database - from the app (dependency
	/// injection).
	/// </summary>
	public FeedbackViewModel(IFeedbackService feedback, ILocalDatabaseService local)
	{
		_feedback = feedback;
		_local = local;
	}

	/// <summary>Every subject feedback can be about, for the picker.</summary>
	public List<FeedbackCategoryOption> Categories { get; } = FeedbackCategoryOption.All;

	/// <summary>The chosen subject, or null while none is chosen.</summary>
	[ObservableProperty]
	private FeedbackCategoryOption? selectedCategory;

	/// <summary>Every game, for the game picker.</summary>
	public ObservableCollection<FeedbackGameOption> Games { get; } = new();

	/// <summary>Whether the feedback is about a game.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowsGamePicker))]
	private bool isAboutGame;

	/// <summary>The game picker shows once the feedback is about a game.</summary>
	public bool ShowsGamePicker => IsAboutGame;

	/// <summary>The chosen game, or null.</summary>
	[ObservableProperty]
	private FeedbackGameOption? selectedGame;

	/// <summary>The explanation, as typed.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CharactersText))]
	private string text = string.Empty;

	/// <summary>"120 / 1000": how much of the space is used.</summary>
	public string CharactersText => $"{Text.Trim().Length} / {Feedback.MaxLength}";

	/// <summary>The most characters an explanation can have.</summary>
	public int MaxLength => Feedback.MaxLength;

	/// <summary>What's still missing, that it's sent, or what went wrong.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasStatusMessage))]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	/// <summary>Whether there's a message to show.</summary>
	public bool HasStatusMessage => StatusMessage.Length > 0;

	/// <summary>Sending: the send button waits.</summary>
	[ObservableProperty]
	private bool isBusy;

	/// <summary>Feedback sent earlier, newest first.</summary>
	public ObservableCollection<Feedback> Sent { get; } = new();

	/// <summary>Whether the user sent feedback before: the list of it shows.</summary>
	[ObservableProperty]
	private bool hasSent;

	/// <summary>Fills the game picker and shows the user's earlier feedback.</summary>
	public async Task LoadAsync()
	{
		if (Games.Count == 0)
			foreach (var game in (await _local.GetGamesAsync()).Select(g => g.InAppLanguage()).OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase))
				Games.Add(new FeedbackGameOption(game));
		await LoadSentAsync();
	}

	/// <summary>Shows the user's earlier feedback with its status and reply.</summary>
	private async Task LoadSentAsync()
	{
		try
		{
			var mine = await _feedback.GetMineAsync();
			Sent.Clear();
			foreach (var item in mine)
				Sent.Add(item);
			HasSent = Sent.Count > 0;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - your earlier feedback can't be shown now.") : ex.Message;
		}
	}

	/// <summary>Checks the form and sends it; true when sent.</summary>
	public async Task<bool> SendAsync()
	{
		if (SelectedCategory is not { } category)
		{
			StatusMessage = L.T("Choose what your feedback is about.");
			return false;
		}
		PubGame? game = null;
		if (IsAboutGame)
		{
			if (SelectedGame?.Game is not { } chosen)
			{
				StatusMessage = L.T("Choose which game it's about.");
				return false;
			}
			game = chosen;
		}
		if (Text.Trim().Length < Feedback.MinLength)
		{
			StatusMessage = L.F("Explain your feedback - at least {0} characters, so the admins know what to look at.", Feedback.MinLength);
			return false;
		}

		IsBusy = true;
		StatusMessage = L.T("Sending...");
		try
		{
			await _feedback.SendAsync(category.Category, game, Text);
			SelectedCategory = null;
			IsAboutGame = false;
			SelectedGame = null;
			Text = string.Empty;
			StatusMessage = L.T("Thanks! Your feedback is sent to the admins.");
			await LoadSentAsync();
			return true;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
			return false;
		}
		finally
		{
			IsBusy = false;
		}
	}
}

/// <summary>
/// Admin tab > Feedback: everyone's feedback, filtered on where it is (open,
/// resolved, ...). Admins mark it as being worked on, resolved or not to be
/// changed, with a reply the sender sees, open the game it's about, or delete it.
/// </summary>
public partial class AdminFeedbackViewModel : ObservableObject
{
	private readonly IFeedbackService _feedback;
	private List<Feedback> _all = new();

	/// <summary>Gets the feedback service from the app (dependency injection).</summary>
	public AdminFeedbackViewModel(IFeedbackService feedback) => _feedback = feedback;

	/// <summary>What the list can show: open feedback, a status, or all.</summary>
	public List<string> Filters { get; } = [L.T("Open"), L.T("New"), L.T("Being worked on"), L.T("Resolved"), L.T("Won't be changed"), L.T("All")];

	/// <summary>What the list shows now.</summary>
	[ObservableProperty]
	private string selectedFilter = L.T("Open");

	partial void OnSelectedFilterChanged(string value) => Show();

	/// <summary>The feedback that fits the filter, newest first.</summary>
	public ObservableCollection<Feedback> Items { get; } = new();

	/// <summary>What went wrong, if something did.</summary>
	[ObservableProperty]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	/// <summary>Loading: the pull-down spinner shows.</summary>
	[ObservableProperty]
	private bool isRefreshing;

	/// <summary>Loads all feedback from the cloud and shows what the filter asks for.</summary>
	public async Task LoadAsync()
	{
		IsRefreshing = true;
		try
		{
			_all = await _feedback.GetAllAsync();
			StatusMessage = string.Empty;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - feedback can't be loaded now.") : ex.Message;
		}
		finally
		{
			IsRefreshing = false;
		}
		Show();
	}

	/// <summary>Shows the feedback that fits the chosen filter.</summary>
	private void Show()
	{
		// By place in Filters: the names are in the app's language.
		var filter = Filters.IndexOf(SelectedFilter);
		var shown = _all.Where(f => filter switch
		{
			0 => f.IsOpen,
			1 => f.Status == FeedbackStatus.New,
			2 => f.Status == FeedbackStatus.InProgress,
			3 => f.Status == FeedbackStatus.Resolved,
			4 => f.Status == FeedbackStatus.Declined,
			_ => true
		}).ToList();
		Items.Clear();
		foreach (var item in shown)
			Items.Add(item);
	}

	/// <summary>Gives feedback a new status and a reply the sender sees.</summary>
	public async Task UpdateAsync(Feedback item, FeedbackStatus status, string? reply)
	{
		try
		{
			await _feedback.UpdateAsync(item, status, reply);
			StatusMessage = string.Empty;
			Show();
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
		}
	}

	/// <summary>Deletes feedback for everyone.</summary>
	public async Task DeleteAsync(Feedback item)
	{
		try
		{
			await _feedback.DeleteAsync(item);
			_all.Remove(item);
			StatusMessage = string.Empty;
			Show();
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
		}
	}
}

/// <summary>Settings > Blocked players: who the user blocked, to unblock.</summary>
public partial class BlockedPlayersViewModel : ObservableObject
{
	private readonly IPlayerLinkService _links;

	/// <summary>Gets the player-links service from the app (dependency injection).</summary>
	public BlockedPlayersViewModel(IPlayerLinkService links) => _links = links;

	/// <summary>The players the user blocked.</summary>
	public ObservableCollection<BlockedPlayer> Blocked { get; } = new();

	/// <summary>That a player is unblocked, or what went wrong.</summary>
	[ObservableProperty]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	/// <summary>Shows the players the user blocked.</summary>
	public async Task LoadAsync()
	{
		try
		{
			var blocked = await _links.GetBlockedAsync();
			Blocked.Clear();
			foreach (var blockedPlayer in blocked)
				Blocked.Add(blockedPlayer);
			StatusMessage = string.Empty;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - blocked players can't be shown now.") : ex.Message;
		}
	}

	/// <summary>Unblocks a player: they can add the user by code again.</summary>
	public async Task UnblockAsync(BlockedPlayer player)
	{
		try
		{
			await _links.UnblockAsync(player.Uid);
			Blocked.Remove(player);
			StatusMessage = L.F("{0} is unblocked. They can add you by your code again; add them again by their code if you want.", player.Name);
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
		}
	}
}
