using System.Collections.ObjectModel;
using System.Net.Mail;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

public partial class AdminTeamViewModel : ObservableObject
{
	private readonly ILocalDatabaseService _local;
	private readonly ICloudSyncService _cloud;
	private readonly IPermissionService _permissions;
	private readonly IAuthService _auth;
	private readonly ITeamService _team;
	private readonly IEmailSender _email;

	// TODO: replace with the team the user picked once multiple teams exist.
	private const string CurrentTeamId = AuthService.DefaultTeamId;

	private string CurrentUserId => _auth.AccountId;

	private string AdminName => _auth.CurrentUser is { } u
		? (string.IsNullOrWhiteSpace(u.DisplayName) ? u.Email : u.DisplayName)
		: "An admin";

	public ObservableCollection<TeamMember> Members { get; } = new();
	public ObservableCollection<ApprovalRequest> PendingApprovals { get; } = new();

	[ObservableProperty]
	private string statusMessage = string.Empty;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(AddModeratorCommand))]
	private string newModeratorEmail = string.Empty;

	[ObservableProperty]
	private bool newModeratorCanAddGames = true;

	[ObservableProperty]
	private bool newModeratorCanDeleteGames;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(AddModeratorCommand))]
	private bool isBusy;

	public AdminTeamViewModel(ILocalDatabaseService local, ICloudSyncService cloud, IPermissionService permissions,
		IAuthService auth, ITeamService team, IEmailSender email)
	{
		_auth = auth;
		_local = local;
		_cloud = cloud;
		_permissions = permissions;
		_team = team;
		_email = email;
	}

	public async Task LoadAsync()
	{
		await _auth.InitializeAsync();
		ShowMembers(await _team.GetCachedMembersAsync());
		ShowApprovals(await _local.GetPendingApprovalsAsync(CurrentTeamId));

		try
		{
			ShowMembers(await _team.PullMembersAsync());
			ShowApprovals(await _cloud.PullPendingApprovalsAsync(CurrentTeamId));
			StatusMessage = string.Empty;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException
				? "Offline - showing the team as last seen on this phone."
				: $"Couldn't refresh the team: {ex.Message}";
		}
	}

	private void ShowMembers(IEnumerable<TeamMember> members)
	{
		Members.Clear();
		foreach (var m in members)
			Members.Add(m);
	}

	private void ShowApprovals(IEnumerable<ApprovalRequest> requests)
	{
		PendingApprovals.Clear();
		foreach (var r in requests)
			PendingApprovals.Add(r);
	}

	private bool CanAddModerator() => !IsBusy && !string.IsNullOrWhiteSpace(NewModeratorEmail);

	/// <summary>Adds the person to the team in the cloud, then emails them their permissions and how the app works.</summary>
	[RelayCommand(CanExecute = nameof(CanAddModerator))]
	private async Task AddModeratorAsync()
	{
		if (!IsAllowedToManageTeam()) return;

		var email = TeamMember.NormalizeEmail(NewModeratorEmail);
		if (!IsValidEmail(email))
		{
			StatusMessage = "That doesn't look like an email address.";
			return;
		}
		if (Members.FirstOrDefault(m => m.Email == email) is { } existing)
		{
			StatusMessage = $"{email} is already on the team as {existing.RoleName.ToLowerInvariant()}.";
			return;
		}

		var member = new TeamMember
		{
			Email = email,
			Role = TeamRole.Moderator,
			CanAddGames = NewModeratorCanAddGames,
			CanDeleteGames = NewModeratorCanDeleteGames
		};

		IsBusy = true;
		StatusMessage = "Adding moderator...";
		try
		{
			// Cloud first: the email promises access, so it must already be real.
			await _team.SaveMemberAsync(member);
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message;
			return;
		}
		finally
		{
			IsBusy = false;
		}

		ShowMembers(await _team.GetCachedMembersAsync());
		NewModeratorEmail = string.Empty;
		NewModeratorCanAddGames = true;
		NewModeratorCanDeleteGames = false;

		var (subject, body) = TeamEmails.ModeratorAdded(member, AdminName);
		await SendEmailAsync(member.Email, subject, body, $"{email} is now a moderator.");
	}

	/// <summary>Removes the member in the cloud (their rights end on their next app start), then emails them the reason.</summary>
	public async Task RemoveMemberAsync(TeamMember member, string reason)
	{
		if (!IsAllowedToManageTeam() || member.IsBuiltInAdmin) return;

		StatusMessage = $"Removing {member.Email}...";
		try
		{
			await _team.RemoveMemberAsync(member);
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message;
			return;
		}

		Members.Remove(member);
		var (subject, body) = TeamEmails.MemberRemoved(member, reason, AdminName);
		await SendEmailAsync(member.Email, subject, body, $"{member.Email} is no longer {member.RoleName.ToLowerInvariant()}.");
	}

	private async Task SendEmailAsync(string to, string subject, string body, string done)
	{
		try
		{
			await _email.SendAsync(to, subject, body, _auth.CurrentUser?.Email ?? string.Empty);
			StatusMessage = _email.SendsAutomatically
				? $"{done} They've been emailed."
				: $"{done} Tap Send in your mail app to email them.";
		}
		catch (Exception ex)
		{
			StatusMessage = $"{done} But the email couldn't be sent: {ex.Message}";
		}
	}

	[RelayCommand]
	private Task SetCanAddGamesAsync((TeamMember member, bool value) args) =>
		UpdateMemberAsync(args.member, m => m.CanAddGames = args.value);

	[RelayCommand]
	private Task SetCanDeleteGamesAsync((TeamMember member, bool value) args) =>
		UpdateMemberAsync(args.member, m => m.CanDeleteGames = args.value);

	/// <summary>Promotion adds a peer admin - it does not replace/remove the promoter.</summary>
	[RelayCommand]
	private Task PromoteToAdminAsync(TeamMember member) =>
		UpdateMemberAsync(member, m =>
		{
			m.Role = TeamRole.Admin;
			m.CanAddGames = true;
			m.CanDeleteGames = true;
		});

	private async Task UpdateMemberAsync(TeamMember member, Action<TeamMember> change)
	{
		if (!IsAllowedToManageTeam() || member.IsBuiltInAdmin) return;

		change(member);
		try
		{
			await _team.SaveMemberAsync(member);
			StatusMessage = string.Empty;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? "No connection - the change wasn't saved." : ex.Message;
		}
		// Re-read so the page shows what's actually stored (the switch may have moved without saving).
		ShowMembers(await _team.GetCachedMembersAsync());
	}

	/// <summary>
	/// Any admin (not just the team's original creator) can resolve a
	/// pending request. Approving a delete marks the game Deleted; approving
	/// a publish makes it Published; denying sends it back to Draft.
	/// </summary>
	[RelayCommand]
	private async Task ResolveApprovalAsync((ApprovalRequest request, bool approve) args)
	{
		if (!IsAllowedToManageTeam()) return;

		try
		{
			// The moderator's game may not be on this phone yet.
			var game = await _local.GetGameAsync(args.request.GameId) ?? await _cloud.PullGameAsync(args.request.GameId);
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
				else if (args.request.Type == ApprovalRequestType.PublishGame)
				{
					// Denied: fall back to Draft so the moderator can revise and resubmit.
					game.Status = GameStatus.Draft;
				}

				await _cloud.SaveGameAsync(game);
				await _local.SaveGameAsync(game);
			}

			args.request.Status = args.approve ? ApprovalRequestStatus.Approved : ApprovalRequestStatus.Denied;
			args.request.ResolvedByUserId = CurrentUserId;
			args.request.ResolvedAt = DateTime.UtcNow;
			await _cloud.ResolveApprovalRequestAsync(args.request);
		}
		catch (Exception ex)
		{
			args.request.Status = ApprovalRequestStatus.Pending;
			StatusMessage = ex is HttpRequestException
				? "No connection - try again when you're online."
				: ex.Message;
			return;
		}

		PendingApprovals.Remove(args.request);
	}

	private bool IsAllowedToManageTeam()
	{
		var me = _team.Me;
		if (me is not null && _permissions.CanManageTeam(me)) return true;

		StatusMessage = "Only admins can manage the team.";
		return false;
	}

	private static bool IsValidEmail(string email) =>
		MailAddress.TryCreate(email, out var parsed) && parsed.Address == email && email.Contains('.', StringComparison.Ordinal);
}
