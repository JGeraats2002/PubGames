using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>A category with how many published games are in it.</summary>
public record CategoryRow(GameCategory Category, int GameCount)
{
	public string Name => Category.Name;
	public string CountLabel => GameCount == 1 ? "1 game" : $"{GameCount} games";
}

/// <summary>
/// Admin tab > Categories. Admins add categories directly (and pick their
/// games); moderators propose them as a "New category" review request.
/// </summary>
public partial class CategoriesViewModel : ObservableObject
{
	private readonly ILocalDatabaseService _local;
	private readonly ICloudSyncService _cloud;
	private readonly ICategoryService _categories;
	private readonly IPermissionService _permissions;
	private readonly ITeamService _team;
	private readonly IAuthService _auth;

	// TODO: replace with the team the user picked once multiple teams exist.
	private const string CurrentTeamId = AuthService.DefaultTeamId;

	public ObservableCollection<CategoryRow> Categories { get; } = new();

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(AddButtonText), nameof(Explanation))]
	private bool isAdmin;

	public string AddButtonText => IsAdmin ? "+ New category" : "+ Propose a new category";

	public string Explanation => IsAdmin
		? "Every game is in at least one category; players filter games by category. Tap a category to choose its games."
		: "Every game is in at least one category; players filter games by category. A new category you propose is reviewed by an admin first.";

	[ObservableProperty]
	private string statusMessage = string.Empty;

	public CategoriesViewModel(ILocalDatabaseService local, ICloudSyncService cloud, ICategoryService categories,
		IPermissionService permissions, ITeamService team, IAuthService auth)
	{
		_local = local;
		_cloud = cloud;
		_categories = categories;
		_permissions = permissions;
		_team = team;
		_auth = auth;
	}

	public async Task LoadAsync()
	{
		IsAdmin = _team.Me is { } me && _permissions.CanChangeGamesDirectly(me);
		await ShowAsync(await _categories.GetCachedAsync());
		try
		{
			if (IsAdmin)
				await _categories.EnsureDefaultsAsync();
			await ShowAsync(await _categories.PullAsync());
			StatusMessage = string.Empty;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException
				? "Offline - showing the categories saved on this phone."
				: $"Couldn't refresh categories: {ex.Message}";
		}
	}

	private async Task ShowAsync(List<GameCategory> categories)
	{
		var games = (await _local.GetManageableGamesAsync()).Where(g => g.Status == GameStatus.Published).ToList();
		Categories.Clear();
		foreach (var category in categories)
			Categories.Add(new CategoryRow(category, games.Count(g => g.CategoryIds.Contains(category.Id))));
	}

	/// <summary>Admins: adds it now. Moderators: submits a "New category" review request. Returns what to tell the user.</summary>
	public async Task<string?> AddAsync(string name)
	{
		var existing = Categories.Select(r => r.Category).ToList();
		if (_categories.ValidateNewName(name, existing) is { } problem)
		{
			StatusMessage = problem;
			return null;
		}
		name = GameCategory.NormalizeName(name);

		StatusMessage = IsAdmin ? "Adding category..." : "Submitting for review...";
		try
		{
			if (IsAdmin)
			{
				await _categories.AddAsync(name);
				await ShowAsync(await _categories.GetCachedAsync());
				StatusMessage = string.Empty;
				return $"\"{name}\" is added. Choose its games now via the request on the Requests page, or by tapping the category here.";
			}

			var user = _auth.CurrentUser!;
			await _cloud.SaveReviewRequestAsync(new ReviewRequest
			{
				Type = ReviewRequestType.NewCategory,
				// Reserved now, so the category keeps this id once approved.
				CategoryId = Guid.NewGuid().ToString("N"),
				TeamId = CurrentTeamId,
				ProposedName = name,
				SubjectName = name,
				SubmittedByUserId = user.Uid,
				SubmittedByEmail = TeamMember.NormalizeEmail(user.Email),
				SubmittedByName = user.DisplayName
			});
			StatusMessage = string.Empty;
			return $"\"{name}\" is submitted for review. You'll get the admin's decision in your Inbox.";
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message;
			return null;
		}
	}
}
