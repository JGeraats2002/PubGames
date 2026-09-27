using PubGames.Models;

namespace PubGames.Services;

public interface IPermissionService
{
	bool CanPublishDirectly(HostMember member);
	bool CanDeleteDirectly(HostMember member);
	bool CanManageTeam(HostMember member);

	/// <summary>
	/// The membership to check permissions against for this user in this org:
	/// app admins always act as head host, even without a stored row; null means
	/// the user isn't on this org's team at all.
	/// </summary>
	HostMember? ResolveMember(IEnumerable<HostMember> members, AppUser? user, string hostOrgId);
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

	public HostMember? ResolveMember(IEnumerable<HostMember> members, AppUser? user, string hostOrgId)
	{
		if (user is null) return null;

		if (user.IsAdmin)
			return new HostMember { UserId = user.Uid, HostOrgId = hostOrgId, DisplayName = user.DisplayName, Role = HostRole.HeadHost };

		return members.FirstOrDefault(m => m.UserId == user.Uid);
	}
}
