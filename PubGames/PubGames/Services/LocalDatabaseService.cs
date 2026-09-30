using PubGames.Models;
using SQLite;

namespace PubGames.Services;

public interface ILocalDatabaseService
{
	Task InitAsync();

	// Players
	/// <summary>The account's saved players (not one-off players added to a single game), alphabetical.</summary>
	Task<List<Player>> GetPlayersAsync(string accountId);
	Task<List<Player>> GetPlayersByIdsAsync(IEnumerable<string> playerIds);
	Task SavePlayerAsync(Player player);

	// Games
	/// <summary>Published games only - what players can pick from.</summary>
	Task<List<PubGame>> GetGamesAsync();
	/// <summary>Every game that isn't deleted, drafts included - the admin library.</summary>
	Task<List<PubGame>> GetManageableGamesAsync();
	Task<PubGame?> GetGameAsync(string gameId);
	Task SaveGameAsync(PubGame game);

	// Categories
	/// <summary>All categories, alphabetical.</summary>
	Task<List<GameCategory>> GetCategoriesAsync();
	Task SaveCategoryAsync(GameCategory category);
	Task DeleteCategoryAsync(string categoryId);

	// Sessions
	Task<GameSession?> GetSessionAsync(string sessionId);
	Task SaveSessionAsync(GameSession session);
	Task<List<SessionParticipant>> GetParticipantsAsync(string sessionId);
	Task SaveParticipantAsync(SessionParticipant participant);
	/// <summary>Every subgame result of a session, in subgame order.</summary>
	Task<List<RoundResult>> GetRoundResultsAsync(string sessionId);
	/// <summary>Replaces the results of one subgame.</summary>
	Task SaveRoundResultsAsync(string sessionId, int round, IEnumerable<RoundResult> results);
	/// <summary>
	/// Hands one player's subgame results in a session to another, who took
	/// over their seat. Subgames the new player already has a result in (they
	/// played earlier on another seat) keep that result.
	/// </summary>
	Task TransferRoundResultsAsync(string sessionId, string fromPlayerId, string toPlayerId);

	// Team / review requests (cached copies of the cloud, for offline use)
	Task<List<TeamMember>> GetTeamMembersAsync(string teamId);
	Task<TeamMember?> GetTeamMemberAsync(string email);
	Task SaveTeamMemberAsync(TeamMember member);
	Task DeleteTeamMemberAsync(string email);
	Task<ReviewRequest?> GetReviewRequestAsync(string requestId);
	Task SaveReviewRequestAsync(ReviewRequest request);
	Task DeleteReviewRequestAsync(string requestId);
	/// <summary>Requests an admin still has to review, oldest first.</summary>
	Task<List<ReviewRequest>> GetRequestsWaitingForReviewAsync(string teamId);
	/// <summary>Everything this moderator submitted, newest first.</summary>
	Task<List<ReviewRequest>> GetRequestsSubmittedByAsync(string email);

	// Inbox
	/// <summary>Messages to any of these recipients (an email and/or InboxMessage.AllAdmins), newest first.</summary>
	Task<List<InboxMessage>> GetMessagesToAsync(IEnumerable<string> recipients);
	Task SaveMessageAsync(InboxMessage message);
	Task DeleteMessageAsync(string messageId);

	// Purchases
	Task<bool> IsGameOwnedAsync(string accountId, string gameId);
	Task SavePurchaseAsync(Purchase purchase);
}

/// <summary>
/// Everything is cached here first so the app works offline at the pub with
/// no signal. ICloudSyncService is responsible for pushing/pulling this data
/// against your backend whenever connectivity is available.
/// </summary>
public class LocalDatabaseService : ILocalDatabaseService
{
	private SQLiteAsyncConnection? _db;

	private async Task<SQLiteAsyncConnection> DbAsync()
	{
		if (_db is not null) return _db;

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "pubgames.db3");
		_db = new SQLiteAsyncConnection(dbPath);

