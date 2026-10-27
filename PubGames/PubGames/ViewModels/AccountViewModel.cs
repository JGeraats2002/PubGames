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
	private readonly FunctionsClient _functions;
	private readonly ILocalDatabaseService _local;

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

	/// <summary>
	/// The owner only: fetches the friend code that works now and shows it with until when.
	/// </summary>
	public async Task LoadFriendCodeAsync() => ShowFriendCode(IsOwner ? await _store.FriendCodeAsync() : null);

	/// <summary>New code: the old one stops working, the new one works for 30 minutes.</summary>
	[RelayCommand]
	private async Task RenewFriendCodeAsync()
	{
		ShowFriendCode(await _store.RenewFriendCodeAsync());
		ProfileMessage = FriendCodeValidUntil is null ? "Couldn't make a new code - check your connection." : "New code: the old one no longer works.";
	}

	/// <summary>
	/// Shows a friend code and until when it works, or that it can't be fetched (offline).
	/// </summary>
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

	/// <summary>Whether the user is an admin or moderator: their role shows on the profile.</summary>
	[ObservableProperty]
	private bool isTeamMember;

	/// <summary>"Owner", "Admin" or "Moderator", shown on the profile.</summary>
	[ObservableProperty]
	private string roleName = string.Empty;

	/// <summary>The user's team membership, for the ⓘ explanation of their role; null for players.</summary>
	public TeamMember? Me { get; private set; }

	/// <summary>The email of the signed-in account.</summary>
	[ObservableProperty]
	private string email = string.Empty;

	// The profile, as edited on the page; saved with Save profile.
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasPhoto), nameof(HasNoPhoto))]
	private string? photoRef;

	/// <summary>Whether the profile has a picture.</summary>
	public bool HasPhoto => !string.IsNullOrEmpty(PhotoRef);
	/// <summary>No picture: the initials show instead.</summary>
	public bool HasNoPhoto => !HasPhoto;

	/// <summary>The name, as typed on the Profile page.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Initials))]
	private string profileName = string.Empty;

	/// <summary>The initials of the name, shown without a picture.</summary>
	public string Initials => Player.InitialsFrom(ProfileName);

	/// <summary>The nickname other players see, as typed on the Profile page.</summary>
	[ObservableProperty]
	private string nickname = string.Empty;

	/// <summary>The personal code other players add you with.</summary>
	[ObservableProperty]
	private string code = "····-····";

	/// <summary>A short message about the profile: saved, copied, or what went wrong.</summary>
	[ObservableProperty]
	private string profileMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _profileMessageTimer = new();

	partial void OnProfileMessageChanged(string value) => _profileMessageTimer.Shown(value, () => ProfileMessage, v => ProfileMessage = v);

	/// <summary>A picture is uploading: a spinner shows.</summary>
	[ObservableProperty]
	private bool isBusy;

	/// <summary>All games together; tapping them opens the stats per game (MyStatsPage).</summary>
	[ObservableProperty]
	private GameStats totals = GameStats.From("All games", []);

	/// <summary>About the stats: offline, or no finished games yet.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasStatsMessage))]
	private string statsMessage = string.Empty;

	/// <summary>Whether there's something to say about the stats.</summary>
	public bool HasStatsMessage => StatsMessage.Length > 0;

	/// <summary>
	/// Gets the services it works with - the sign-in, the team, profiles, stats, the store, the server
	/// functions and the phone's database - from the app (dependency injection).
	/// </summary>
	public AccountViewModel(IAuthService auth, ITeamService team, IProfileService profiles, IStatsService stats, IStoreService store,
		FunctionsClient functions, ILocalDatabaseService local)
	{
		_functions = functions;
		_local = local;
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

	/// <summary>Puts the friend code on the clipboard, to paste in a message to a friend.</summary>
	[RelayCommand]
	private async Task CopyFriendCodeAsync()
	{
		if (!FriendCode.Contains('-')) return;
		await Clipboard.Default.SetTextAsync(FriendCode);
		ProfileMessage = "Friend code copied.";
	}

	/// <summary>Fills the page with a profile: picture, name, nickname and player code.</summary>
	private void Show(UserProfile profile)
	{
		PhotoRef = profile.PhotoRef;
		ProfileName = profile.Name;
		Nickname = profile.Nickname;
		Code = UserProfile.FormatCode(profile.Code);
	}

	/// <summary>
	/// Sends games finished offline first, then shows the user's stats over all finished games (or the copy
	/// on the phone when offline).
	/// </summary>
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

	/// <summary>Lets the user pick a new profile picture, uploads it and saves the profile.</summary>
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

	/// <summary>Saves the name and nickname; a name is required.</summary>
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

	/// <summary>
	/// Saves the profile in the cloud (and the name on the team list for admins and moderators), then shows
	/// `done`.
	/// </summary>
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

	/// <summary>Puts the player code on the clipboard, to share it.</summary>
	[RelayCommand]
	private async Task CopyCodeAsync()
	{
		await Clipboard.Default.SetTextAsync(Code);
		ProfileMessage = "Code copied.";
	}

	/// <summary>
	/// Deletes the account for good: in the cloud (profile, code, purchases,
	/// feedback, blocks, its place in game results and the sign-in itself -
	/// see deleteAccount in functions/index.js), then its data on this phone,
	/// then signs out. Null when it worked, otherwise why not.
	/// </summary>
	public async Task<string?> DeleteAccountAsync()
	{
		var accountId = _auth.AccountId;
		try
		{
			await _functions.CallAsync("deleteAccount", new System.Text.Json.Nodes.JsonObject());
		}
		catch (Exception ex) when (ex is StoreException or HttpRequestException or TaskCanceledException)
		{
			return ex is StoreException ? ex.Message : "No connection - deleting your account needs internet.";
		}
		await _local.DeleteAccountDataAsync(accountId);
		// Cached profile, purchases and settings of this phone.
		Preferences.Default.Clear();
		await _auth.SignOutAsync();
		return null;
	}

	/// <summary>App reacts to the sign-out and returns to LoginPage.</summary>
	[RelayCommand]
	private Task SignOutAsync() => _auth.SignOutAsync();
}
