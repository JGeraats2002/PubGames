using PubGames.Models;

namespace PubGames.Services;

/// <summary>
/// Game categories: the cloud "categories" collection (everyone reads, admins
/// write), cached on the phone. Adding a category - by an admin directly, or by
/// approving a moderator's New category request - also creates an "Add games
/// to category" review request, so an admin picks which games belong in it.
/// </summary>
public interface ICategoryService
{
	/// <summary>This phone's copy, alphabetical.</summary>
	Task<List<GameCategory>> GetCachedAsync();

	/// <summary>Refreshes this phone's copy from the cloud. Throws when offline.</summary>
	Task<List<GameCategory>> PullAsync();

	/// <summary>Admins: creates the default categories (Family, Pub, Cards, ...) if there are none at all yet.</summary>
	Task EnsureDefaultsAsync();

	/// <summary>
	/// Admins: adds the category and files the "Add games to category" request.
	/// Throws InvalidOperationException if the name is empty or already used.
	/// </summary>
	/// <param name="id">Keeps the id a moderator's New category request reserved.</param>
	Task<GameCategory> AddAsync(string name, string? id = null);

	/// <summary>Null when the name is fine; otherwise why it can't be used.</summary>
	string? ValidateNewName(string name, IEnumerable<GameCategory> existing);

	/// <summary>"Cards, Pub" for a game's category ids (unknown ids are skipped).</summary>
	string LabelFor(IEnumerable<string> categoryIds, IEnumerable<GameCategory> categories);
}

public class CategoryService : ICategoryService
{
	private const string Collection = "categories";

	// TODO: replace with the team the user picked once multiple teams exist.
	private const string CurrentTeamId = AuthService.DefaultTeamId;

	private readonly ILocalDatabaseService _local;
	private readonly FirestoreClient _firestore;
	private readonly ICloudSyncService _cloud;
	private readonly IAuthService _auth;

	public CategoryService(ILocalDatabaseService local, FirestoreClient firestore, ICloudSyncService cloud, IAuthService auth)
	{
		_local = local;
		_firestore = firestore;
		_cloud = cloud;
		_auth = auth;
	}

	public Task<List<GameCategory>> GetCachedAsync() => _local.GetCategoriesAsync();

	public async Task<List<GameCategory>> PullAsync()
	{
		var docs = await _firestore.GetAllAsync(Collection);
		var categories = docs
			.Where(f => f.GetValueOrDefault("id") is string)
			.Select(f => new GameCategory
			{
				Id = (string)f["id"]!,
				Name = f.GetValueOrDefault("name") as string ?? string.Empty,
				CreatedAt = f.GetValueOrDefault("createdAt") as DateTime? ?? DateTime.UtcNow
			})
			.ToList();

		var ids = categories.Select(c => c.Id).ToHashSet();
		foreach (var stale in (await _local.GetCategoriesAsync()).Where(c => !ids.Contains(c.Id)))
			await _local.DeleteCategoryAsync(stale.Id);
		foreach (var category in categories)
			await _local.SaveCategoryAsync(category);

		return categories.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
	}

	public async Task EnsureDefaultsAsync()
	{
		if ((await PullAsync()).Count > 0) return;

		foreach (var name in GameCategory.Defaults)
			await AddAsync(name);
	}

	public async Task<GameCategory> AddAsync(string name, string? id = null)
	{
		var existing = await PullAsync();
		name = GameCategory.NormalizeName(name);
		if (ValidateNewName(name, existing) is { } problem)
			throw new InvalidOperationException(problem);

		var category = new GameCategory { Name = name };
		if (!string.IsNullOrEmpty(id))
			category.Id = id;

		await _firestore.SetAsync(Collection, category.Id, new Dictionary<string, object?>
		{
			["id"] = category.Id,
			["name"] = category.Name,
			["createdAt"] = category.CreatedAt
		});
		await _local.SaveCategoryAsync(category);

		// So the new category doesn't stay empty: every admin sees this on the Requests page.
		var admin = _auth.CurrentUser!;
		await _cloud.SaveReviewRequestAsync(new ReviewRequest
		{
			Type = ReviewRequestType.AddGamesToCategory,
			CategoryId = category.Id,
			SubjectName = category.Name,
			TeamId = CurrentTeamId,
			SubmittedByUserId = admin.Uid,
			SubmittedByEmail = TeamMember.NormalizeEmail(admin.Email),
			SubmittedByName = "Pub Games (automatic)"
		});

		return category;
	}

	public string? ValidateNewName(string name, IEnumerable<GameCategory> existing)
	{
		name = GameCategory.NormalizeName(name);
		if (name.Length == 0)
			return "Give the category a name.";
		if (name.Length > 30)
			return "Keep category names short (30 characters at most).";
		if (existing.Any(c => c.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
			return $"There's already a category called \"{name}\".";
		return null;
	}

	public string LabelFor(IEnumerable<string> categoryIds, IEnumerable<GameCategory> categories)
	{
		var names = categories.ToDictionary(c => c.Id, c => c.Name);
		return string.Join(", ", categoryIds.Where(names.ContainsKey).Select(id => names[id]).Order(StringComparer.CurrentCultureIgnoreCase));
	}
}
