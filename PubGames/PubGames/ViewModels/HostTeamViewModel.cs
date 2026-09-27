using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

public partial class HostTeamViewModel : ObservableObject
{
	private readonly ILocalDatabaseService _local;
	private readonly ICloudSyncService _cloud;
	private readonly IPermissionService _permissions;
	private readonly IAuthService _auth;

	// TODO: replace with the org the user picked once multiple orgs exist.
	private const string CurrentHostOrgId = AuthService.DefaultHostOrgId;

	private string CurrentUserId => _auth.AccountId;

	public ObservableCollection<HostMember> Members { get; } = new();
	public ObservableCollection<ApprovalRequest> PendingApprovals { get; } = new();

	public HostTeamViewModel(ILocalDatabaseService local, ICloudSyncService cloud, IPermissionService permissions, IAuthService auth)
	{
		_auth = auth;
		_local = local;
		_cloud = cloud;
		_permissions = permissions;
	}

	public async Task LoadAsync()
	{
		await _auth.InitializeAsync();
		Members.Clear();
		foreach (var m in await _local.GetHostMembersAsync(CurrentHostOrgId))
			Members.Add(m);

		PendingApprovals.Clear();
		foreach (var r in await _local.GetPendingApprovalsAsync(CurrentHostOrgId))
			PendingApprovals.Add(r);
	}

	private HostMember? CurrentMember() => _permissions.ResolveMember(Members, _auth.CurrentUser, CurrentHostOrgId);

	[RelayCommand]
	private async Task SetCanAddGamesAsync((HostMember member, bool value) args)
	{
		if (!IsAllowedToManageTeam()) return;
		args.member.CanAddGames = args.value;
		await _local.SaveHostMemberAsync(args.member);
	}

	[RelayCommand]
	private async Task SetCanDeleteGamesAsync((HostMember member, bool value) args)
	{
		if (!IsAllowedToManageTeam()) return;
		args.member.CanDeleteGames = args.value;
		await _local.SaveHostMemberAsync(args.member);
	}

	/// <summary>Promotion adds a peer head host - it does not replace/remove the promoter.</summary>
	[RelayCommand]
	private async Task PromoteToHeadHostAsync(HostMember member)
	{
		if (!IsAllowedToManageTeam()) return;

		member.Role = HostRole.HeadHost;
		member.CanAddGames = true;
		member.CanDeleteGames = true;
		await _local.SaveHostMemberAsync(member);
	}

	/// <summary>
	/// Any head host (not just the org's original creator) can resolve a
	/// pending request. Approving a delete marks the game Deleted; approving
	/// a publish makes it Published; denying just clears the pending state.
	/// </summary>
	[RelayCommand]
	private async Task ResolveApprovalAsync((ApprovalRequest request, bool approve) args)
	{
		if (!IsAllowedToManageTeam()) return;

		var game = await _local.GetGameAsync(args.request.GameId);
		if (game is not null)
		{
			if (args.approve)
			{
				game.Status = args.request.Type == ApprovalRequestType.DeleteGame
					? GameStatus.Deleted
					: GameStatus.Published;
				if (game.Status == GameStatus.Published)
					game.PublishedAt = DateTime.UtcNow;
			}
			else
			{
				// Denied: fall back to Draft so the host can revise and resubmit.
				game.Status = GameStatus.Draft;
			}
			await _local.SaveGameAsync(game);
		}

		args.request.Status = args.approve ? ApprovalRequestStatus.Approved : ApprovalRequestStatus.Denied;
		args.request.ResolvedByUserId = CurrentUserId;
		args.request.ResolvedAt = DateTime.UtcNow;
		await _cloud.ResolveApprovalRequestAsync(args.request.Id, args.approve, CurrentUserId);

		PendingApprovals.Remove(args.request);
	}

	private bool IsAllowedToManageTeam()
	{
		var me = CurrentMember();
		return me is not null && _permissions.CanManageTeam(me);
	}
}
