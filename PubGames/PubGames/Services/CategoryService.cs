using PubGames.Models;

namespace PubGames.Services;

/// <summary>
/// Game categories and the main categories they're filed under: the cloud
/// "categories" and "categoryGroups" collections (everyone reads, admins
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

	/// <summary>This phone's copy of the main categories, in their order.</summary>
	Task<List<CategoryGroup>> GetCachedGroupsAsync();

	/// <summary>Refreshes this phone's copy of the main categories from the cloud. Throws when offline.</summary>
	Task<List<CategoryGroup>> PullGroupsAsync();

	/// <summary>
	/// Admins: creates the default main categories (Play style, Duration, ...)
	/// with their categories if there are no main categories at all yet, and
	/// tidies up: categories without a main category and doubles are removed.
	/// </summary>
	Task EnsureDefaultsAsync();

	/// <summary>
	/// Admins: adds the category and files the "Add games to category" request.
	/// Throws InvalidOperationException if the name is empty or already used.
	/// </summary>
	/// <param name="groupId">The main category it's filed under.</param>
	/// <param name="id">Keeps the id a moderator's New category request reserved.</param>
	Task<GameCategory> AddAsync(string name, string groupId, string? id = null);

	/// <summary>Admins: files an existing category under another main category, as its last one.</summary>
	Task MoveAsync(GameCategory category, string groupId);

	/// <summary>Admins: renames a category. Throws InvalidOperationException if the name is empty or already used.</summary>
	Task RenameAsync(GameCategory category, string name);

	/// <summary>Admins: deletes a category and takes it off every game.</summary>
	Task DeleteAsync(GameCategory category);

	/// <summary>Admins: puts one main category's categories in this order.</summary>
	Task ReorderAsync(IReadOnlyList<GameCategory> ordered);


	/// <summary>
	/// Admins: adds a main category. Throws InvalidOperationException if the
	/// name is empty or already used.
	/// </summary>
	/// <param name="id">Keeps the id a moderator's New main category request reserved.</param>
	Task<CategoryGroup> AddGroupAsync(string name, string? id = null);

	/// <summary>Admins: renames a main category. Throws InvalidOperationException if the name is empty or already used.</summary>
	Task RenameGroupAsync(CategoryGroup group, string name);

	/// <summary>Admins: deletes a main category together with its categories, which are also taken off every game.</summary>
	Task DeleteGroupAsync(CategoryGroup group);

	/// <summary>
	/// Null when the name is fine; otherwise why it can't be used. A name may
	/// be used once in each main category (so "Other" can be in several).
	/// </summary>
	/// <param name="groupId">The main category the name is for.</param>
	/// <param name="existing">The categories there are, without the one being renamed or moved.</param>
	string? ValidateNewName(string name, string groupId, IEnumerable<GameCategory> existing);

	/// <summary>Null when the name is fine; otherwise why it can't be used.</summary>
	string? ValidateNewGroupName(string name, IEnumerable<CategoryGroup> existing);

	/// <summary>"Cards, Pub" for a game's category ids (unknown ids are skipped).</summary>
	string LabelFor(IEnumerable<string> categoryIds, IEnumerable<GameCategory> categories);
}

/// <summary>
/// Keeps the categories and main categories: from the cloud, cached on the phone, and changed by admins
/// directly or by moderators through review requests.
/// </summary>
public class CategoryService : ICategoryService
{
	private const string Collection = "categories";
	private const string GroupsCollection = "categoryGroups";

	// TODO: replace with the team the user picked once multiple teams exist.
	private const string CurrentTeamId = AuthService.DefaultTeamId;

	private readonly ILocalDatabaseService _local;
	private readonly FirestoreClient _firestore;
	private readonly ICloudSyncService _cloud;
	private readonly IAuthService _auth;

