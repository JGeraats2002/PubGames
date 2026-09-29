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

	// Toggled also fires when the list is rebuilt; only real changes get saved.
	private void OnCanAddToggled(object? sender, ToggledEventArgs e)
	{
		if (sender is Switch { BindingContext: TeamMember member } && member.CanAddGames != e.Value)
			_vm.SetCanAddGamesCommand.Execute((member, e.Value));
	}

	private void OnCanDeleteToggled(object? sender, ToggledEventArgs e)
	{
		if (sender is Switch { BindingContext: TeamMember member } && member.CanDeleteGames != e.Value)
			_vm.SetCanDeleteGamesCommand.Execute((member, e.Value));
	}

	private async void OnPromoteClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: TeamMember member }) return;

		var confirmed = await DisplayAlertAsync("Make admin?",
			$"{member.Label} gets full rights: publishing, deleting, managing the team and approving requests.", "Make admin", "Cancel");
		if (confirmed)
			_vm.PromoteToAdminCommand.Execute(member);
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

	private void OnApproveClicked(object? sender, EventArgs e)
	{
		if (sender is Button { BindingContext: ApprovalRequest request })
			_vm.ResolveApprovalCommand.Execute((request, true));
	}

	private void OnDenyClicked(object? sender, EventArgs e)
	{
		if (sender is Button { BindingContext: ApprovalRequest request })
			_vm.ResolveApprovalCommand.Execute((request, false));
	}
}
