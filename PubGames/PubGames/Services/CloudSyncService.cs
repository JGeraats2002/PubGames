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

	/// <summary>Admins and moderators only: downloads every game including drafts and deleted ones, for the admin library.</summary>
	Task PullAllGamesAsync();

	/// <summary>One game straight from the cloud (null if it doesn't exist), also saved locally.</summary>
	Task<PubGame?> PullGameAsync(string gameId);

	/// <summary>Files the request in the cloud so every admin sees it. Throws when offline.</summary>
	Task SubmitApprovalRequestAsync(ApprovalRequest request);

	/// <summary>Admins only: the team's open requests, refreshed into local storage. Throws when offline.</summary>
	Task<List<ApprovalRequest>> PullPendingApprovalsAsync(string teamId);

	/// <summary>Writes the request's new status (set by the caller) to the cloud, then locally.</summary>
	Task ResolveApprovalRequestAsync(ApprovalRequest request);
}

/// <summary>
/// Firestore-backed sync for games and approval requests. Account data,
/// sessions and purchases are still stubs (see the TODOs) until those get
/// their own collections and rules.
/// </summary>
public class CloudSyncService : ICloudSyncService
{
	private const string GamesCollection = "games";
	private const string ApprovalsCollection = "approvals";

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
			["teamId"] = game.TeamId,
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

	public async Task<PubGame?> PullGameAsync(string gameId)
	{
		var doc = await _firestore.GetAsync(GamesCollection, gameId);
		var game = doc is null ? null : ToGame(doc);
		if (game is not null)
			await _local.SaveGameAsync(game);
		return game;
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
			// Games saved before the host -> admin rename have "hostOrgId" instead.
			TeamId = f.GetValueOrDefault("teamId") as string ?? f.GetValueOrDefault("hostOrgId") as string ?? string.Empty,
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

	public async Task SubmitApprovalRequestAsync(ApprovalRequest request)
	{
		// TODO: push a notification to every admin on the team (the "Sanne wants
		// to delete Kings cup" card); for now they see it when opening the Team tab.
		await SaveApprovalAsync(request);
		await _local.SaveApprovalRequestAsync(request);
	}

	public async Task<List<ApprovalRequest>> PullPendingApprovalsAsync(string teamId)
	{
		var docs = await _firestore.WhereEqualAsync(ApprovalsCollection, "status", nameof(ApprovalRequestStatus.Pending));
		var pending = docs.Select(ToApproval).Where(r => r is not null && r.TeamId == teamId).Select(r => r!).ToList();

		// Requests another admin already resolved drop out of the local cache.
		var pendingIds = pending.Select(r => r.Id).ToHashSet();
		foreach (var stale in (await _local.GetPendingApprovalsAsync(teamId)).Where(r => !pendingIds.Contains(r.Id)))
		{
			stale.Status = ApprovalRequestStatus.Denied;
			await _local.SaveApprovalRequestAsync(stale);
		}
		foreach (var request in pending)
			await _local.SaveApprovalRequestAsync(request);

		return pending;
	}

	public async Task ResolveApprovalRequestAsync(ApprovalRequest request)
	{
		await SaveApprovalAsync(request);
		await _local.SaveApprovalRequestAsync(request);
	}

	private Task SaveApprovalAsync(ApprovalRequest r) =>
		_firestore.SetAsync(ApprovalsCollection, r.Id, new Dictionary<string, object?>
		{
			["id"] = r.Id,
			["gameId"] = r.GameId,
			["teamId"] = r.TeamId,
			["gameName"] = r.GameName,
			["type"] = r.Type.ToString(),
			["requestedByUserId"] = r.RequestedByUserId,
			["requestedByEmail"] = r.RequestedByEmail,
			["requestedByName"] = r.RequestedByName,
			["status"] = r.Status.ToString(),
			["resolvedByUserId"] = r.ResolvedByUserId,
			["requestedAt"] = r.RequestedAt,
			["resolvedAt"] = r.ResolvedAt
		});

	private static ApprovalRequest? ToApproval(Dictionary<string, object?> f)
	{
		if (f.GetValueOrDefault("id") is not string id) return null;

		return new ApprovalRequest
		{
			Id = id,
			GameId = f.GetValueOrDefault("gameId") as string ?? string.Empty,
			TeamId = f.GetValueOrDefault("teamId") as string ?? string.Empty,
			GameName = f.GetValueOrDefault("gameName") as string ?? string.Empty,
			Type = Enum.TryParse<ApprovalRequestType>(f.GetValueOrDefault("type") as string, out var t) ? t : ApprovalRequestType.PublishGame,
			RequestedByUserId = f.GetValueOrDefault("requestedByUserId") as string ?? string.Empty,
			RequestedByEmail = f.GetValueOrDefault("requestedByEmail") as string ?? string.Empty,
			RequestedByName = f.GetValueOrDefault("requestedByName") as string ?? string.Empty,
			Status = Enum.TryParse<ApprovalRequestStatus>(f.GetValueOrDefault("status") as string, out var s) ? s : ApprovalRequestStatus.Pending,
			ResolvedByUserId = f.GetValueOrDefault("resolvedByUserId") as string,
			RequestedAt = f.GetValueOrDefault("requestedAt") as DateTime? ?? DateTime.UtcNow,
			ResolvedAt = f.GetValueOrDefault("resolvedAt") as DateTime?
		};
	}
}
