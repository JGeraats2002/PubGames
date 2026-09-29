using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>Profile of the signed-in user. Signing in happens on LoginPage; this page is only reachable once signed in.</summary>
public partial class AccountViewModel : ObservableObject
{
	private readonly IAuthService _auth;
	private readonly ITeamService _team;

	[ObservableProperty]
	private bool isTeamMember;

	[ObservableProperty]
	private string roleName = string.Empty;

	[ObservableProperty]
	private string roleDescription = string.Empty;

	[ObservableProperty]
	private string displayName = string.Empty;

	[ObservableProperty]
	private string email = string.Empty;

	public AccountViewModel(IAuthService auth, ITeamService team)
	{
		_auth = auth;
		_team = team;
	}

	public void Load()
	{
		var user = _auth.CurrentUser;
		DisplayName = user?.DisplayName ?? string.Empty;
		Email = user?.Email ?? string.Empty;

		var me = _team.Me;
		IsTeamMember = me is not null;
		RoleName = me?.RoleName ?? string.Empty;
		RoleDescription = me switch
		{
			null => string.Empty,
			{ IsAdmin: true } => "Full rights: publish, edit and delete games and add categories directly, review moderators' requests and manage the team.",
			_ => "You propose new games, changes, deletions and new categories as review requests. An admin approves them before players see anything; the decision arrives in your Inbox."
		};
	}

	/// <summary>App reacts to the sign-out and returns to LoginPage.</summary>
	[RelayCommand]
	private Task SignOutAsync() => _auth.SignOutAsync();
}
