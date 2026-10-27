using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// The signed-in user's own tab. Its main page is Settings (AccountPage), with
/// a heading per subject; this view model also serves the Profile page:
/// picture, name, nickname and the personal code others add them with. Plus
/// their stats over every finished game they played in - on their own phone
/// or someone else's. Signing in happens on LoginPage; this tab is only
/// reachable once signed in.
/// </summary>
public partial class AccountViewModel : ObservableObject
{
	private readonly IAuthService _auth;
	private readonly ITeamService _team;
	private readonly IProfileService _profiles;
	private readonly IStatsService _stats;
	private readonly IStoreService _store;

	/// <summary>The owner of the app (a built-in admin): sees today's friend code.</summary>
	[ObservableProperty]
	private bool isOwner;

	/// <summary>The owner's friend code: 100% off a game, for friends. Works 15 minutes, or 30 after New code.</summary>
	[ObservableProperty]
	private string friendCode = "····-····";

	/// <summary>"Works until 14:45".</summary>
	[ObservableProperty]
	private string friendCodeValidity = string.Empty;

	/// <summary>When the code shown runs out (UTC); the page fetches the next one then.</summary>
	public DateTime? FriendCodeValidUntil { get; private set; }

	public async Task LoadFriendCodeAsync() => ShowFriendCode(IsOwner ? await _store.FriendCodeAsync() : null);

	/// <summary>New code: the old one stops working, the new one works for 30 minutes.</summary>
	[RelayCommand]
	private async Task RenewFriendCodeAsync()
	{
		ShowFriendCode(await _store.RenewFriendCodeAsync());
		ProfileMessage = FriendCodeValidUntil is null ? "Couldn't make a new code - check your connection." : "New code: the old one no longer works.";
	}

	private void ShowFriendCode((string Code, DateTime ValidUntil)? current)
	{
		if (!IsOwner) return;
		if (current is { } code)
		{
			FriendCode = code.Code;
			FriendCodeValidUntil = code.ValidUntil;
			FriendCodeValidity = $"Works until {code.ValidUntil.ToLocalTime():HH:mm}";
		}
		else
		{
			FriendCode = "····-····";
			FriendCodeValidUntil = null;
			FriendCodeValidity = "Not available offline";
		}
	}

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

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _profileMessageTimer = new();

	partial void OnProfileMessageChanged(string value) => _profileMessageTimer.Shown(value, () => ProfileMessage, v => ProfileMessage = v);

	[ObservableProperty]
	private bool isBusy;

	/// <summary>All games together; tapping them opens the stats per game (MyStatsPage).</summary>
	[ObservableProperty]
	private GameStats totals = GameStats.From("All games", []);

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasStatsMessage))]
	private string statsMessage = string.Empty;

	public bool HasStatsMessage => StatsMessage.Length > 0;

	public AccountViewModel(IAuthService auth, ITeamService team, IProfileService profiles, IStatsService stats, IStoreService store)
	{
		_store = store;
		_auth = auth;
		_team = team;
		_profiles = profiles;
		_stats = stats;
	}

	/// <param name="withStats">The Settings page shows the stats summary; the Profile page doesn't need it.</param>
	public async Task LoadAsync(bool withStats = true)
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

		// The owner's email is a built-in admin (AuthConfig.AdminEmails); the server checks the same.
		IsOwner = _auth.CurrentUser?.IsAdmin == true;
		if (withStats)
			await LoadStatsAsync();
		else
			await LoadFriendCodeAsync();
	}

	[RelayCommand]
	private async Task CopyFriendCodeAsync()
	{
		if (!FriendCode.Contains('-')) return;
		await Clipboard.Default.SetTextAsync(FriendCode);
		ProfileMessage = "Friend code copied.";
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
