using PubGames.Models;

namespace PubGames.Services;

public interface IPermissionService
{
	bool CanPublishDirectly(HostMember member);
	bool CanDeleteDirectly(HostMember member);
	bool CanManageTeam(HostMember member);
}

/// <summary>
/// Single source of truth for permission checks, so the same rule is used
/// whether it's a button being enabled in the UI or a request being resolved
/// server-side. Head hosts always pass every check; subhosts are gated by
/// their individual toggles, which a head host can change at any time.
/// </summary>
public class PermissionService : IPermissionService
{
	public bool CanPublishDirectly(HostMember member) =>
		member.HasFullRights || member.CanAddGames;

	public bool CanDeleteDirectly(HostMember member) =>
		member.HasFullRights || member.CanDeleteGames;

	public bool CanManageTeam(HostMember member) =>
		member.HasFullRights;
}
