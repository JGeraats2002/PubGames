using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>One game with a tick box: is it in the category?</summary>
public partial class GameChoice : ObservableObject
{
	/// <summary>The game.</summary>
	public required PubGame Game { get; init; }

	/// <summary>Whether the game is in the category.</summary>
	[ObservableProperty]
	private bool isSelected;
}

/// <summary>
/// Admins: choose which games are in a category. Opened from an "Add games to
/// category" review request ("requestId"), which is completed on save, or
/// straight from the Categories page ("categoryId").
/// </summary>
public partial class CategoryGamesViewModel : ObservableObject, IQueryAttributable
{
	private readonly ILocalDatabaseService _local;
	private readonly ICloudSyncService _cloud;
	private readonly ICategoryService _categories;
	private readonly IAuthService _auth;

	private ReviewRequest? _request;
	private GameCategory? _category;

	/// <summary>Raised after saving so the page can navigate back.</summary>
	public event EventHandler<string>? Finished;

	/// <summary>Every published game, alphabetical; saving goes over all of them.</summary>
	public List<GameChoice> Games { get; } = new();

	/// <summary>The games matching the search.</summary>
	public ObservableCollection<GameChoice> VisibleGames { get; } = new();

	/// <summary>The search text: only games with it in their name show.</summary>
	[ObservableProperty]
	private string searchText = string.Empty;

	/// <summary>Category clouds to find games, the same as everywhere: several can be selected.</summary>
	public CategoryFilterSet Filters { get; } = new();

	partial void OnSearchTextChanged(string value) => ApplySearch();

	/// <summary>Shows the games whose name contains the search text.</summary>
	private void ApplySearch()
	{
		var text = SearchText.Trim();
		VisibleGames.Clear();
		foreach (var choice in Games.Where(c =>
			(text.Length == 0 || c.Game.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase))
			&& Filters.Matches(c.Game)))
			VisibleGames.Add(choice);
	}

	/// <summary>Ticks or unticks a game for this category.</summary>
	public void Toggle(GameChoice choice) => choice.IsSelected = !choice.IsSelected;

	/// <summary>The page's title: the category's name.</summary>
	[ObservableProperty]
	private string title = L.T("Games in category");


	/// <summary>Saving: the save button waits.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanSave))]
	private bool isBusy;

	/// <summary>Whether the games can be saved: not busy, and the category exists.</summary>
	public bool CanSave => !IsBusy && _category is not null;

	/// <summary>What happened, or why it couldn't.</summary>
	[ObservableProperty]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	/// <summary>
	/// Gets the services it works with - the phone's database, cloud sync, the categories and the sign-in -
	/// from the app (dependency injection).
	/// </summary>
	public CategoryGamesViewModel(ILocalDatabaseService local, ICloudSyncService cloud, ICategoryService categories, IAuthService auth)
	{
		_local = local;
		_cloud = cloud;
		_categories = categories;
		_auth = auth;
		Filters.Changed += (_, _) => ApplySearch();
	}

	/// <summary>Opened for a category: shows every game, with the ones in the category ticked.</summary>
	public async void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		string? categoryId = null;
		if (query.TryGetValue("requestId", out var rid) && rid is string requestId)
		{
			_request = await _local.GetReviewRequestAsync(requestId);
			categoryId = _request?.CategoryId;
		}
		else if (query.TryGetValue("categoryId", out var cid))
			categoryId = cid as string;

		_category = (await _categories.GetCachedAsync()).FirstOrDefault(c => c.Id == categoryId);
		OnPropertyChanged(nameof(CanSave));
		if (_category is null)
		{
			_statusMessageTimer.ShowLasting(L.T("This category no longer exists."), v => StatusMessage = v);
			return;
		}

		Title = L.F("Games in \"{0}\"", _category.Name);

		try
		{
			await _cloud.PullAllGamesAsync();
		}
		catch (Exception)
		{
			// Offline: this phone's copy of the games.
		}

		var categories = await _categories.GetCachedAsync();
		Games.Clear();
		foreach (var game in (await _local.GetManageableGamesAsync())
			.Where(g => g.Status == GameStatus.Published)
			.OrderBy(g => g.ShownName, StringComparer.CurrentCultureIgnoreCase))
			Games.Add(new GameChoice { Game = game, IsSelected = game.CategoryIds.Contains(_category.Id) });

		// The clouds filter by the games' saved categories; only categories that have games.
		var used = Games.SelectMany(g => g.Game.CategoryIds).ToHashSet();
		Filters.Show(await _categories.GetCachedGroupsAsync(), categories.Where(c => used.Contains(c.Id)));
		ApplySearch();
	}

	/// <summary>Saves every game whose tick changed; completes the review request, if opened from one.</summary>
	public async Task SaveAsync()
	{
		if (_category is null) return;

		// A game must keep at least one category.
		var wouldBeUncategorized = Games
			.Where(c => !c.IsSelected && c.Game.CategoryIds.Count == 1 && c.Game.CategoryIds[0] == _category.Id)
			.Select(c => c.Game.Name)
			.ToList();
		if (wouldBeUncategorized.Count > 0)
		{
			StatusMessage = L.F("Every game needs at least one category, so these must stay in \"{0}\" (or first get another category): {1}.", _category.Name, string.Join(", ", wouldBeUncategorized));
			return;
		}

		IsBusy = true;
		StatusMessage = L.T("Saving...");
		var changed = 0;
		try
		{
			foreach (var choice in Games)
			{
				var ids = choice.Game.CategoryIds;
				if (choice.IsSelected == ids.Contains(_category.Id)) continue;

				if (choice.IsSelected) ids.Add(_category.Id);
				else ids.Remove(_category.Id);
				choice.Game.CategoryIds = ids;

				await _cloud.SaveGameAsync(choice.Game);
				await _local.SaveGameAsync(choice.Game);
				changed++;
			}

			if (_request is { IsWaitingForReview: true })
			{
				_request.Status = ReviewStatus.Approved;
				_request.ReviewedByUserId = _auth.AccountId;
				_request.ReviewedAt = DateTime.UtcNow;
				await _cloud.SaveReviewRequestAsync(_request);
			}
		}
		catch (Exception ex)
		{
			StatusMessage = (ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message)
				+ (changed > 0 ? L.F(" ({0} game(s) were already saved.)", changed) : string.Empty);
			return;
		}
		finally
		{
			IsBusy = false;
		}

		var total = Games.Count(g => g.IsSelected);
		Finished?.Invoke(this, L.F("\"{0}\" now has {1}.", _category.Name, total == 1 ? L.T("1 game") : L.F("{0} games", total)));
	}
}
