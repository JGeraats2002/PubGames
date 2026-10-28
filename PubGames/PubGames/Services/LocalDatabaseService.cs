using PubGames.Models;
using SQLite;

namespace PubGames.Services;

/// <summary>
/// Everything the app keeps on the phone (SQLite): players, games, categories, game nights and their results,
/// the team, review requests and messages.
/// </summary>
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
	/// <summary>All main categories, oldest first.</summary>
	Task<List<CategoryGroup>> GetCategoryGroupsAsync();
	Task SaveCategoryGroupAsync(CategoryGroup group);
	Task DeleteCategoryGroupAsync(string groupId);

	// Sessions
	Task<GameSession?> GetSessionAsync(string sessionId);
	Task SaveSessionAsync(GameSession session);
	/// <summary>Finished games whose results haven't been published to the players' stats yet.</summary>
	Task<List<GameSession>> GetUnpublishedFinishedSessionsAsync(string accountId);
	/// <summary>Every game played on this phone with this account, finished or not.</summary>
	Task<List<GameSession>> GetSessionsAsync(string accountId);
	/// <summary>Deletes games with their players' scores and subgame results.</summary>
	Task DeleteSessionsAsync(IEnumerable<string> sessionIds);
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

	/// <summary>Removes everything of this account on this phone: its saved players and its games.</summary>
	Task DeleteAccountDataAsync(string accountId);
}

/// <summary>
/// Everything is cached here first so the app works offline at the pub with
/// no signal. ICloudSyncService is responsible for pushing/pulling this data
/// against your backend whenever connectivity is available.
/// </summary>
public class LocalDatabaseService : ILocalDatabaseService
{
	private SQLiteAsyncConnection? _db;

	/// <summary>The database connection, opened (and its tables made) the first time it's needed.</summary>
	private async Task<SQLiteAsyncConnection> DbAsync()
	{
		if (_db is not null) return _db;

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "pubgames.db3");
		_db = new SQLiteAsyncConnection(dbPath);

		await _db.CreateTableAsync<Player>();
		await _db.CreateTableAsync<PubGame>();
		await _db.CreateTableAsync<GameCategory>();
		await _db.CreateTableAsync<CategoryGroup>();
		await _db.CreateTableAsync<InboxMessage>();
		await _db.CreateTableAsync<GameSession>();
		await _db.CreateTableAsync<SessionParticipant>();
		await _db.CreateTableAsync<RoundResult>();
		await _db.CreateTableAsync<Team>();
		await _db.CreateTableAsync<TeamMember>();
		await _db.CreateTableAsync<ReviewRequest>();

