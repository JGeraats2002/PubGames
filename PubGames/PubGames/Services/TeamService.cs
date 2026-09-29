using PubGames.Models;

namespace PubGames.Services;

/// <summary>
/// Who is on the team and with which rights. The cloud "team" collection
/// (one document per member, id = lower-case email) is the source of truth
/// and is what firestore.rules check; this phone keeps a copy for offline use.
/// Built-in admins from AuthConfig.AdminEmails aren't stored there.
/// </summary>
public interface ITeamService
{
	/// <summary>The signed-in user's membership as of the last refresh; null for regular players.</summary>
	TeamMember? Me { get; }

	/// <summary>
	/// Re-reads the signed-in user's membership from the cloud, falling back to
	/// this phone's copy when offline. Call after sign-in, before building the shell.
	/// </summary>
	Task<TeamMember?> RefreshMyMembershipAsync();

	/// <summary>Built-in admins plus everyone added on the Team page, from this phone's copy.</summary>
	Task<List<TeamMember>> GetCachedMembersAsync();

	/// <summary>Admins only: same as GetCachedMembersAsync but refreshed from the cloud first. Throws when offline.</summary>
	Task<List<TeamMember>> PullMembersAsync();

	/// <summary>Creates or updates the member (role) in the cloud, then locally.</summary>
	Task SaveMemberAsync(TeamMember member);

	/// <summary>Removes the member from the cloud, then locally. Their access ends next time their app refreshes.</summary>
	Task RemoveMemberAsync(TeamMember member);
}

public class TeamService : ITeamService
{
	private const string Collection = "team";

	// TODO: replace with the team the user picked once multiple teams exist.
	private const string CurrentTeamId = AuthService.DefaultTeamId;

	private readonly IAuthService _auth;
	private readonly ILocalDatabaseService _local;
	private readonly FirestoreClient _firestore;

	public TeamService(IAuthService auth, ILocalDatabaseService local, FirestoreClient firestore)
	{
		_auth = auth;
		_local = local;
		_firestore = firestore;
	}

	public TeamMember? Me { get; private set; }

	public async Task<TeamMember?> RefreshMyMembershipAsync()
	{
		var user = _auth.CurrentUser;
		if (user is null || string.IsNullOrEmpty(user.Email))
			return Me = null;

		if (user.IsAdmin)
			return Me = BuiltInAdmin(user.Email, user.DisplayName);

		var email = TeamMember.NormalizeEmail(user.Email);
		try
		{
			var doc = await _firestore.GetAsync(Collection, email);
			if (doc is null)
			{
				// Not on the team (any more): forget the cached copy so rights end offline too.
				await _local.DeleteTeamMemberAsync(email);
				return Me = null;
			}
			Me = ToMember(doc);
			await _local.SaveTeamMemberAsync(Me);
		}
		catch (Exception ex) when (ex is HttpRequestException or CloudException or InvalidOperationException)
		{
			// Offline at the pub (or rules not deployed yet): trust the last known membership.
			Me = await _local.GetTeamMemberAsync(email);
		}
		return Me;
	}

	public async Task<List<TeamMember>> GetCachedMembersAsync() =>
		WithBuiltInAdmins(await _local.GetTeamMembersAsync(CurrentTeamId));

	public async Task<List<TeamMember>> PullMembersAsync()
	{
		var docs = await _firestore.GetAllAsync(Collection);
		var members = docs.Select(ToMember).Where(m => m.TeamId == CurrentTeamId).ToList();

		// Removed by another admin since the last refresh.
		var emails = members.Select(m => m.Email).ToHashSet();
		foreach (var stale in (await _local.GetTeamMembersAsync(CurrentTeamId)).Where(m => !emails.Contains(m.Email)))
			await _local.DeleteTeamMemberAsync(stale.Email);
		foreach (var member in members)
			await _local.SaveTeamMemberAsync(member);

		return WithBuiltInAdmins(members);
	}

	public async Task SaveMemberAsync(TeamMember member)
	{
		member.Email = TeamMember.NormalizeEmail(member.Email);
		member.TeamId = CurrentTeamId;
		await _firestore.SetAsync(Collection, member.Email, new Dictionary<string, object?>
		{
			["email"] = member.Email,
			["teamId"] = member.TeamId,
			["displayName"] = member.DisplayName,
			["role"] = member.Role.ToString(),
			["addedAt"] = member.AddedAt
		});
		await _local.SaveTeamMemberAsync(member);
	}

	public async Task RemoveMemberAsync(TeamMember member)
	{
		if (member.IsBuiltInAdmin)
			throw new InvalidOperationException("Built-in admins can only be removed in AuthConfig.AdminEmails.");

		await _firestore.DeleteAsync(Collection, member.Email);
		await _local.DeleteTeamMemberAsync(member.Email);
	}

	private List<TeamMember> WithBuiltInAdmins(IEnumerable<TeamMember> members)
	{
		var builtIn = AuthConfig.AdminEmails
			.Select(email => BuiltInAdmin(email,
				_auth.CurrentUser is { } me && me.Email.Equals(email, StringComparison.OrdinalIgnoreCase) ? me.DisplayName : string.Empty))
			.ToList();
		var builtInEmails = builtIn.Select(m => m.Email).ToHashSet();

		return builtIn
			.Concat(members.Where(m => !builtInEmails.Contains(m.Email))
				.OrderBy(m => m.Role)
				.ThenBy(m => m.Label, StringComparer.OrdinalIgnoreCase))
			.ToList();
	}

	private static TeamMember BuiltInAdmin(string email, string displayName) => new()
	{
		Email = TeamMember.NormalizeEmail(email),
		TeamId = CurrentTeamId,
		DisplayName = displayName,
		Role = TeamRole.Admin,
		IsBuiltInAdmin = true
	};

	private static TeamMember ToMember(Dictionary<string, object?> f) => new()
	{
		Email = TeamMember.NormalizeEmail(f.GetValueOrDefault("email") as string ?? string.Empty),
		TeamId = f.GetValueOrDefault("teamId") as string ?? CurrentTeamId,
		DisplayName = f.GetValueOrDefault("displayName") as string ?? string.Empty,
		Role = Enum.TryParse<TeamRole>(f.GetValueOrDefault("role") as string, out var role) ? role : TeamRole.Moderator,
		AddedAt = f.GetValueOrDefault("addedAt") as DateTime? ?? DateTime.UtcNow
	};
}
