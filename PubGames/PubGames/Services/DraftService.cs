using PubGames.Models;

namespace PubGames.Services;

/// <summary>
/// Games that are being made (GameDraft), in the cloud "drafts" collection:
/// every admin and moderator reads them; only the owner writes theirs, and
/// anyone on the team can take over a draft nobody worked on for 3 days
/// (firestore.rules checks both). The cloud (announceStaleDrafts) puts such
/// a draft in everyone's Inbox once.
/// </summary>
public sealed class DraftService(FirestoreClient firestore, IAuthService auth)
{
	private const string Collection = "drafts";

	/// <summary>The signed-in user's lower-case email.</summary>
	public string MyEmail => TeamMember.NormalizeEmail(auth.CurrentUser?.Email ?? string.Empty);

	private string MyName => auth.CurrentUser is { } user && !string.IsNullOrWhiteSpace(user.DisplayName) ? user.DisplayName : MyEmail;

	/// <summary>Every draft, the ones changed most recently first. Throws when offline.</summary>
	public async Task<List<GameDraft>> PullAllAsync()
	{
		var docs = await firestore.GetAllAsync(Collection);
		return docs.Select(ToDraft).OfType<GameDraft>().OrderByDescending(d => d.UpdatedAt).ToList();
	}

	/// <summary>One draft, or null when it's gone (finished or removed).</summary>
	public async Task<GameDraft?> GetAsync(string id) =>
		await firestore.GetAsync(Collection, id) is { } doc ? ToDraft(doc) : null;

	/// <summary>
	/// Saves the form as a draft of the signed-in user: a new one (draftId
	/// null) or theirs again. Returns the draft's id.
	/// </summary>
	public async Task<string> SaveAsync(string? draftId, PubGame game, DateTime? createdAt = null, string? createdByEmail = null)
	{
		var id = draftId ?? Guid.NewGuid().ToString("N");
		await firestore.SetAsync(Collection, id, new Dictionary<string, object?>
		{
			["id"] = id,
			["name"] = game.Name,
			["ownerEmail"] = MyEmail,
			["ownerName"] = MyName,
			["createdByEmail"] = createdByEmail ?? MyEmail,
			["createdAt"] = createdAt ?? DateTime.UtcNow,
			["updatedAt"] = DateTime.UtcNow,
			["game"] = CloudSyncService.GameFields(game)
		});
		return id;
	}

	/// <summary>
	/// Takes over a draft nobody worked on for 3 days: from now on the
	/// signed-in user changes and finishes it. The cloud refuses it when it
	/// isn't 3 days untouched (any more).
	/// </summary>
	public async Task TakeOverAsync(GameDraft draft)
	{
		await firestore.UpdateFieldsAsync(Collection, draft.Id, new Dictionary<string, object?>
		{
			["ownerEmail"] = MyEmail,
			["ownerName"] = MyName,
			["updatedAt"] = DateTime.UtcNow
		});
		draft.OwnerEmail = MyEmail;
		draft.OwnerName = MyName;
		draft.UpdatedAt = DateTime.UtcNow;
	}

	/// <summary>Removes a draft: it's finished (published or submitted), or its owner doesn't want it any more.</summary>
	public Task DeleteAsync(string id) => firestore.DeleteAsync(Collection, id);

	/// <summary>A draft from its cloud document; null when it isn't one.</summary>
	private static GameDraft? ToDraft(Dictionary<string, object?> f)
	{
		if (f.GetValueOrDefault("id") is not string id || f.GetValueOrDefault("game") is not Dictionary<string, object?> fields
			|| CloudSyncService.ToGame(fields) is not { } game)
			return null;
		return new GameDraft
		{
			Id = id,
			Game = game,
			OwnerEmail = f.GetValueOrDefault("ownerEmail") as string ?? string.Empty,
			OwnerName = f.GetValueOrDefault("ownerName") as string ?? string.Empty,
			CreatedByEmail = f.GetValueOrDefault("createdByEmail") as string ?? string.Empty,
			CreatedAt = f.GetValueOrDefault("createdAt") as DateTime? ?? DateTime.UtcNow,
			UpdatedAt = f.GetValueOrDefault("updatedAt") as DateTime? ?? DateTime.UtcNow
		};
	}
}