		return _db;
	}

	/// <summary>Opens the database early, so the first page doesn't wait for it.</summary>
	public async Task InitAsync() => await DbAsync();

	/// <inheritdoc />
	public async Task<List<Player>> GetPlayersAsync(string accountId)
	{
		var db = await DbAsync();
		return await db.Table<Player>()
			.Where(p => p.OwnerAccountId == accountId && !p.IsTemporary)
			.OrderBy(p => p.Name)
			.ToListAsync();
	}

	/// <summary>
	/// The players with these ids, also the ones taken off the saved list (earlier games still show their
	/// names).
	/// </summary>
	public async Task<List<Player>> GetPlayersByIdsAsync(IEnumerable<string> playerIds)
	{
		var ids = playerIds.ToList();
		var db = await DbAsync();
		return await db.Table<Player>().Where(p => ids.Contains(p.Id)).ToListAsync();
	}

	/// <summary>Saves a player (new or changed).</summary>
	public async Task SavePlayerAsync(Player player)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(player);
	}

	/// <inheritdoc />
	public async Task<List<PubGame>> GetGamesAsync()
	{
		var db = await DbAsync();
		return await db.Table<PubGame>().Where(g => g.Status == GameStatus.Published).ToListAsync();
	}

	/// <inheritdoc />
	public async Task<List<PubGame>> GetManageableGamesAsync()
	{
		var db = await DbAsync();
		return await db.Table<PubGame>().Where(g => g.Status != GameStatus.Deleted).OrderBy(g => g.Name).ToListAsync();
	}

	/// <summary>One game by its id, or null.</summary>
	public async Task<PubGame?> GetGameAsync(string gameId)
	{
		var db = await DbAsync();
		return await db.Table<PubGame>().Where(g => g.Id == gameId).FirstOrDefaultAsync();
	}

	/// <summary>Saves a game (new or changed).</summary>
	public async Task SaveGameAsync(PubGame game)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(game);
	}

	/// <inheritdoc />
	public async Task<List<GameCategory>> GetCategoriesAsync()
	{
		var db = await DbAsync();
		return await db.Table<GameCategory>().OrderBy(c => c.Name).ToListAsync();
	}

	/// <summary>Saves a category (new or changed).</summary>
	public async Task SaveCategoryAsync(GameCategory category)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(category);
	}

	/// <summary>Removes a category from the phone.</summary>
	public async Task DeleteCategoryAsync(string categoryId)
	{
		var db = await DbAsync();
		await db.DeleteAsync<GameCategory>(categoryId);
	}

	/// <inheritdoc />
	public async Task<List<CategoryGroup>> GetCategoryGroupsAsync()
	{
		var db = await DbAsync();
		return await db.Table<CategoryGroup>().OrderBy(g => g.CreatedAt).ToListAsync();
	}

	/// <summary>Saves a main category (new or changed).</summary>
	public async Task SaveCategoryGroupAsync(CategoryGroup group)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(group);
	}

	/// <summary>Removes a main category from the phone.</summary>
	public async Task DeleteCategoryGroupAsync(string groupId)
	{
		var db = await DbAsync();
		await db.DeleteAsync<CategoryGroup>(groupId);
	}

	/// <inheritdoc />
	public async Task<List<InboxMessage>> GetMessagesToAsync(IEnumerable<string> recipients)
	{
		var to = recipients.ToList();
		var db = await DbAsync();
		return await db.Table<InboxMessage>()
			.Where(m => to.Contains(m.ToEmail))
			.OrderByDescending(m => m.SentAt)
			.ToListAsync();
	}

	/// <summary>Saves an Inbox message (new or changed).</summary>
	public async Task SaveMessageAsync(InboxMessage message)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(message);
	}

	/// <summary>Removes an Inbox message from the phone.</summary>
	public async Task DeleteMessageAsync(string messageId)
	{
		var db = await DbAsync();
		await db.DeleteAsync<InboxMessage>(messageId);
	}

	/// <summary>One game night by its id, or null.</summary>
	public async Task<GameSession?> GetSessionAsync(string sessionId)
	{
		var db = await DbAsync();
		return await db.Table<GameSession>().Where(s => s.Id == sessionId).FirstOrDefaultAsync();
	}

	/// <summary>Saves a game night (new or changed).</summary>
	public async Task SaveSessionAsync(GameSession session)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(session);
	}

	/// <inheritdoc />
	public async Task<List<GameSession>> GetUnpublishedFinishedSessionsAsync(string accountId)
	{
		var db = await DbAsync();
		return await db.Table<GameSession>()
			.Where(s => s.AccountId == accountId && s.EndedAt != null && !s.IsSynced)
			.ToListAsync();
	}

	/// <inheritdoc />
	public async Task<List<GameSession>> GetSessionsAsync(string accountId)
	{
		var db = await DbAsync();
		return await db.Table<GameSession>().Where(s => s.AccountId == accountId).ToListAsync();
	}

	/// <inheritdoc />
	public async Task DeleteSessionsAsync(IEnumerable<string> sessionIds)
	{
		var ids = sessionIds.ToList();
		if (ids.Count == 0) return;
		var db = await DbAsync();
		await db.RunInTransactionAsync(tx =>
		{
			foreach (var id in ids)
			{
				tx.Execute(string.Format(System.Globalization.CultureInfo.InvariantCulture, "DELETE FROM {0} WHERE {1} = ?", nameof(RoundResult), nameof(RoundResult.SessionId)), id);
				tx.Execute(string.Format(System.Globalization.CultureInfo.InvariantCulture, "DELETE FROM {0} WHERE {1} = ?", nameof(SessionParticipant), nameof(SessionParticipant.SessionId)), id);
				tx.Execute(string.Format(System.Globalization.CultureInfo.InvariantCulture, "DELETE FROM {0} WHERE {1} = ?", nameof(GameSession), nameof(GameSession.Id)), id);
			}
		});
	}

	/// <summary>Everyone at a game night, in seat order.</summary>
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

	/// <inheritdoc />
	public async Task<List<RoundResult>> GetRoundResultsAsync(string sessionId)
	{
		var db = await DbAsync();
		return await db.Table<RoundResult>()
			.Where(r => r.SessionId == sessionId)
			.OrderBy(r => r.Round)
			.ToListAsync();
	}

	/// <inheritdoc />
	public async Task SaveRoundResultsAsync(string sessionId, int round, IEnumerable<RoundResult> results)
	{
		var db = await DbAsync();
		var list = results.ToList();
		await db.RunInTransactionAsync(tx =>
		{
			tx.Execute(string.Format(System.Globalization.CultureInfo.InvariantCulture, "DELETE FROM {0} WHERE {1} = ? AND {2} = ?", nameof(RoundResult), nameof(RoundResult.SessionId), nameof(RoundResult.Round)), sessionId, round);
			foreach (var roundResult in list)
				tx.Insert(roundResult);
		});
	}

	/// <inheritdoc />
	public async Task TransferRoundResultsAsync(string sessionId, string fromPlayerId, string toPlayerId)
	{
		var db = await DbAsync();
		await db.ExecuteAsync(
			string.Format(System.Globalization.CultureInfo.InvariantCulture, "UPDATE {0} SET {1} = ? ", nameof(RoundResult), nameof(RoundResult.PlayerId)) +
			string.Format(System.Globalization.CultureInfo.InvariantCulture, "WHERE {0} = ? AND {1} = ? ", nameof(RoundResult.SessionId), nameof(RoundResult.PlayerId)) +
			string.Format(System.Globalization.CultureInfo.InvariantCulture, "AND {0} NOT IN (SELECT {1} FROM {2} ", nameof(RoundResult.Round), nameof(RoundResult.Round), nameof(RoundResult)) +
			string.Format(System.Globalization.CultureInfo.InvariantCulture, "WHERE {0} = ? AND {1} = ?)", nameof(RoundResult.SessionId), nameof(RoundResult.PlayerId)),
			toPlayerId, sessionId, fromPlayerId, sessionId, toPlayerId);
	}

	/// <summary>The admins and moderators of a team, as last seen.</summary>
	public async Task<List<TeamMember>> GetTeamMembersAsync(string teamId)
	{
		var db = await DbAsync();
		return await db.Table<TeamMember>().Where(m => m.TeamId == teamId).ToListAsync();
	}

	/// <summary>One team member by email, or null.</summary>
	public async Task<TeamMember?> GetTeamMemberAsync(string email)
	{
		var db = await DbAsync();
		return await db.FindAsync<TeamMember>(email);
	}

	/// <summary>Saves a team member (new or changed).</summary>
	public async Task SaveTeamMemberAsync(TeamMember member)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(member);
	}

	/// <summary>Removes a team member from the phone.</summary>
	public async Task DeleteTeamMemberAsync(string email)
	{
		var db = await DbAsync();
		await db.DeleteAsync<TeamMember>(email);
	}

	/// <summary>One review request by its id, or null.</summary>
	public async Task<ReviewRequest?> GetReviewRequestAsync(string requestId)
	{
		var db = await DbAsync();
		return await db.FindAsync<ReviewRequest>(requestId);
	}

	/// <summary>Saves a review request (new or changed).</summary>
	public async Task SaveReviewRequestAsync(ReviewRequest request)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(request);
	}

	/// <summary>Removes a review request from the phone.</summary>
	public async Task DeleteReviewRequestAsync(string requestId)
	{
		var db = await DbAsync();
		await db.DeleteAsync<ReviewRequest>(requestId);
	}

	/// <inheritdoc />
	public async Task<List<ReviewRequest>> GetRequestsWaitingForReviewAsync(string teamId)
	{
		var db = await DbAsync();
		return await db.Table<ReviewRequest>()
			.Where(r => r.TeamId == teamId && r.Status == ReviewStatus.WaitingForReview)
			.OrderBy(r => r.SubmittedAt)
			.ToListAsync();
	}

	/// <inheritdoc />
	public async Task<List<ReviewRequest>> GetRequestsSubmittedByAsync(string email)
	{
		var db = await DbAsync();
		return await db.Table<ReviewRequest>()
			.Where(r => r.SubmittedByEmail == email)
			.OrderByDescending(r => r.SubmittedAt)
			.ToListAsync();
	}

	/// <inheritdoc />
	public async Task DeleteAccountDataAsync(string accountId)
	{
		var sessions = await GetSessionsAsync(accountId);
		await DeleteSessionsAsync(sessions.Select(session => session.Id));
		var db = await DbAsync();
		await db.ExecuteAsync(string.Format(System.Globalization.CultureInfo.InvariantCulture, "DELETE FROM {0} WHERE {1} = ?", nameof(Player), nameof(Player.OwnerAccountId)), accountId);
	}
}
