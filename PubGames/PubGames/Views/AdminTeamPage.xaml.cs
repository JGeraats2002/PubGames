using PubGames.Models;
using PubGames.Services;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class AdminTeamPage : ContentPage
{
	private readonly AdminTeamViewModel _vm;

	public AdminTeamPage(AdminTeamViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	private async void OnMakeAdminClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: TeamMember member }) return;

		var confirmed = await DisplayAlertAsync("Make admin?",
			$"{member.Label} gets full rights: publishing, editing and deleting games directly, reviewing requests and managing the team.",
			"Make admin", "Cancel");
		if (confirmed)
			_vm.MakeAdminCommand.Execute(member);
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
