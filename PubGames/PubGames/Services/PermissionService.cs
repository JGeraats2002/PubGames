using PubGames.Models;

namespace PubGames.Services;

public interface IPermissionService
{
	bool CanPublishDirectly(TeamMember member);
	bool CanDeleteDirectly(TeamMember member);
	bool CanManageTeam(TeamMember member);
}

/// <summary>
/// Single source of truth for permission checks in the app, so the same rule
/// is used wherever a button is enabled or an action is attempted. Mirrors
/// canAddGames()/canDeleteGames()/isAdmin() in firestore.rules, which enforce
/// it in the cloud. Admins always pass every check; moderators are gated by
/// their individual toggles, which an admin can change at any time.
/// </summary>
public class PermissionService : IPermissionService
{
	public bool CanPublishDirectly(TeamMember member) =>
		member.HasFullRights || member.CanAddGames;

	public bool CanDeleteDirectly(TeamMember member) =>
		member.HasFullRights || member.CanDeleteGames;

	public bool CanManageTeam(TeamMember member) =>
		member.HasFullRights;
}
