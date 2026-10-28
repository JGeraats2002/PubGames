using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>A category with how many published games are in it.</summary>
/// <param name="CanReorder">Admins move it up and down with the arrows on its row.</param>
public record CategoryRow(GameCategory Category, int GameCount, bool CanReorder)
{
	/// <summary>The category's name.</summary>
	public string Name => Category.Name;
	/// <summary>"1 game" or "4 games": how many games are in it.</summary>
	public string CountLabel => GameCount == 1 ? L.T("1 game") : L.F("{0} games", GameCount);
}

/// <summary>A main category with its categories; Group is null for the categories that aren't filed under one yet.</summary>
public class CategorySection(CategoryGroup? group, IEnumerable<CategoryRow> rows) : List<CategoryRow>(rows)
{
	/// <summary>The main category, or null for categories without one.</summary>
	public CategoryGroup? Group { get; } = group;
	/// <summary>The main category's name.</summary>
	public string Name => Group?.Name ?? L.T("No main category yet");
	/// <summary>Whether the main category has no categories yet.</summary>
	public bool IsEmpty => Count == 0;

	/// <summary>Tapping a main category changes it (admins) or proposes a change (moderators).</summary>
	public bool CanEdit { get; } = group is not null;

}

/// <summary>
/// Admin tab > Categories, listed under their main categories. Admins add
/// categories and main categories directly, rename and delete both, put the
/// categories in order, pick a category's games or move it to another main category.
/// Moderators propose all of that (except the order and the games) as review
/// requests; nothing changes until an admin approves.
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

	/// <summary>The main categories with their categories.</summary>
	public ObservableCollection<CategorySection> Sections { get; } = new();

	/// <summary>The main categories a category can be filed under, in their order.</summary>
	public List<CategoryGroup> Groups { get; private set; } = new();

	private List<GameCategory> _allCategories = new();

	/// <summary>The user is an admin: changes happen directly instead of as requests.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(AddButtonText), nameof(AddGroupButtonText))]
	private bool isAdmin;

	/// <summary>"+ New category" for admins, "+ Propose a new category" for moderators.</summary>
	public string AddButtonText => IsAdmin ? L.T("+ New category") : L.T("+ Propose a new category");

	/// <summary>"+ New main category" for admins, "+ Propose a new main category" for moderators.</summary>
	public string AddGroupButtonText => IsAdmin ? L.T("+ New main category") : L.T("+ Propose a new main category");

	/// <summary>What happened, or why it couldn't.</summary>
	[ObservableProperty]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	/// <summary>
	/// Gets the services it works with - the phone's database, cloud sync, the categories, the permissions,
	/// the team and the sign-in - from the app (dependency injection).
	/// </summary>
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

	/// <summary>Shows the categories as saved on the phone, then refreshes them from the cloud.</summary>
	public async Task LoadAsync()
	{
		IsAdmin = _team.Me is { } me && _permissions.CanChangeGamesDirectly(me);
		await ShowCachedAsync();
		try
		{
			if (IsAdmin)
				await _categories.EnsureDefaultsAsync();
			await _categories.PullAsync();
			await _categories.PullGroupsAsync();
			await ShowCachedAsync();
			StatusMessage = string.Empty;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException
				? L.T("Offline - showing the categories saved on this phone.")
				: L.F("Couldn't refresh categories: {0}", ex.Message);
		}
	}

	/// <summary>Shows the main categories with their categories, as saved on the phone.</summary>
	private async Task ShowCachedAsync()
	{
		_allCategories = await _categories.GetCachedAsync();
		Groups = await _categories.GetCachedGroupsAsync();
		var games = (await _local.GetManageableGamesAsync()).Where(g => g.Status == GameStatus.Published).ToList();

		Sections.Clear();
		foreach (var (group, categories) in CategoryGroup.Arrange(Groups, _allCategories))
			Sections.Add(new CategorySection(group,
				categories.Select(c => new CategoryRow(c, games.Count(g => g.CategoryIds.Contains(c.Id)), IsAdmin))));
	}

	/// <summary>Whether the name can be used for a new category in this main category (null: not chosen yet); if not, StatusMessage says why.</summary>
	public bool CheckNewName(string name, CategoryGroup? group)
	{
		StatusMessage = _categories.ValidateNewName(name, group?.Id ?? L.T("(not chosen yet)"), _allCategories) ?? string.Empty;
		return StatusMessage.Length == 0;
	}

	/// <summary>
	/// Admins: adds it now. Moderators: submits a "New category" review request.
	/// Returns what to tell the user.
	/// </summary>
	/// <param name="group">The main category it's filed under; null only while no main categories exist.</param>
	public async Task<string?> AddAsync(string name, CategoryGroup? group)
	{
		if (!CheckNewName(name, group)) return null;
		name = GameCategory.NormalizeName(name);
		var groupId = group?.Id ?? string.Empty;

		StatusMessage = IsAdmin ? L.T("Adding category...") : L.T("Submitting for review...");
		try
		{
			if (IsAdmin)
			{
				await _categories.AddAsync(name, groupId);
				await ShowCachedAsync();
				StatusMessage = string.Empty;
				return L.F("\"{0}\" has been added. Choose its games via the request on the Requests page, or by tapping the category here.", name);
			}

			await SubmitAsync(new ReviewRequest
			{
				Type = ReviewRequestType.NewCategory,
				// Reserved now, so the category keeps this id once approved.
				CategoryId = Guid.NewGuid().ToString("N"),
				ProposedName = name,
				ProposedGroupId = groupId,
				SubjectName = name
			});
			StatusMessage = string.Empty;
			return L.F("\"{0}\" has been submitted for review. You'll get the admin's decision in your Inbox.", name);
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
			return null;
		}
	}

	/// <summary>
	/// Admins: adds the main category now. Moderators: submits a "New main
	/// category" review request. Returns what to tell the user.
	/// </summary>
	public async Task<string?> AddGroupAsync(string name)
	{
		if (_categories.ValidateNewGroupName(name, Groups) is { } problem)
		{
			StatusMessage = problem;
			return null;
		}
		name = GameCategory.NormalizeName(name);

		StatusMessage = IsAdmin ? L.T("Adding main category...") : L.T("Submitting for review...");
		try
		{
			if (IsAdmin)
			{
				await _categories.AddGroupAsync(name);
				await ShowCachedAsync();
				StatusMessage = string.Empty;
				return L.F("\"{0}\" has been added. Add categories to it with + New category. Once it has categories, every game needs at least one of them.", name);
			}

			await SubmitAsync(new ReviewRequest
			{
				Type = ReviewRequestType.NewCategoryGroup,
				// Reserved now, so the main category keeps this id once approved.
				CategoryId = Guid.NewGuid().ToString("N"),
				ProposedName = name,
				SubjectName = name
			});
			StatusMessage = string.Empty;
			return L.F("\"{0}\" has been submitted for review. You'll get the admin's decision in your Inbox.", name);
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
			return null;
		}
	}

	// Changes to what's there. Admins make them straight away (these then return null);
	// moderators propose them, and get back what to tell them.

	/// <summary>Files a category under another main category.</summary>
	public async Task<string?> MoveAsync(GameCategory category, CategoryGroup group)
	{
		if (_categories.ValidateNewName(category.Name, group.Id, _allCategories.Where(c => c.Id != category.Id)) is { } problem)
		{
			StatusMessage = problem;
			return null;
		}

		if (!IsAdmin)
			return await ProposeAsync(new ReviewRequest
			{
				Type = ReviewRequestType.MoveCategory,
				CategoryId = category.Id,
				SubjectName = category.Name,
				ProposedGroupId = group.Id,
				ProposedName = group.Name
			}, L.F("Moving \"{0}\" to \"{1}\"", category.Name, group.Name));

		await RunAsync(L.T("Moving category..."), () => _categories.MoveAsync(category, group.Id));
		return null;
	}

	/// <summary>Gives a category another name.</summary>
	public async Task<string?> RenameAsync(GameCategory category, string name)
	{
		if (_categories.ValidateNewName(name, category.GroupId, _allCategories.Where(c => c.Id != category.Id)) is { } problem)
		{
			StatusMessage = problem;
			return null;
		}

		if (!IsAdmin)
			return await ProposeAsync(new ReviewRequest
			{
				Type = ReviewRequestType.RenameCategory,
				CategoryId = category.Id,
				SubjectName = category.Name,
				ProposedName = GameCategory.NormalizeName(name)
			}, L.F("Renaming \"{0}\"", category.Name));

		await RunAsync(L.T("Renaming category..."), () => _categories.RenameAsync(category, name));
		return null;
	}

	/// <summary>Deletes a category, which also comes off every game.</summary>
	public async Task<string?> DeleteAsync(GameCategory category)
	{
		if (!IsAdmin)
			return await ProposeAsync(new ReviewRequest
			{
				Type = ReviewRequestType.DeleteCategory,
				CategoryId = category.Id,
				SubjectName = category.Name
			}, L.F("Deleting \"{0}\"", category.Name));

		await RunAsync(L.T("Deleting category..."), () => _categories.DeleteAsync(category));
		return null;
	}

	/// <summary>Moderators: sends a change to the admins. Returns what to tell the moderator, or null when it failed (StatusMessage says why).</summary>
	private async Task<string?> ProposeAsync(ReviewRequest request, string what)
	{
		StatusMessage = L.T("Submitting for review...");
		try
		{
			await SubmitAsync(request);
			StatusMessage = string.Empty;
			return L.F("{0} has been submitted for review. Nothing changes until an admin approves it; you'll get the decision in your Inbox.", what);
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
			return null;
		}
	}

	/// <summary>Whether the category can go one place up (-1) or down (+1) within its main category.</summary>
	public static bool CanReorder(CategorySection section, CategoryRow row, int direction) =>
		section.IndexOf(row) + direction is var to && to >= 0 && to < section.Count;

	/// <summary>Admins: moves a category one place up (-1) or down (+1) within its main category.</summary>
	public Task ReorderAsync(CategorySection section, CategoryRow row, int direction)
	{
		if (!CanReorder(section, row, direction)) return Task.CompletedTask;

		var order = section.Select(r => r.Category).ToList();
		var from = order.IndexOf(row.Category);
		(order[from], order[from + direction]) = (order[from + direction], order[from]);
		return RunAsync(L.T("Moving category..."), () => _categories.ReorderAsync(order));
	}

	/// <summary>Runs a change and shows the result. False when it didn't work (StatusMessage says why).</summary>
	private async Task<bool> RunAsync(string busyMessage, Func<Task> change)
	{
		StatusMessage = busyMessage;
		var done = false;
		try
		{
			await change();
			StatusMessage = string.Empty;
			done = true;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
		}
		// Also after a failure: part of it may have been saved already.
		await ShowCachedAsync();
		return done;
	}

	/// <summary>Gives a main category another name.</summary>
	public async Task<string?> RenameGroupAsync(CategoryGroup group, string name)
	{
		if (_categories.ValidateNewGroupName(name, Groups.Where(g => g.Id != group.Id)) is { } problem)
		{
			StatusMessage = problem;
			return null;
		}

		if (!IsAdmin)
			return await ProposeAsync(new ReviewRequest
			{
				Type = ReviewRequestType.RenameCategoryGroup,
				CategoryId = group.Id,
				SubjectName = group.Name,
				ProposedName = GameCategory.NormalizeName(name)
			}, L.F("Renaming \"{0}\"", group.Name));

		await RunAsync(L.T("Renaming main category..."), () => _categories.RenameGroupAsync(group, name));
		return null;
	}

	/// <summary>Deleting the last one would bring the default main categories back.</summary>
	public bool CanDeleteGroup => Groups.Count > 1;

	/// <summary>Deletes a main category and its categories, which also come off every game.</summary>
	public async Task<string?> DeleteGroupAsync(CategoryGroup group)
	{
		if (!IsAdmin)
			return await ProposeAsync(new ReviewRequest
			{
				Type = ReviewRequestType.DeleteCategoryGroup,
				CategoryId = group.Id,
				SubjectName = group.Name
			}, L.F("Deleting \"{0}\"", group.Name));

		await RunAsync(L.T("Deleting main category..."), () => _categories.DeleteGroupAsync(group));
		return null;
	}

	/// <summary>Moderators: sends a change of the categories to an admin as a review request.</summary>
	private Task SubmitAsync(ReviewRequest request)
	{
		var user = _auth.CurrentUser!;
		request.TeamId = CurrentTeamId;
		request.SubmittedByUserId = user.Uid;
		request.SubmittedByEmail = TeamMember.NormalizeEmail(user.Email);
		request.SubmittedByName = user.DisplayName;
		return _cloud.SaveReviewRequestAsync(request);
	}
}