		await _db.CreateTableAsync<Player>();
		await _db.CreateTableAsync<PubGame>();
		await _db.CreateTableAsync<GameCategory>();
		await _db.CreateTableAsync<InboxMessage>();
		await _db.CreateTableAsync<GameSession>();
		await _db.CreateTableAsync<SessionParticipant>();
		await _db.CreateTableAsync<RoundResult>();
		await _db.CreateTableAsync<Team>();
		await _db.CreateTableAsync<TeamMember>();
		await _db.CreateTableAsync<ReviewRequest>();
		await _db.CreateTableAsync<Purchase>();

		return _db;
	}

	public async Task InitAsync() => await DbAsync();

	public async Task<List<Player>> GetPlayersAsync(string accountId)
	{
		var db = await DbAsync();
		return await db.Table<Player>()
			.Where(p => p.OwnerAccountId == accountId && !p.IsTemporary)
			.OrderBy(p => p.Name)
			.ToListAsync();
	}

	public async Task<List<Player>> GetPlayersByIdsAsync(IEnumerable<string> playerIds)
	{
		var ids = playerIds.ToList();
		var db = await DbAsync();
		return await db.Table<Player>().Where(p => ids.Contains(p.Id)).ToListAsync();
	}

	public async Task SavePlayerAsync(Player player)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(player);
	}

	public async Task<List<PubGame>> GetGamesAsync()
	{
		var db = await DbAsync();
		return await db.Table<PubGame>().Where(g => g.Status == GameStatus.Published).ToListAsync();
	}

	public async Task<List<PubGame>> GetManageableGamesAsync()
	{
		var db = await DbAsync();
		return await db.Table<PubGame>().Where(g => g.Status != GameStatus.Deleted).OrderBy(g => g.Name).ToListAsync();
	}

	public async Task<PubGame?> GetGameAsync(string gameId)
	{
		var db = await DbAsync();
		return await db.Table<PubGame>().Where(g => g.Id == gameId).FirstOrDefaultAsync();
	}

	public async Task SaveGameAsync(PubGame game)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(game);
	}

	public async Task<List<GameCategory>> GetCategoriesAsync()
	{
		var db = await DbAsync();
		return await db.Table<GameCategory>().OrderBy(c => c.Name).ToListAsync();
	}

	public async Task SaveCategoryAsync(GameCategory category)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(category);
	}

	public async Task DeleteCategoryAsync(string categoryId)
	{
		var db = await DbAsync();
		await db.DeleteAsync<GameCategory>(categoryId);
	}

	public async Task<List<InboxMessage>> GetMessagesToAsync(IEnumerable<string> recipients)
	{
		var to = recipients.ToList();
		var db = await DbAsync();
		return await db.Table<InboxMessage>()
			.Where(m => to.Contains(m.ToEmail))
			.OrderByDescending(m => m.SentAt)
			.ToListAsync();
	}

	public async Task SaveMessageAsync(InboxMessage message)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(message);
	}

	public async Task DeleteMessageAsync(string messageId)
	{
		var db = await DbAsync();
		await db.DeleteAsync<InboxMessage>(messageId);
	}

	public async Task<GameSession?> GetSessionAsync(string sessionId)
	{
		var db = await DbAsync();
		return await db.Table<GameSession>().Where(s => s.Id == sessionId).FirstOrDefaultAsync();
	}

	public async Task SaveSessionAsync(GameSession session)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(session);
	}

	public async Task<List<SessionParticipant>> GetParticipantsAsync(string sessionId)
	{
		var db = await DbAsync();
		return await db.Table<SessionParticipant>()
			.Where(p => p.SessionId == sessionId)
			.OrderBy(p => p.Position)
			.ToListAsync();
	}

	/// <summary>
	/// Not InsertOrReplace: SessionParticipant has an auto-increment id, and
	/// InsertOrReplace writes the unset id (0) as-is, so every new player would
	/// overwrite the previous one. Insert assigns a fresh id; later saves update it.
	/// </summary>
	public async Task SaveParticipantAsync(SessionParticipant participant)
	{
		var db = await DbAsync();
		if (participant.Id == 0)
			await db.InsertAsync(participant);
		else
			await db.UpdateAsync(participant);
	}

	public async Task<List<RoundResult>> GetRoundResultsAsync(string sessionId)
	{
		var db = await DbAsync();
		return await db.Table<RoundResult>()
			.Where(r => r.SessionId == sessionId)
			.OrderBy(r => r.Round)
			.ToListAsync();
	}

	public async Task SaveRoundResultsAsync(string sessionId, int round, IEnumerable<RoundResult> results)
	{
		var db = await DbAsync();
		var list = results.ToList();
		await db.RunInTransactionAsync(tx =>
		{
			tx.Execute($"DELETE FROM {nameof(RoundResult)} WHERE {nameof(RoundResult.SessionId)} = ? AND {nameof(RoundResult.Round)} = ?", sessionId, round);
			foreach (var r in list)
				tx.Insert(r);
		});
	}

	public async Task TransferRoundResultsAsync(string sessionId, string fromPlayerId, string toPlayerId)
	{
		var db = await DbAsync();
		await db.ExecuteAsync(
			$"UPDATE {nameof(RoundResult)} SET {nameof(RoundResult.PlayerId)} = ? " +
			$"WHERE {nameof(RoundResult.SessionId)} = ? AND {nameof(RoundResult.PlayerId)} = ? " +
			$"AND {nameof(RoundResult.Round)} NOT IN (SELECT {nameof(RoundResult.Round)} FROM {nameof(RoundResult)} " +
			$"WHERE {nameof(RoundResult.SessionId)} = ? AND {nameof(RoundResult.PlayerId)} = ?)",
			toPlayerId, sessionId, fromPlayerId, sessionId, toPlayerId);
	}

	public async Task<List<TeamMember>> GetTeamMembersAsync(string teamId)
	{
		var db = await DbAsync();
		return await db.Table<TeamMember>().Where(m => m.TeamId == teamId).ToListAsync();
	}

	public async Task<TeamMember?> GetTeamMemberAsync(string email)
	{
		var db = await DbAsync();
		return await db.FindAsync<TeamMember>(email);
	}

	public async Task SaveTeamMemberAsync(TeamMember member)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(member);
	}

	public async Task DeleteTeamMemberAsync(string email)
	{
		var db = await DbAsync();
		await db.DeleteAsync<TeamMember>(email);
	}

	public async Task<ReviewRequest?> GetReviewRequestAsync(string requestId)
	{
		var db = await DbAsync();
		return await db.FindAsync<ReviewRequest>(requestId);
	}

	public async Task SaveReviewRequestAsync(ReviewRequest request)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(request);
	}

	public async Task DeleteReviewRequestAsync(string requestId)
	{
		var db = await DbAsync();
		await db.DeleteAsync<ReviewRequest>(requestId);
	}

	public async Task<List<ReviewRequest>> GetRequestsWaitingForReviewAsync(string teamId)
	{
		var db = await DbAsync();
		return await db.Table<ReviewRequest>()
			.Where(r => r.TeamId == teamId && r.Status == ReviewStatus.WaitingForReview)
			.OrderBy(r => r.SubmittedAt)
			.ToListAsync();
	}

	public async Task<List<ReviewRequest>> GetRequestsSubmittedByAsync(string email)
	{
		var db = await DbAsync();
		return await db.Table<ReviewRequest>()
			.Where(r => r.SubmittedByEmail == email)
			.OrderByDescending(r => r.SubmittedAt)
			.ToListAsync();
	}

	public async Task<bool> IsGameOwnedAsync(string accountId, string gameId)
	{
		var db = await DbAsync();
		var purchase = await db.Table<Purchase>()
			.Where(p => p.AccountId == accountId && p.GameId == gameId)
			.FirstOrDefaultAsync();
		return purchase is not null;
	}

	public async Task SavePurchaseAsync(Purchase purchase)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(purchase);
	}
}
