using PubGames.Models;

namespace PubGames.Services;

public interface ICloudSyncService
{
	/// <summary>Pull anything the account owns (players, purchases, sessions) from the cloud into local storage.</summary>
	Task PullAccountDataAsync(string accountId);

	/// <summary>Push local, not-yet-synced sessions/scores up to the cloud.</summary>
	Task PushPendingSessionsAsync();

	/// <summary>
	/// Verifies a Google Play Billing purchase token against your backend, and
	/// only THEN writes the purchase record - the backend is the source of
	/// truth for "owned", never the device alone.
	/// </summary>
	Task<bool> VerifyAndRecordPurchaseAsync(string accountId, string gameId, string playBillingToken);

	/// <summary>
	/// Writes the game to Firestore so every user sees its current state
	/// (published, deleted, ...). Throws if offline or not allowed - callers
	/// should only commit the change locally after this succeeds.
	/// </summary>
	Task SaveGameAsync(PubGame game);

	/// <summary>
	/// Replaces the local copy of the public library with what's published in
	/// the cloud. Throws when offline; the local copy then stays as it was.
	/// </summary>
	Task PullPublishedGamesAsync();

	/// <summary>Admins only: downloads every game including drafts and deleted ones, for the host library.</summary>
	Task PullAllGamesAsync();

	Task SubmitApprovalRequestAsync(ApprovalRequest request);
	Task ResolveApprovalRequestAsync(string requestId, bool approved, string resolvedByUserId);
}

/// <summary>
/// Placeholder implementation. Replace the bodies below with real HTTP calls
/// (or Firebase SDK calls) to your backend once it exists. Kept as a stub so
/// the rest of the app - and the offline-first local flow - works and is
/// testable before the backend is built.
/// </summary>
public class CloudSyncService : ICloudSyncService
{
	private const string GamesCollection = "games";

	private readonly ILocalDatabaseService _local;
	private readonly FirestoreClient _firestore;

	public CloudSyncService(ILocalDatabaseService local, FirestoreClient firestore)
	{
		_local = local;
		_firestore = firestore;
	}

	public Task SaveGameAsync(PubGame game) =>
		_firestore.SetAsync(GamesCollection, game.Id, new Dictionary<string, object?>
		{
			// Also the document id; duplicated as a field so reads don't have to parse document names.
			["id"] = game.Id,
			["name"] = game.Name,
			["rulesText"] = game.RulesText,
			["coverImageUrl"] = game.CoverImageUrl,
			["scoringType"] = game.ScoringType.ToString(),
			["isPaid"] = game.IsPaid,
			["price"] = game.Price,
			["hostOrgId"] = game.HostOrgId,
			["createdByUserId"] = game.CreatedByUserId,
			["status"] = game.Status.ToString(),
			["createdAt"] = game.CreatedAt,
			["publishedAt"] = game.PublishedAt
		});

	public async Task PullPublishedGamesAsync()
	{
		var docs = await _firestore.WhereEqualAsync(GamesCollection, "status", nameof(GameStatus.Published));
		var cloudGames = docs.Select(ToGame).Where(g => g is not null).Select(g => g!).ToList();

		foreach (var game in cloudGames)
			await _local.SaveGameAsync(game);

		// Anything we still show as published but the cloud no longer does was unpublished or deleted elsewhere.
		var cloudIds = cloudGames.Select(g => g.Id).ToHashSet();
		foreach (var stale in (await _local.GetGamesAsync()).Where(g => !cloudIds.Contains(g.Id)))
		{
			stale.Status = GameStatus.Deleted;
			await _local.SaveGameAsync(stale);
		}
	}

	public async Task PullAllGamesAsync()
	{
		var docs = await _firestore.GetAllAsync(GamesCollection);
		foreach (var game in docs.Select(ToGame).Where(g => g is not null))
			await _local.SaveGameAsync(game!);
	}

	private static PubGame? ToGame(Dictionary<string, object?> f)
	{
		if (f.GetValueOrDefault("id") is not string id) return null;

		return new PubGame
		{
			Id = id,
			Name = f.GetValueOrDefault("name") as string ?? string.Empty,
			RulesText = f.GetValueOrDefault("rulesText") as string ?? string.Empty,
			CoverImageUrl = f.GetValueOrDefault("coverImageUrl") as string,
			ScoringType = Enum.TryParse<ScoringType>(f.GetValueOrDefault("scoringType") as string, out var st) ? st : ScoringType.Custom,
			IsPaid = f.GetValueOrDefault("isPaid") as bool? ?? false,
			Price = Convert.ToDecimal(f.GetValueOrDefault("price") ?? 0d),
			HostOrgId = f.GetValueOrDefault("hostOrgId") as string ?? string.Empty,
			CreatedByUserId = f.GetValueOrDefault("createdByUserId") as string ?? string.Empty,
			Status = Enum.TryParse<GameStatus>(f.GetValueOrDefault("status") as string, out var s) ? s : GameStatus.Draft,
			CreatedAt = f.GetValueOrDefault("createdAt") as DateTime? ?? DateTime.UtcNow,
			PublishedAt = f.GetValueOrDefault("publishedAt") as DateTime?
		};
	}

	public Task PullAccountDataAsync(string accountId)
	{
		// TODO: GET /accounts/{accountId}/players, /purchases, /sessions
		// and upsert results into ILocalDatabaseService.
		return Task.CompletedTask;
	}

	public Task PushPendingSessionsAsync()
	{
		// TODO: query local sessions where IsSynced == false, POST each to
		// the backend, then mark IsSynced = true locally on success.
		return Task.CompletedTask;
	}

	public Task<bool> VerifyAndRecordPurchaseAsync(string accountId, string gameId, string playBillingToken)
	{
		// TODO: POST { accountId, gameId, playBillingToken } to your backend.
		// Backend calls the Google Play Developer API to verify the token
		// server-side, then inserts the Purchase row and returns success.
		return Task.FromResult(false);
	}

	public Task SubmitApprovalRequestAsync(ApprovalRequest request)
	{
		// TODO: POST the request; backend should push a notification to every
		// head host on the org (the "Sanne wants to delete Kings cup" card).
		return _local.SaveApprovalRequestAsync(request);
	}

	public Task ResolveApprovalRequestAsync(string requestId, bool approved, string resolvedByUserId)
	{
		// TODO: PATCH the request status server-side, and if approved, apply
		// the underlying action (publish or delete the game) there too, so
		// it's consistent even if this device goes offline right after.
		return Task.CompletedTask;
	}
}
