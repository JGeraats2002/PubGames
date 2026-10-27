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

		var confirmed = await DisplayAlertAsync("Make admin?",
			$"{member.Label} will get full rights: publishing, editing and deleting games directly, reviewing requests and managing moderators.",
			"Make admin", "Cancel");
		if (confirmed)
			_vm.MakeAdminCommand.Execute(member);
	}

	/// <summary>The owner makes an admin moderator, after asking.</summary>
	private async void OnMakeModeratorClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: TeamMember member }) return;

		var confirmed = await DisplayAlertAsync("Make moderator?",
			$"{member.Label} will go back to proposing games, changes and deletions for an admin to review.",
			"Make moderator", "Cancel");
		if (confirmed)
			_vm.MakeModeratorCommand.Execute(member);
	}

	/// <summary>An admin must pick a reason; it's included in the email the person receives.</summary>
	private async void OnRemoveClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: TeamMember member }) return;

		var role = member.RoleName.ToLowerInvariant();
		var reason = await DisplayActionSheetAsync(
			$"Why are you removing {member.Label} as {role}? They'll receive this reason by email.",
			"Cancel", null, [.. TeamEmails.RemovalReasons, TeamEmails.OtherReason]);

		if (reason == TeamEmails.OtherReason)
			reason = await DisplayPromptAsync("Reason", $"Why is {member.Label}'s {role} role ending?",
				"Remove", "Cancel", maxLength: 300, keyboard: Keyboard.Text);

		if (string.IsNullOrWhiteSpace(reason) || reason == "Cancel") return;

		await _vm.RemoveMemberAsync(member, reason.Trim());
	}
}
