using PubGames.Models;
using SQLite;

namespace PubGames.Services;

public interface ILocalDatabaseService
{
	Task InitAsync();

	// Players
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
	Task<List<GameCategory>> GetCategoriesForGameAsync(string gameId);

	// Sessions
	Task<GameSession?> GetSessionAsync(string sessionId);
	Task SaveSessionAsync(GameSession session);
	Task<List<SessionParticipant>> GetParticipantsAsync(string sessionId);
	Task SaveParticipantAsync(SessionParticipant participant);

	// Team / approvals (cached copies of the cloud, for offline use)
	Task<List<TeamMember>> GetTeamMembersAsync(string teamId);
	Task<TeamMember?> GetTeamMemberAsync(string email);
	Task SaveTeamMemberAsync(TeamMember member);
	Task DeleteTeamMemberAsync(string email);
	Task SaveApprovalRequestAsync(ApprovalRequest request);
	Task<List<ApprovalRequest>> GetPendingApprovalsAsync(string teamId);

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
		await _db.CreateTableAsync<GameCategoryLink>();
		await _db.CreateTableAsync<GameSession>();
		await _db.CreateTableAsync<SessionParticipant>();
		await _db.CreateTableAsync<Team>();
		await _db.CreateTableAsync<TeamMember>();
		await _db.CreateTableAsync<ApprovalRequest>();
		await _db.CreateTableAsync<Purchase>();

		return _db;
	}

	public async Task InitAsync() => await DbAsync();

	public async Task<List<Player>> GetPlayersAsync(string accountId)
	{
		var db = await DbAsync();
		return await db.Table<Player>().Where(p => p.OwnerAccountId == accountId).ToListAsync();
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

	public async Task<List<GameCategory>> GetCategoriesForGameAsync(string gameId)
	{
		var db = await DbAsync();
		var links = await db.Table<GameCategoryLink>().Where(l => l.GameId == gameId).ToListAsync();
		var categoryIds = links.Select(l => l.CategoryId).ToList();
		var all = await db.Table<GameCategory>().ToListAsync();
		return all.Where(c => categoryIds.Contains(c.Id)).ToList();
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

	public async Task SaveParticipantAsync(SessionParticipant participant)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(participant);
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

	public async Task SaveApprovalRequestAsync(ApprovalRequest request)
	{
		var db = await DbAsync();
		await db.InsertOrReplaceAsync(request);
	}

	public async Task<List<ApprovalRequest>> GetPendingApprovalsAsync(string teamId)
	{
		var db = await DbAsync();
		return await db.Table<ApprovalRequest>()
			.Where(r => r.TeamId == teamId && r.Status == ApprovalRequestStatus.Pending)
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
