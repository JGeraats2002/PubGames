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
		: L.T("An admin");

	/// <summary>Everyone on the team, as last loaded (before search and filter).</summary>
	private List<TeamMember> _members = [];

	/// <summary>The admins that match the search and filter, in alphabetical order.</summary>
	public ObservableCollection<TeamMember> Admins { get; } = new();

	/// <summary>The moderators that match the search and filter, in alphabetical order.</summary>
	public ObservableCollection<TeamMember> Moderators { get; } = new();

	/// <summary>What's typed in the search bar: matches a member's name or email.</summary>
	[ObservableProperty]
	private string searchText = string.Empty;

	partial void OnSearchTextChanged(string value) => ApplyFilter();

	/// <summary>Which roles are shown: everyone, only admins or only moderators.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ShowingAll), nameof(ShowingAdmins), nameof(ShowingModerators))]
	private TeamFilter filter = TeamFilter.All;

	partial void OnFilterChanged(TeamFilter value) => ApplyFilter();

	/// <summary>The All button is selected.</summary>
	public bool ShowingAll => Filter == TeamFilter.All;

	/// <summary>The Admins button is selected.</summary>
	public bool ShowingAdmins => Filter == TeamFilter.Admins;

	/// <summary>The Moderators button is selected.</summary>
	public bool ShowingModerators => Filter == TeamFilter.Moderators;

	/// <summary>The admins list shows: the filter allows it and someone matches.</summary>
	public bool HasAdmins => Filter != TeamFilter.Moderators && Admins.Count > 0;

	/// <summary>The moderators list shows: the filter allows it and someone matches.</summary>
	public bool HasModerators => Filter != TeamFilter.Admins && Moderators.Count > 0;

	/// <summary>Heading above the admins, with how many there are.</summary>
	public string AdminsHeading => L.F("Admins ({0})", Admins.Count);

	/// <summary>Heading above the moderators, with how many there are.</summary>
	public string ModeratorsHeading => L.F("Moderators ({0})", Moderators.Count);

	/// <summary>Nobody matches the search and filter.</summary>
	public bool HasNoMatches => _members.Count > 0 && !HasAdmins && !HasModerators;

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
				? L.T("Offline - showing the team as last seen on this phone.")
				: L.F("Couldn't refresh the team: {0}", ex.Message);
		}
	}

	private bool IAmOwner => _team.Me is { } me && _permissions.CanManageAdmins(me);

	/// <summary>
	/// Shows the team, with the buttons each member may get from the user (only the owner changes admins).
	/// </summary>
	private void ShowMembers(IEnumerable<TeamMember> members)
	{
		var owner = IAmOwner;
		_members = [.. members];
		foreach (var member in _members)
		{
			member.CanPromote = owner && !member.IsAdmin;
			member.CanDemote = owner && member.IsAdmin && !member.IsBuiltInAdmin;
			member.CanRemove = !member.IsBuiltInAdmin && (owner || !member.IsAdmin);
		}
		ApplyFilter();
	}

	/// <summary>Shows only the given roles (the All, Admins and Moderators buttons).</summary>
	[RelayCommand]
	private void SetFilter(TeamFilter value) => Filter = value;

	/// <summary>
	/// Fills the two lists with the members that match the search (name or email) and the filter, each sorted
	/// alphabetically by name, or by email when there's no name.
	/// </summary>
	private void ApplyFilter()
	{
		var search = SearchText.Trim();
		var matches = _members
			.Where(member => search.Length == 0
				|| member.Label.Contains(search, StringComparison.CurrentCultureIgnoreCase)
				|| member.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
			.OrderBy(member => member.Label, StringComparer.CurrentCultureIgnoreCase)
			.ToList();

		Admins.Clear();
		Moderators.Clear();
		foreach (var member in matches)
		{
			if (member.IsAdmin)
			{
				if (Filter != TeamFilter.Moderators) Admins.Add(member);
			}
			else if (Filter != TeamFilter.Admins)
			{
				Moderators.Add(member);
			}
		}

		OnPropertyChanged(nameof(HasAdmins));
		OnPropertyChanged(nameof(HasModerators));
		OnPropertyChanged(nameof(AdminsHeading));
		OnPropertyChanged(nameof(ModeratorsHeading));
		OnPropertyChanged(nameof(HasNoMatches));
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
			StatusMessage = L.T("That doesn't look like an email address.");
			return;
		}
		if (_members.FirstOrDefault(m => m.Email == email) is { } existing)
		{
			StatusMessage = L.F("{0} is already on the team as {1}.", email, existing.RoleName.ToLowerInvariant());
			return;
		}

		var member = new TeamMember { Email = email, Role = TeamRole.Moderator };

		IsBusy = true;
		StatusMessage = L.T("Adding moderator...");
		try
		{
			// Cloud first: the email promises access, so it must already be real.
			await _team.SaveMemberAsync(member);
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
			return;
		}
		finally
		{
			IsBusy = false;
		}

		ShowMembers(await _team.GetCachedMembersAsync());
		NewModeratorEmail = string.Empty;

		var (subject, body) = TeamEmails.ModeratorAdded(member, AdminName);
		await SendEmailAsync(member.Email, subject, body, L.F("{0} is now a moderator.", email));
	}

	/// <summary>Removes the member in the cloud (their rights end on their next app start), then emails them the reason.</summary>
	public async Task RemoveMemberAsync(TeamMember member, string reason)
	{
		if (!IsAllowedToManageTeam() || member.IsBuiltInAdmin) return;
		if (member.IsAdmin && !IsAllowedToManageAdmins()) return;

		StatusMessage = L.F("Removing {0}...", member.Email);
		try
		{
			await _team.RemoveMemberAsync(member);
		}
		catch (Exception ex)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - try again when you're online.") : ex.Message;
			return;
		}

		_members.Remove(member);
		ApplyFilter();
		var (subject, body) = TeamEmails.MemberRemoved(member, reason, AdminName);
		await SendEmailAsync(member.Email, subject, body, L.F("{0} is no longer {1}.", member.Email, member.RoleName.ToLowerInvariant()));
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
				? L.F("{0} They've been emailed.", done)
				: L.F("{0} Tap Send in your mail app to email them.", done);
		}
		catch (Exception ex)
		{
			StatusMessage = L.F("{0} But the email couldn't be sent: {1}", done, ex.Message);
		}
	}

	/// <summary>Owner only. Promotion adds a peer admin - it does not replace/remove the promoter.</summary>
	[RelayCommand]
	private Task MakeAdminAsync(TeamMember member) => ChangeRoleAsync(member, TeamRole.Admin, L.F("{0} is now an admin.", member.Label));

	/// <summary>Owner only: an admin goes back to being a moderator.</summary>
	[RelayCommand]
	private Task MakeModeratorAsync(TeamMember member) => ChangeRoleAsync(member, TeamRole.Moderator, L.F("{0} is now a moderator.", member.Label));

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
			StatusMessage = ex is HttpRequestException ? L.T("No connection - the change wasn't saved.") : ex.Message;
		}
		// Re-read so the page shows what's actually stored.
		ShowMembers(await _team.GetCachedMembersAsync());
	}

	/// <summary>Whether the user may change the team; otherwise the page says why not.</summary>
	private bool IsAllowedToManageTeam()
	{
		if (_team.Me is { } me && _permissions.CanManageTeam(me)) return true;

		StatusMessage = L.T("Only admins can manage the team.");
		return false;
	}

	/// <summary>
	/// Whether the user may change admins (only the owner); otherwise the page says why not.
	/// </summary>
	private bool IsAllowedToManageAdmins()
	{
		if (IAmOwner) return true;

		StatusMessage = L.T("Only the owner of the app can make someone admin, turn an admin back into a moderator or remove an admin. If you'd like a change, send the owner a message from your Inbox.");
		return false;
	}

	/// <summary>Whether the text is a whole email address (with a dot in the domain).</summary>
	private static bool IsValidEmail(string email) =>
		MailAddress.TryCreate(email, out var parsed) && parsed.Address == email && email.Contains('.', StringComparison.Ordinal);
}

/// <summary>Which roles the Team page shows.</summary>
public enum TeamFilter
{
	/// <summary>Admins and moderators.</summary>
	All,

	/// <summary>Only admins.</summary>
	Admins,

	/// <summary>Only moderators.</summary>
	Moderators
}
