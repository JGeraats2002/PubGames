using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>One game with a tick box: is it in the category?</summary>
public partial class GameChoice : ObservableObject
{
	public required PubGame Game { get; init; }

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

	public ObservableCollection<GameChoice> Games { get; } = new();

	[ObservableProperty]
	private string title = "Games in category";

	[ObservableProperty]
	private string explanation = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanSave))]
	private bool isBusy;

	public bool CanSave => !IsBusy && _category is not null;

	[ObservableProperty]
	private string statusMessage = string.Empty;

	public CategoryGamesViewModel(ILocalDatabaseService local, ICloudSyncService cloud, ICategoryService categories, IAuthService auth)
	{
		_local = local;
		_cloud = cloud;
		_categories = categories;
		_auth = auth;
	}

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
			StatusMessage = "This category no longer exists.";
			return;
		}

		Title = $"Games in \"{_category.Name}\"";
		Explanation = $"Tick every game that belongs in \"{_category.Name}\". Players can then find these games by choosing this category.";

		try
		{
			await _cloud.PullAllGamesAsync();
		}
		catch (Exception)
		{
			// Offline: this phone's copy of the games.
		}

		Games.Clear();
		foreach (var game in (await _local.GetManageableGamesAsync()).Where(g => g.Status == GameStatus.Published))
			Games.Add(new GameChoice { Game = game, IsSelected = game.CategoryIds.Contains(_category.Id) });
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
			StatusMessage = $"Every game needs at least one category, so these must stay in \"{_category.Name}\" (or first get another category): {string.Join(", ", wouldBeUncategorized)}.";
			return;
		}

		IsBusy = true;
		StatusMessage = "Saving...";
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
			StatusMessage = (ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message)
				+ (changed > 0 ? $" ({changed} game(s) were already saved.)" : string.Empty);
			return;
		}
		finally
		{
			IsBusy = false;
		}

		var total = Games.Count(g => g.IsSelected);
		Finished?.Invoke(this, $"\"{_category.Name}\" now has {total} game{(total == 1 ? "" : "s")}.");
	}
}
