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

	/// <summary>The admins and moderators.</summary>
	public ObservableCollection<TeamMember> Members { get; } = new();

	/// <summary>What happened, or why it couldn't.</summary>
	[ObservableProperty]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	/// <summary>The email of the moderator to add, as typed.</summary>
	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(AddModeratorCommand))]
	private string newModeratorEmail = string.Empty;

	/// <summary>A change is being saved: the buttons wait.</summary>
	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(AddModeratorCommand))]
	private bool isBusy;

	/// <summary>
	/// Gets the services it works with - the permissions, the sign-in, the team and email - from the app
	/// (dependency injection).
	/// </summary>
	public AdminTeamViewModel(IPermissionService permissions, IAuthService auth, ITeamService team, IEmailSender email)
	{
		_auth = auth;
		_permissions = permissions;
		_team = team;
		_email = email;
	}

	/// <summary>Shows the team as saved on the phone, then refreshes it from the cloud.</summary>
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

	/// <summary>
	/// Shows the team, with the buttons each member may get from the user (only the owner changes admins).
	/// </summary>
	private void ShowMembers(IEnumerable<TeamMember> members)
	{
		var owner = IAmOwner;
		Members.Clear();
		foreach (var member in members)
		{
			member.CanPromote = owner && !member.IsAdmin;
			member.CanDemote = owner && member.IsAdmin && !member.IsBuiltInAdmin;
			member.CanRemove = !member.IsBuiltInAdmin && (owner || !member.IsAdmin);
			Members.Add(member);
		}
	}

	/// <summary>A moderator can be added once an email is typed and nothing else is busy.</summary>
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

	/// <summary>
	/// Opens an email to `to` (or sends it, when the app sends automatically), then shows `done`.
	/// </summary>
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

	/// <summary>The owner makes a moderator admin or an admin moderator, in the cloud.</summary>
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

	/// <summary>Whether the user may change the team; otherwise the page says why not.</summary>
	private bool IsAllowedToManageTeam()
	{
		if (_team.Me is { } me && _permissions.CanManageTeam(me)) return true;

		StatusMessage = "Only admins can manage the team.";
		return false;
	}

	/// <summary>
	/// Whether the user may change admins (only the owner); otherwise the page says why not.
	/// </summary>
	private bool IsAllowedToManageAdmins()
	{
		if (IAmOwner) return true;

		StatusMessage = "Only the owner of the app can make someone admin, turn an admin back into a moderator or remove an admin. If you'd like a change, send the owner a message from your Inbox.";
		return false;
	}

	/// <summary>Whether the text is a whole email address (with a dot in the domain).</summary>
	private static bool IsValidEmail(string email) =>
		MailAddress.TryCreate(email, out var parsed) && parsed.Address == email && email.Contains('.', StringComparison.Ordinal);
}