	/// <summary>Several pages call EnsureDefaultsAsync when they appear; one at a time, or the defaults are created twice.</summary>
	private readonly SemaphoreSlim _defaultsGate = new(1, 1);

	/// <summary>Set once this phone has checked that every category has its "Add games to category" request.</summary>
	private const string RequestsCheckedKey = "categories.addGamesRequestsChecked";

	/// <summary>
	/// Gets the services it works with - the phone's database, the cloud, cloud sync and the sign-in - from
	/// the app (dependency injection).
	/// </summary>
	public CategoryService(ILocalDatabaseService local, FirestoreClient firestore, ICloudSyncService cloud, IAuthService auth)
	{
		_local = local;
		_firestore = firestore;
		_cloud = cloud;
		_auth = auth;
	}

	/// <inheritdoc />
	public Task<List<GameCategory>> GetCachedAsync() => _local.GetCategoriesAsync();

	/// <inheritdoc />
	public async Task<List<GameCategory>> PullAsync()
	{
		var docs = await _firestore.GetAllAsync(Collection);
		var categories = docs
			.Where(f => f.GetValueOrDefault("id") is string)
			.Select(f => new GameCategory
			{
				Id = (string)f["id"]!,
				Name = f.GetValueOrDefault("name") as string ?? string.Empty,
				TranslationsText = ReadTranslations(f),
				GroupId = f.GetValueOrDefault("groupId") as string ?? string.Empty,
				SortOrder = f.GetValueOrDefault("sortOrder") is long place ? (int)place : 0,
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

	/// <inheritdoc />
	public async Task<List<CategoryGroup>> GetCachedGroupsAsync() => CategoryGroup.InOrder(await _local.GetCategoryGroupsAsync());

	/// <inheritdoc />
	public async Task<List<CategoryGroup>> PullGroupsAsync()
	{
		var docs = await _firestore.GetAllAsync(GroupsCollection);
		var groups = docs
			// Ids that start with "_" are notes for the app itself (see SortOnceAsync), not main categories.
			.Where(f => f.GetValueOrDefault("id") is string id && !id.StartsWith('_'))
			.Select(f => new CategoryGroup
			{
				Id = (string)f["id"]!,
				Name = f.GetValueOrDefault("name") as string ?? string.Empty,
				TranslationsText = ReadTranslations(f),
				SortOrder = f.GetValueOrDefault("sortOrder") is long place ? (int)place : 0,
				CreatedAt = f.GetValueOrDefault("createdAt") as DateTime? ?? DateTime.UtcNow
			})
			.ToList();

		var ids = groups.Select(g => g.Id).ToHashSet();
		foreach (var stale in (await _local.GetCategoryGroupsAsync()).Where(g => !ids.Contains(g.Id)))
			await _local.DeleteCategoryGroupAsync(stale.Id);
		foreach (var group in groups)
			await _local.SaveCategoryGroupAsync(group);

		return CategoryGroup.InOrder(groups);
	}

	/// <inheritdoc />
	public async Task EnsureDefaultsAsync()
	{
		await _defaultsGate.WaitAsync();
		try
		{
			var categories = await PullAsync();
			var groups = await PullGroupsAsync();
			if (groups.Count == 0)
			{
				await CreateDefaultsAsync(categories);
				categories = await GetCachedAsync();
				groups = await GetCachedGroupsAsync();
			}

			if (groups.FirstOrDefault(g => g.Id == CategoryGroup.RetiredPlayersId) is { } retired)
			{
				await DeleteGroupAsync(retired);
				categories = await GetCachedAsync();
				groups = await GetCachedGroupsAsync();
			}

			categories = await TidyAsync(groups, categories);
			await FileMissingRequestsAsync(categories);
			await SortOnceAsync(groups, categories);
		}
		finally
		{
			_defaultsGate.Release();
		}
	}

	/// <summary>
	/// Adds the starting categories of a main category (e.g. Duration: short, medium, long) when they don't
	/// exist yet.
	/// </summary>
	private async Task CreateDefaultsAsync(List<GameCategory> categories)
	{
		// The categories first and the main categories last: if this stops halfway
		// there are still no main categories, so the next time finishes the job.
		// Fixed ids, so two admins doing this at the same time don't create doubles.
		foreach (var (groupId, _, names) in CategoryGroup.Defaults)
		{
			for (var position = 0; position < names.Length; position++)
			{
				var existing = categories.FirstOrDefault(c => c.Name.Equals(names[position], StringComparison.CurrentCultureIgnoreCase));
				if (existing is null)
					await FileAddGamesRequestAsync(await SaveAsync(new GameCategory { Id = $"{groupId}{position}", Name = names[position], GroupId = groupId, SortOrder = position }));
				else if (string.IsNullOrEmpty(existing.GroupId))
					await MoveAsync(existing, groupId);
			}
		}

		var place = 0;
		foreach (var (groupId, name, _) in CategoryGroup.Defaults)
			await SaveGroupAsync(new CategoryGroup { Id = groupId, Name = name, SortOrder = place++ });
	}

	/// <summary>
	/// Every category belongs to a main category and is there once: removes
	/// categories without a main category (or whose main category is gone), and
	/// doubles - games in a double move to the one that's kept, the oldest.
	/// Returns the categories that are left.
	/// </summary>
	private async Task<List<GameCategory>> TidyAsync(List<CategoryGroup> groups, List<GameCategory> categories)
	{
		if (groups.Count == 0) return categories;

		var groupIds = groups.Select(g => g.Id).ToHashSet();
		var kept = new List<GameCategory>();
		foreach (var category in categories.OrderBy(c => c.CreatedAt))
		{
			if (string.IsNullOrEmpty(category.GroupId) || !groupIds.Contains(category.GroupId))
			{
				await DeleteAsync(category, replacement: null);
				continue;
			}

			var original = kept.FirstOrDefault(k => k.GroupId == category.GroupId
				&& k.Name.Equals(category.Name, StringComparison.CurrentCultureIgnoreCase));
			if (original is not null)
				await DeleteAsync(category, original);
			else
				kept.Add(category);
		}
		return kept;
	}

	/// <summary>Kept in the cloud once the categories were put in A-Z order, so that happens only once, whoever opens the app.</summary>
	private const string SortedOnceId = "_sorted-a-z";

	/// <summary>
	/// Once, ever: puts the main categories, and the categories in each, in
	/// alphabetical order - except where they have an order of their own
	/// (CategoryGroup.HaveOwnOrder). After that the main categories stay in
	/// that order, with new ones at the end; the order of the categories is
	/// the admins': moving them up or down is never undone.
	/// </summary>
	private async Task SortOnceAsync(List<CategoryGroup> groups, List<GameCategory> categories)
	{
		if (await _firestore.GetAsync(GroupsCollection, SortedOnceId) is not null) return;

		await ReorderGroupsAsync(groups.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
		foreach (var group in groups)
		{
			var inGroup = categories.Where(c => c.GroupId == group.Id).ToList();
			if (!CategoryGroup.HaveOwnOrder(inGroup))
				await ReorderAsync(inGroup.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
		}

		await _firestore.SetAsync(GroupsCollection, SortedOnceId, new Dictionary<string, object?>
		{
			["id"] = SortedOnceId,
			["name"] = "Sorted A-Z once",
			["createdAt"] = DateTime.UtcNow
		});
	}

	/// <summary>
	/// Once per phone: categories that have no games yet and never got their
	/// "Add games to category" request get one now.
	/// </summary>
	private async Task FileMissingRequestsAsync(List<GameCategory> categories)
	{
		if (Preferences.Default.Get(RequestsCheckedKey, false)) return;

		var games = await _local.GetManageableGamesAsync();
		var waiting = await _local.GetRequestsWaitingForReviewAsync(CurrentTeamId);
		foreach (var category in categories.OrderBy(c => c.CreatedAt))
		{
			if (games.Any(g => g.CategoryIds.Contains(category.Id))
				|| waiting.Any(r => r.Type == ReviewRequestType.AddGamesToCategory && r.CategoryId == category.Id)
				|| await _firestore.GetAsync(CloudSyncService.ReviewRequestsCollection, AddGamesRequestId(category)) is not null)
				continue;
			await FileAddGamesRequestAsync(category);
		}

		Preferences.Default.Set(RequestsCheckedKey, true);
	}

	/// <inheritdoc />
	public async Task<GameCategory> AddAsync(string name, string groupId, string? id = null)
	{
		var existing = await PullAsync();
		name = GameCategory.NormalizeName(name);
		if (ValidateNewName(name, groupId, existing) is { } problem)
			throw new InvalidOperationException(problem);

		var category = new GameCategory { Name = name, GroupId = groupId, SortOrder = NextPlace(existing, groupId) };
		if (!string.IsNullOrEmpty(id))
			category.Id = id;
		await SaveAsync(category);
		await FileAddGamesRequestAsync(category);

		return category;
	}

	/// <summary>
	/// The id of a category's "add games to this category" request: one per category, so it isn't filed
	/// twice.
	/// </summary>
	private static string AddGamesRequestId(GameCategory category) => $"addgames-{category.Id}";

	/// <summary>So a new category doesn't stay empty: every admin sees this on the Requests page.</summary>
	private Task FileAddGamesRequestAsync(GameCategory category)
	{
		var admin = _auth.CurrentUser!;
		return _cloud.SaveReviewRequestAsync(new ReviewRequest
		{
			// One per category, however often this is called.
			Id = AddGamesRequestId(category),
			Type = ReviewRequestType.AddGamesToCategory,
			CategoryId = category.Id,
			SubjectName = category.Name,
			TeamId = CurrentTeamId,
			SubmittedByUserId = admin.Uid,
			SubmittedByEmail = TeamMember.NormalizeEmail(admin.Email),
			SubmittedByName = L.T("Pub Games (automatic)")
		});
	}

	/// <summary>
	/// Removes a category: off every game (or swapped for the replacement),
	/// its waiting "Add games to category" request withdrawn.
	/// </summary>
	private async Task DeleteAsync(GameCategory category, GameCategory? replacement)
	{
		foreach (var game in (await _local.GetManageableGamesAsync()).Where(g => g.CategoryIds.Contains(category.Id)))
		{
			var ids = game.CategoryIds.Where(id => id != category.Id).ToList();
			if (replacement is not null)
				ids.Add(replacement.Id);
			game.CategoryIds = ids;
			// Only this field: this phone's copy of the rest of the game may be out of date.
			await _firestore.UpdateFieldsAsync(CloudSyncService.GamesCollection, game.Id,
				new Dictionary<string, object?> { ["categoryIds"] = game.CategoryIds });
			await _local.SaveGameAsync(game);
		}

		foreach (var request in (await _local.GetRequestsWaitingForReviewAsync(CurrentTeamId))
			.Where(r => r.Type == ReviewRequestType.AddGamesToCategory && r.CategoryId == category.Id))
			await _cloud.WithdrawReviewRequestAsync(request);

		await _firestore.DeleteAsync(Collection, category.Id);
		await _local.DeleteCategoryAsync(category.Id);
	}

	/// <inheritdoc />
	public async Task MoveAsync(GameCategory category, string groupId)
	{
		var others = (await GetCachedAsync()).Where(c => c.Id != category.Id).ToList();
		if (ValidateNewName(category.Name, groupId, others) is { } problem)
			throw new InvalidOperationException(problem);

		category.SortOrder = NextPlace(others, groupId);
		category.GroupId = groupId;
		await SaveAsync(category);
	}

	/// <summary>The place after the last category of a main category.</summary>
	private static int NextPlace(IEnumerable<GameCategory> categories, string groupId) =>
		categories.Where(c => c.GroupId == groupId).Select(c => c.SortOrder).DefaultIfEmpty(-1).Max() + 1;

	/// <inheritdoc />
	public async Task RenameAsync(GameCategory category, string name)
	{
		var others = (await PullAsync()).Where(c => c.Id != category.Id);
		name = GameCategory.NormalizeName(name);
		if (ValidateNewName(name, category.GroupId, others) is { } problem)
			throw new InvalidOperationException(problem);

		category.Name = name;
		await SaveAsync(category);

		// Its request on the Requests page shows the new name too.
		foreach (var request in (await _local.GetRequestsWaitingForReviewAsync(CurrentTeamId))
			.Where(r => r.Type == ReviewRequestType.AddGamesToCategory && r.CategoryId == category.Id))
		{
			request.SubjectName = name;
			await _cloud.SaveReviewRequestAsync(request);
		}
	}

	/// <inheritdoc />
	public Task DeleteAsync(GameCategory category) => DeleteAsync(category, replacement: null);

	/// <inheritdoc />
	public async Task ReorderAsync(IReadOnlyList<GameCategory> ordered)
	{
		for (var position = 0; position < ordered.Count; position++)
		{
			if (ordered[position].SortOrder == position) continue;
			ordered[position].SortOrder = position;
			await SaveAsync(ordered[position]);
		}
	}

	/// <summary>Puts the main categories in this order (only done once, by SortOnceAsync: they can't be moved by hand).</summary>
	private async Task ReorderGroupsAsync(IReadOnlyList<CategoryGroup> ordered)
	{
		for (var position = 0; position < ordered.Count; position++)
		{
			if (ordered[position].SortOrder == position) continue;
			ordered[position].SortOrder = position;
			await SaveGroupAsync(ordered[position]);
		}
	}

	/// <summary>Saves a category in the cloud and on the phone, and returns it.</summary>
	private async Task<GameCategory> SaveAsync(GameCategory category)
	{
		await _firestore.SetAsync(Collection, category.Id, new Dictionary<string, object?>
		{
			["id"] = category.Id,
			["name"] = category.Name,
			// The language the name is written in: the app's language of whoever saves it.
			["sourceLanguage"] = AppLanguage.Current.Code,
			// Kept while the name stays; the cloud translates a new name again (translateCategory).
			["translations"] = TranslationsToKeep(category.Name, category.TranslationsText),
			["translatedFrom"] = TranslatedFrom(category.Name, category.TranslationsText),
			["groupId"] = category.GroupId ?? string.Empty,
			["sortOrder"] = category.SortOrder,
			["createdAt"] = category.CreatedAt
		});
		await _local.SaveCategoryAsync(category);
		return category;
	}

	/// <inheritdoc />
	public async Task<CategoryGroup> AddGroupAsync(string name, string? id = null)
	{
		var existing = await PullGroupsAsync();
		name = GameCategory.NormalizeName(name);
		if (ValidateNewGroupName(name, existing) is { } problem)
			throw new InvalidOperationException(problem);

		var group = new CategoryGroup { Name = name, SortOrder = existing.Select(g => g.SortOrder).DefaultIfEmpty(-1).Max() + 1 };
		if (!string.IsNullOrEmpty(id))
			group.Id = id;
		await SaveGroupAsync(group);
		return group;
	}

	/// <inheritdoc />
	public async Task RenameGroupAsync(CategoryGroup group, string name)
	{
		var others = (await PullGroupsAsync()).Where(g => g.Id != group.Id);
		name = GameCategory.NormalizeName(name);
		if (ValidateNewGroupName(name, others) is { } problem)
			throw new InvalidOperationException(problem);

		group.Name = name;
		await SaveGroupAsync(group);
	}

	/// <inheritdoc />
	public async Task DeleteGroupAsync(CategoryGroup group)
	{
		// The categories first: if this stops halfway, deleting again finishes the job.
		foreach (var category in (await PullAsync()).Where(c => c.GroupId == group.Id))
			await DeleteAsync(category, replacement: null);

		await _firestore.DeleteAsync(GroupsCollection, group.Id);
		await _local.DeleteCategoryGroupAsync(group.Id);
	}

	/// <summary>
	/// The translations to save with a category: the ones it has, as long as they're of this name. The cloud
	/// translates again when the name was changed (translatedFrom no longer matches).
	/// </summary>
	private static Dictionary<string, object?> TranslationsToKeep(string name, string? translationsText) =>
		GameTranslation.NamesFromJson(translationsText)
			.Where(t => t.Key != TranslatedFromKey)
			.ToDictionary(t => t.Key, t => (object?)t.Value);

	/// <summary>A category's translated names from its cloud document, with the name they belong to (under "_from").</summary>
	private static string ReadTranslations(Dictionary<string, object?> f)
	{
		var names = GameTranslation.NamesFromMap(f.GetValueOrDefault("translations"));
		if (f.GetValueOrDefault("translatedFrom") is string from)
			names[TranslatedFromKey] = from;
		return GameTranslation.NamesToJson(names);
	}

	/// <summary>
	/// The name the saved translations belong to: this name while there are translations; the cloud compares
	/// it with the name and translates again when they differ. The phone remembers it next to the names.
	/// </summary>
	private static string TranslatedFrom(string name, string? translationsText) =>
		GameTranslation.NamesFromJson(translationsText).GetValueOrDefault(TranslatedFromKey) ?? string.Empty;

	/// <summary>Where the phone keeps translatedFrom among a category's translated names.</summary>
	internal const string TranslatedFromKey = "_from";

	/// <summary>Saves a main category in the cloud and on the phone.</summary>
	private async Task SaveGroupAsync(CategoryGroup group)
	{
		await _firestore.SetAsync(GroupsCollection, group.Id, new Dictionary<string, object?>
		{
			["id"] = group.Id,
			["name"] = group.Name,
			["sourceLanguage"] = AppLanguage.Current.Code,
			["translations"] = TranslationsToKeep(group.Name, group.TranslationsText),
			["translatedFrom"] = TranslatedFrom(group.Name, group.TranslationsText),
			["sortOrder"] = group.SortOrder,
			["createdAt"] = group.CreatedAt
		});
		await _local.SaveCategoryGroupAsync(group);
	}

	/// <inheritdoc />
	public string? ValidateNewName(string name, string groupId, IEnumerable<GameCategory> existing)
	{
		name = GameCategory.NormalizeName(name);
		if (name.Length == 0)
			return L.T("Give the category a name.");
		if (name.Length > 30)
			return L.T("Keep category names short (30 characters at most).");
		if (existing.Any(c => c.GroupId == groupId && c.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
			return L.F("There's already a category called \"{0}\" in this main category.", name);
		return null;
	}

	/// <inheritdoc />
	public string? ValidateNewGroupName(string name, IEnumerable<CategoryGroup> existing)
	{
		name = GameCategory.NormalizeName(name);
		if (name.Length == 0)
			return L.T("Give the main category a name.");
		if (name.Length > 30)
			return L.T("Keep main category names short (30 characters at most).");
		if (existing.Any(g => g.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
			return L.F("There's already a main category called \"{0}\".", name);
		return null;
	}

	/// <inheritdoc />
	public string LabelFor(IEnumerable<string> categoryIds, IEnumerable<GameCategory> categories)
	{
		var names = categories.ToDictionary(c => c.Id, c => c.ShownName);
		return string.Join(", ", categoryIds.Where(names.ContainsKey).Select(id => names[id]).Order(StringComparer.CurrentCultureIgnoreCase));
	}
}
