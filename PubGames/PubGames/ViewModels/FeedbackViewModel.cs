using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>A choice in "Which game?": none, or one of the games in the library.</summary>
public sealed record FeedbackGameOption(PubGame? Game)
{
	public string Label => Game?.Name ?? "Not about a game";

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

	public FeedbackViewModel(IFeedbackService feedback, ILocalDatabaseService local)
	{
		_feedback = feedback;
		_local = local;
	}

	public List<FeedbackCategoryOption> Categories { get; } = FeedbackCategoryOption.All;

	[ObservableProperty]
	private FeedbackCategoryOption? selectedCategory;

	public ObservableCollection<FeedbackGameOption> Games { get; } = new();

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowsGamePicker))]
	private bool isAboutGame;

	public bool ShowsGamePicker => IsAboutGame;

	[ObservableProperty]
	private FeedbackGameOption? selectedGame;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CharactersText))]
	private string text = string.Empty;

	public string CharactersText => $"{Text.Trim().Length} / {Feedback.MaxLength}";

	public int MaxLength => Feedback.MaxLength;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasStatusMessage))]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	public bool HasStatusMessage => StatusMessage.Length > 0;

	[ObservableProperty]
	private bool isBusy;

	/// <summary>Feedback sent earlier, newest first.</summary>
	public ObservableCollection<Feedback> Sent { get; } = new();

	[ObservableProperty]
	private bool hasSent;

	public async Task LoadAsync()
	{
		if (Games.Count == 0)
			foreach (var game in (await _local.GetGamesAsync()).OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase))
				Games.Add(new FeedbackGameOption(game));
		await LoadSentAsync();
	}

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
			StatusMessage = ex is HttpRequestException ? "No connection - your earlier feedback can't be shown now." : ex.Message;
		}
	}

	/// <summary>Checks the form and sends it; true when sent.</summary>
	public async Task<bool> SendAsync()
	{
		if (SelectedCategory is not { } category)
		{
			StatusMessage = "Choose what your feedback is about.";
			return false;
		}
		PubGame? game = null;
		if (IsAboutGame)
		{
			if (SelectedGame?.Game is not { } chosen)
			{
				StatusMessage = "Choose which game it's about.";
				return false;
			}
			game = chosen;
		}
		if (Text.Trim().Length < Feedback.MinLength)
		{
			StatusMessage = $"Explain your feedback - at least {Feedback.MinLength} characters, so the admins know what to look at.";
			return false;
		}

		IsBusy = true;
		StatusMessage = "Sending...";
		try
		{
			await _feedback.SendAsync(category.Category, game, Text);
			SelectedCategory = null;
			IsAboutGame = false;
			SelectedGame = null;
			Text = string.Empty;
			StatusMessage = "Thanks! Your feedback is sent to the admins.";
			await LoadSentAsync();
			return true;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message;
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

	public AdminFeedbackViewModel(IFeedbackService feedback) => _feedback = feedback;

	public List<string> Filters { get; } = ["Open", "New", "Being worked on", "Resolved", "Won't be changed", "All"];

	[ObservableProperty]
	private string selectedFilter = "Open";

	partial void OnSelectedFilterChanged(string value) => Show();

	public ObservableCollection<Feedback> Items { get; } = new();

	[ObservableProperty]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	[ObservableProperty]
	private bool isRefreshing;

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
			StatusMessage = ex is HttpRequestException ? "No connection - feedback can't be loaded now." : ex.Message;
		}
		finally
		{
			IsRefreshing = false;
		}
		Show();
	}

	private void Show()
	{
		var shown = _all.Where(f => SelectedFilter switch
		{
			"Open" => f.IsOpen,
			"New" => f.Status == FeedbackStatus.New,
			"Being worked on" => f.Status == FeedbackStatus.InProgress,
			"Resolved" => f.Status == FeedbackStatus.Resolved,
			"Won't be changed" => f.Status == FeedbackStatus.Declined,
			_ => true
		}).ToList();
		Items.Clear();
		foreach (var item in shown)
			Items.Add(item);
	}

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
			StatusMessage = ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message;
		}
	}

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
			StatusMessage = ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message;
		}
	}
}

/// <summary>Settings > Blocked players: who the user blocked, to unblock.</summary>
public partial class BlockedPlayersViewModel : ObservableObject
{
	private readonly IPlayerLinkService _links;

	public BlockedPlayersViewModel(IPlayerLinkService links) => _links = links;

	public ObservableCollection<BlockedPlayer> Blocked { get; } = new();

	[ObservableProperty]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	public async Task LoadAsync()
	{
		try
		{
			var blocked = await _links.GetBlockedAsync();
			Blocked.Clear();
			foreach (var b in blocked)
				Blocked.Add(b);
			StatusMessage = string.Empty;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? "No connection - blocked players can't be shown now." : ex.Message;
		}
	}

	public async Task UnblockAsync(BlockedPlayer player)
	{
		try
		{
			await _links.UnblockAsync(player.Uid);
			Blocked.Remove(player);
			StatusMessage = $"{player.Name} is unblocked. They can add you by your code again; add them again by their code if you want.";
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message;
		}
	}
}
