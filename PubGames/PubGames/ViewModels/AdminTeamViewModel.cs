using System.Collections.ObjectModel;
using System.Net.Mail;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// Admins: add and remove moderators by email. Only the owner makes someone
/// admin, turns an admin back into a moderator or removes an admin; other
/// admins can send the owner a message for that.
/// </summary>
public partial class AdminTeamViewModel : ObservableObject
{
	private readonly IPermissionService _permissions;
	private readonly IAuthService _auth;
	private readonly ITeamService _team;
	private readonly IEmailSender _email;

	private string AdminName => _auth.CurrentUser is { } u
		? (string.IsNullOrWhiteSpace(u.DisplayName) ? u.Email : u.DisplayName)
		: "An admin";

	public ObservableCollection<TeamMember> Members { get; } = new();

	[ObservableProperty]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(AddModeratorCommand))]
	private string newModeratorEmail = string.Empty;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(AddModeratorCommand))]
	private bool isBusy;

	public AdminTeamViewModel(IPermissionService permissions, IAuthService auth, ITeamService team, IEmailSender email)
	{
		_auth = auth;
		_permissions = permissions;
		_team = team;
		_email = email;
	}

	public async Task LoadAsync()
	{
		await _auth.InitializeAsync();
		ShowMembers(await _team.GetCachedMembersAsync());

		try
		{
			ShowMembers(await _team.PullMembersAsync());
			StatusMessage = string.Empty;
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException
				? "Offline - showing the team as last seen on this phone."
				: $"Couldn't refresh the team: {ex.Message}";
		}
	}

	private bool IAmOwner => _team.Me is { } me && _permissions.CanManageAdmins(me);

	private void ShowMembers(IEnumerable<TeamMember> members)
	{
		var owner = IAmOwner;
		Members.Clear();
		foreach (var m in members)
		{
			m.CanPromote = owner && !m.IsAdmin;
			m.CanDemote = owner && m.IsAdmin && !m.IsBuiltInAdmin;
			m.CanRemove = !m.IsBuiltInAdmin && (owner || !m.IsAdmin);
			Members.Add(m);
		}
	}

	private bool CanAddModerator() => !IsBusy && !string.IsNullOrWhiteSpace(NewModeratorEmail);

	/// <summary>Adds the person to the team in the cloud, then emails them what a moderator does and how the app works.</summary>
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

		var member = new TeamMember { Email = email, Role = TeamRole.Moderator };

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

		var (subject, body) = TeamEmails.ModeratorAdded(member, AdminName);
		await SendEmailAsync(member.Email, subject, body, $"{email} is now a moderator.");
	}

	/// <summary>Removes the member in the cloud (their rights end on their next app start), then emails them the reason.</summary>
	public async Task RemoveMemberAsync(TeamMember member, string reason)
	{
		if (!IsAllowedToManageTeam() || member.IsBuiltInAdmin) return;
		if (member.IsAdmin && !IsAllowedToManageAdmins()) return;

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

	/// <summary>Owner only. Promotion adds a peer admin - it does not replace/remove the promoter.</summary>
	[RelayCommand]
	private Task MakeAdminAsync(TeamMember member) => ChangeRoleAsync(member, TeamRole.Admin, $"{member.Label} is now an admin.");

	/// <summary>Owner only: an admin goes back to being a moderator.</summary>
	[RelayCommand]
	private Task MakeModeratorAsync(TeamMember member) => ChangeRoleAsync(member, TeamRole.Moderator, $"{member.Label} is now a moderator.");

	private async Task ChangeRoleAsync(TeamMember member, TeamRole role, string done)
	{
		if (!IsAllowedToManageAdmins() || member.IsBuiltInAdmin) return;

		var previous = member.Role;
		member.Role = role;
		try
		{
			await _team.SaveMemberAsync(member);
			StatusMessage = done;
		}
		catch (Exception ex)
		{
			member.Role = previous;
			StatusMessage = ex is HttpRequestException ? "No connection - the change wasn't saved." : ex.Message;
		}
		// Re-read so the page shows what's actually stored.
		ShowMembers(await _team.GetCachedMembersAsync());
	}

	private bool IsAllowedToManageTeam()
	{
		if (_team.Me is { } me && _permissions.CanManageTeam(me)) return true;

		StatusMessage = "Only admins can manage the team.";
		return false;
	}

	private bool IsAllowedToManageAdmins()
	{
		if (IAmOwner) return true;

		StatusMessage = "Only the owner of the app can make someone admin, turn an admin back into a moderator or remove an admin. If you'd like a change, send the owner a message from your Inbox.";
		return false;
	}

	private static bool IsValidEmail(string email) =>
		MailAddress.TryCreate(email, out var parsed) && parsed.Address == email && email.Contains('.', StringComparison.Ordinal);
}
