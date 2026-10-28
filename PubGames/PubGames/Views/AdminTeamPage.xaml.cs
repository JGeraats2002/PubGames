using PubGames.Models;
using PubGames.Services;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// Admin tab &gt; Team: the admins and moderators, adding moderators, and (the owner) changing roles and
/// removing members.
/// </summary>
public partial class AdminTeamPage : ContentPage
{
	private readonly AdminTeamViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public AdminTeamPage(AdminTeamViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Team);
	}

	/// <summary>Every time the page shows: the team is loaded fresh.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	/// <summary>The owner makes a moderator admin, after asking.</summary>
	private async void OnMakeAdminClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: TeamMember member }) return;

		var confirmed = await DisplayAlertAsync(L.T("Make admin?"),
			L.F("{0} will get full rights: publishing, editing and deleting games directly, reviewing requests and managing moderators.", member.Label),
			L.T("Make admin"), L.T("Cancel"));
		if (confirmed)
			_vm.MakeAdminCommand.Execute(member);
	}

	/// <summary>The owner makes an admin moderator, after asking.</summary>
	private async void OnMakeModeratorClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: TeamMember member }) return;

		var confirmed = await DisplayAlertAsync(L.T("Make moderator?"),
			L.F("{0} will go back to proposing games, changes and deletions for an admin to review.", member.Label),
			L.T("Make moderator"), L.T("Cancel"));
		if (confirmed)
			_vm.MakeModeratorCommand.Execute(member);
	}

	/// <summary>An admin must pick a reason; it's included in the email the person receives.</summary>
	private async void OnRemoveClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: TeamMember member }) return;

		var role = member.RoleName.ToLowerInvariant();
		var reason = await DisplayActionSheetAsync(
			L.F("Why are you removing {0} as {1}? They'll receive this reason by email.", member.Label, role),
			L.T("Cancel"), null, [.. TeamEmails.RemovalReasons, TeamEmails.OtherReason]);

		if (reason == TeamEmails.OtherReason)
			reason = await DisplayPromptAsync(L.T("Reason"), L.F("Why is {0}'s {1} role ending?", member.Label, role),
				L.T("Remove"), L.T("Cancel"), maxLength: 300, keyboard: Keyboard.Text);

		if (string.IsNullOrWhiteSpace(reason) || reason == L.T("Cancel")) return;

		await _vm.RemoveMemberAsync(member, reason.Trim());
	}
}
