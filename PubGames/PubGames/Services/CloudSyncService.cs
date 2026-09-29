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

	/// <summary>
	/// Creates or replaces the review request in the cloud so every admin sees
	/// it, then saves it locally. Also used by admins to record their decision.
	/// Throws when offline.
	/// </summary>
	Task SaveReviewRequestAsync(ReviewRequest request);

	/// <summary>A moderator takes back a request that is still waiting for review.</summary>
	Task WithdrawReviewRequestAsync(ReviewRequest request);

	/// <summary>Admins only: the team's requests waiting for review, refreshed into local storage. Throws when offline.</summary>
	Task<List<ReviewRequest>> PullRequestsWaitingForReviewAsync(string teamId);

	/// <summary>Everything this moderator submitted, with the admins' decisions, refreshed into local storage.</summary>
	Task<List<ReviewRequest>> PullRequestsSubmittedByAsync(string email);
}

/// <summary>
/// Firestore-backed sync for games and review requests. Account data,
/// sessions and purchases are still stubs (see the TODOs) until those get
/// their own collections and rules.
/// </summary>
public class CloudSyncService : ICloudSyncService
{
	private const string GamesCollection = "games";
	private const string ReviewRequestsCollection = "reviewRequests";

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
			["categoryIds"] = game.CategoryIds,
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
			CategoryIds = FirestoreClient.AsStringList(f.GetValueOrDefault("categoryIds")),
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

	public async Task SaveReviewRequestAsync(ReviewRequest r)
	{
		// TODO: push a notification to every admin on the team when a request
		// is submitted; for now they see it when opening the Requests page.
		await _firestore.SetAsync(ReviewRequestsCollection, r.Id, new Dictionary<string, object?>
		{
			["id"] = r.Id,
			["gameId"] = r.GameId,
			["categoryId"] = r.CategoryId,
			["teamId"] = r.TeamId,
			["type"] = r.Type.ToString(),
			["subjectName"] = r.SubjectName,
			["submittedByUserId"] = r.SubmittedByUserId,
			["submittedByEmail"] = r.SubmittedByEmail,
			["submittedByName"] = r.SubmittedByName,
			["submittedAt"] = r.SubmittedAt,
			["status"] = r.Status.ToString(),
			["reviewedByUserId"] = r.ReviewedByUserId,
			["reviewedAt"] = r.ReviewedAt,
			["reviewNote"] = r.ReviewNote,
			["proposedName"] = r.ProposedName,
			["proposedRulesText"] = r.ProposedRulesText,
			["proposedCoverImageUrl"] = r.ProposedCoverImageUrl,
			["proposedScoringType"] = r.ProposedScoringType.ToString(),
			["proposedIsPaid"] = r.ProposedIsPaid,
			["proposedPrice"] = r.ProposedPrice,
			["proposedCategoryIds"] = r.ProposedCategoryIdsText.Split(',', StringSplitOptions.RemoveEmptyEntries)
		});
		await _local.SaveReviewRequestAsync(r);
	}

	public async Task WithdrawReviewRequestAsync(ReviewRequest request)
	{
		await _firestore.DeleteAsync(ReviewRequestsCollection, request.Id);
		await _local.DeleteReviewRequestAsync(request.Id);
	}

	public async Task<List<ReviewRequest>> PullRequestsWaitingForReviewAsync(string teamId)
	{
		var docs = await _firestore.WhereEqualAsync(ReviewRequestsCollection, "status", nameof(ReviewStatus.WaitingForReview));
		var waiting = docs.Select(ToReviewRequest)
			.Where(r => r is not null && r.TeamId == teamId)
			.Select(r => r!)
			.OrderBy(r => r.SubmittedAt)
			.ToList();

		// Reviewed by another admin or withdrawn since the last refresh.
		var waitingIds = waiting.Select(r => r.Id).ToHashSet();
		foreach (var stale in (await _local.GetRequestsWaitingForReviewAsync(teamId)).Where(r => !waitingIds.Contains(r.Id)))
			await _local.DeleteReviewRequestAsync(stale.Id);
		foreach (var request in waiting)
			await _local.SaveReviewRequestAsync(request);

		return waiting;
	}

	public async Task<List<ReviewRequest>> PullRequestsSubmittedByAsync(string email)
	{
		var docs = await _firestore.WhereEqualAsync(ReviewRequestsCollection, "submittedByEmail", email);
		var mine = docs.Select(ToReviewRequest)
			.Where(r => r is not null)
			.Select(r => r!)
			.OrderByDescending(r => r.SubmittedAt)
			.ToList();

		var ids = mine.Select(r => r.Id).ToHashSet();
		foreach (var stale in (await _local.GetRequestsSubmittedByAsync(email)).Where(r => !ids.Contains(r.Id)))
			await _local.DeleteReviewRequestAsync(stale.Id);
		foreach (var request in mine)
			await _local.SaveReviewRequestAsync(request);

		return mine;
	}

	private static ReviewRequest? ToReviewRequest(Dictionary<string, object?> f)
	{
		if (f.GetValueOrDefault("id") is not string id) return null;

		return new ReviewRequest
		{
			Id = id,
			GameId = f.GetValueOrDefault("gameId") as string ?? string.Empty,
			CategoryId = f.GetValueOrDefault("categoryId") as string ?? string.Empty,
			TeamId = f.GetValueOrDefault("teamId") as string ?? string.Empty,
			Type = Enum.TryParse<ReviewRequestType>(f.GetValueOrDefault("type") as string, out var t) ? t : ReviewRequestType.GameChanges,
			// Requests from before categories existed have "gameName".
			SubjectName = f.GetValueOrDefault("subjectName") as string ?? f.GetValueOrDefault("gameName") as string ?? string.Empty,
			SubmittedByUserId = f.GetValueOrDefault("submittedByUserId") as string ?? string.Empty,
			SubmittedByEmail = f.GetValueOrDefault("submittedByEmail") as string ?? string.Empty,
			SubmittedByName = f.GetValueOrDefault("submittedByName") as string ?? string.Empty,
			SubmittedAt = f.GetValueOrDefault("submittedAt") as DateTime? ?? DateTime.UtcNow,
			Status = Enum.TryParse<ReviewStatus>(f.GetValueOrDefault("status") as string, out var s) ? s : ReviewStatus.WaitingForReview,
			ReviewedByUserId = f.GetValueOrDefault("reviewedByUserId") as string,
			ReviewedAt = f.GetValueOrDefault("reviewedAt") as DateTime?,
			ReviewNote = f.GetValueOrDefault("reviewNote") as string ?? string.Empty,
			ProposedName = f.GetValueOrDefault("proposedName") as string ?? string.Empty,
			ProposedRulesText = f.GetValueOrDefault("proposedRulesText") as string ?? string.Empty,
			ProposedCoverImageUrl = f.GetValueOrDefault("proposedCoverImageUrl") as string,
			ProposedScoringType = Enum.TryParse<ScoringType>(f.GetValueOrDefault("proposedScoringType") as string, out var st) ? st : ScoringType.Custom,
			ProposedIsPaid = f.GetValueOrDefault("proposedIsPaid") as bool? ?? false,
			ProposedPrice = Convert.ToDecimal(f.GetValueOrDefault("proposedPrice") ?? 0d),
			ProposedCategoryIdsText = string.Join(',', FirestoreClient.AsStringList(f.GetValueOrDefault("proposedCategoryIds")))
		};
	}
}
