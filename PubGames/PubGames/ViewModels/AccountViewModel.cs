using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// The signed-in user's own tab: their profile (picture, name, nickname), the
/// personal code others add them with, and their stats over every finished
/// game they played in - on their own phone or someone else's. Signing in
/// happens on LoginPage; this page is only reachable once signed in.
/// </summary>
public partial class AccountViewModel : ObservableObject
{
	private readonly IAuthService _auth;
	private readonly ITeamService _team;
	private readonly IProfileService _profiles;
	private readonly IStatsService _stats;

	[ObservableProperty]
	private bool isTeamMember;

	[ObservableProperty]
	private string roleName = string.Empty;

	/// <summary>The user's team membership, for the ⓘ explanation of their role; null for players.</summary>
	public TeamMember? Me { get; private set; }

	[ObservableProperty]
	private string email = string.Empty;

	// The profile, as edited on the page; saved with Save profile.
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasPhoto), nameof(HasNoPhoto))]
	private string? photoRef;

	public bool HasPhoto => !string.IsNullOrEmpty(PhotoRef);
	public bool HasNoPhoto => !HasPhoto;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Initials))]
	private string profileName = string.Empty;

	public string Initials => Player.InitialsFrom(ProfileName);

	[ObservableProperty]
	private string nickname = string.Empty;

	/// <summary>The personal code other players add you with.</summary>
	[ObservableProperty]
	private string code = "····-····";

	[ObservableProperty]
	private string profileMessage = string.Empty;

	[ObservableProperty]
	private bool isBusy;

	/// <summary>All games together; tapping them opens the stats per game (MyStatsPage).</summary>
	[ObservableProperty]
	private GameStats totals = GameStats.From("All games", []);

	[ObservableProperty]
	private string statsMessage = string.Empty;

	public AccountViewModel(IAuthService auth, ITeamService team, IProfileService profiles, IStatsService stats)
	{
		_auth = auth;
		_team = team;
		_profiles = profiles;
		_stats = stats;
	}

	public async Task LoadAsync()
	{
		Email = _auth.CurrentUser?.Email ?? string.Empty;
		var me = Me = _team.Me;
		IsTeamMember = me is not null;
		RoleName = me?.IsBuiltInAdmin == true ? "Owner" : me?.RoleName ?? string.Empty;

		if (_profiles.Mine is { } cached)
			Show(cached);
		try
		{
			if (await _profiles.LoadMineAsync() is { } profile)
			{
				Show(profile);
				await _team.UpdateMyNameAsync(profile.Name);
			}
			ProfileMessage = string.Empty;
		}
		catch (Exception ex)
		{
			ProfileMessage = ex.Message;
		}

		await LoadStatsAsync();
	}

	private void Show(UserProfile profile)
	{
		PhotoRef = profile.PhotoRef;
		ProfileName = profile.Name;
		Nickname = profile.Nickname;
		Code = UserProfile.FormatCode(profile.Code);
	}

	private async Task LoadStatsAsync()
	{
		try
		{
			// Games finished on this phone while offline go out first, so they count here too.
			await _stats.PublishFinishedGamesAsync();
		}
		catch (Exception)
		{
			// Offline: they go out next time.
		}

		var (games, offline) = await _stats.GetMyGamesAsync();
		Totals = GameStats.From("All games", games);
		StatsMessage = offline
			? "Offline - these are your stats as last seen on this phone."
			: games.Count == 0 ? "No finished games yet." : string.Empty;
	}

	[RelayCommand]
	private async Task ChangePhotoAsync()
	{
		IsBusy = true;
		ProfileMessage = "Uploading picture...";
		try
		{
			if (await _profiles.PickPhotoAsync() is { } reference)
			{
				PhotoRef = reference;
				await SaveAsync("Picture saved.");
			}
			else
				ProfileMessage = string.Empty;
		}
		catch (Exception ex)
		{
			ProfileMessage = ex is HttpRequestException ? "No connection - couldn't upload the picture." : ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	private async Task SaveProfileAsync()
	{
		if (string.IsNullOrWhiteSpace(ProfileName))
		{
			ProfileMessage = "Fill in your name.";
			return;
		}
		await SaveAsync("Profile saved.");
	}

	private async Task SaveAsync(string done)
	{
		if (_profiles.Mine is not { } current)
		{
			ProfileMessage = "Your profile hasn't loaded yet - check your connection and try again.";
			return;
		}
		try
		{
			await _profiles.SaveMineAsync(current with
			{
				Name = ProfileName.Trim(),
				Nickname = Nickname.Trim(),
				PhotoRef = PhotoRef
			});
			// Admins and moderators: the team sees this name on the Team page and when messaging.
			await _team.UpdateMyNameAsync(ProfileName);
			ProfileMessage = done;
		}
		catch (Exception ex)
		{
			ProfileMessage = ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message;
		}
	}

	[RelayCommand]
	private async Task CopyCodeAsync()
	{
		await Clipboard.Default.SetTextAsync(Code);
		ProfileMessage = "Code copied.";
	}

	/// <summary>App reacts to the sign-out and returns to LoginPage.</summary>
	[RelayCommand]
	private Task SignOutAsync() => _auth.SignOutAsync();
}
