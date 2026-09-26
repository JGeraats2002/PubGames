using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class HostTeamPage : ContentPage
{
	private readonly HostTeamViewModel _vm;

	public HostTeamPage(HostTeamViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	private void OnCanAddToggled(object? sender, ToggledEventArgs e)
	{
		if (sender is Switch { BindingContext: HostMember member })
			_vm.SetCanAddGamesCommand.Execute((member, e.Value));
	}

	private void OnCanDeleteToggled(object? sender, ToggledEventArgs e)
	{
		if (sender is Switch { BindingContext: HostMember member })
			_vm.SetCanDeleteGamesCommand.Execute((member, e.Value));
	}

	private void OnPromoteClicked(object? sender, EventArgs e)
	{
		if (sender is Button { BindingContext: HostMember member })
			_vm.PromoteToHeadHostCommand.Execute(member);
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
