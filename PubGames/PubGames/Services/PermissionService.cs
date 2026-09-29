using PubGames.Models;

namespace PubGames.Services;

public interface IPermissionService
{
	/// <summary>Publish, edit and delete games without a review. Everyone else submits review requests.</summary>
	bool CanChangeGamesDirectly(TeamMember member);

	/// <summary>Submit review requests: new games, changes to games, deletions.</summary>
	bool CanSubmitReviewRequests(TeamMember member);

	/// <summary>Approve or reject review requests.</summary>
	bool CanReviewRequests(TeamMember member);

	/// <summary>Add and remove moderators, make someone admin.</summary>
	bool CanManageTeam(TeamMember member);
}

/// <summary>
/// Single source of truth for permission checks in the app, so the same rule
/// is used wherever a button is shown or an action is attempted. Mirrors
/// isAdmin()/isTeamMember() in firestore.rules, which enforce it in the cloud.
/// Admins can do everything; moderators only submit review requests.
/// </summary>
public class PermissionService : IPermissionService
{
	public bool CanChangeGamesDirectly(TeamMember member) => member.IsAdmin;

	public bool CanSubmitReviewRequests(TeamMember member) => true;

	public bool CanReviewRequests(TeamMember member) => member.IsAdmin;

	public bool CanManageTeam(TeamMember member) => member.IsAdmin;
}
