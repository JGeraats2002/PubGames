using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>
/// Step 1 of a game night: who's playing, in seat order. Players are typed-in
/// names, or people added by their personal code - those are linked to their
/// account, so the game's result counts in their stats. The user is a saved
/// player too (linked to their own account).
/// </summary>
public partial class PlayerEntryViewModel : ObservableObject
{
	private readonly ILocalDatabaseService _local;
	private readonly IAuthService _auth;
	private readonly GameNight _gameNight;
	private readonly IProfileService _profiles;
	private readonly IPlayerLinkService _links;

	private string CurrentAccountId => _auth.AccountId;

	/// <summary>Who's playing, in seat order. Tapping one takes them out again.</summary>
	public ObservableCollection<Player> SelectedPlayers { get; } = new();

	/// <summary>Saved players who aren't playing yet: only who's left to choose from.</summary>
	public ObservableCollection<Player> SavedPlayers { get; } = new();

	/// <summary>Every saved player of the account, playing or not.</summary>
	private List<Player> _allSaved = new();

	/// <summary>Whether there are saved players left to choose from.</summary>
	[ObservableProperty]
	private bool hasSavedPlayers;

	/// <summary>The name of a new player, as typed.</summary>
	[ObservableProperty]
	private string newPlayerName = string.Empty;

	/// <summary>Someone's personal code, to add them as a linked player.</summary>
	[ObservableProperty]
	private string playerCode = string.Empty;

	/// <summary>What happened (a player removed or blocked) or why something can't.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasStatusMessage))]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	/// <summary>Whether there's a message to show.</summary>
	public bool HasStatusMessage => StatusMessage.Length > 0;

	private readonly SavedGames _savedGames;

	/// <summary>How many games are saved, for the button to the Saved games page.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SavedGamesButtonText))]
	private int savedGameCount;

	/// <summary>"Saved games ›", with how many there are.</summary>
	public string SavedGamesButtonText => SavedGameCount == 0 ? L.T("Saved games ›") : L.F("Saved games ({0}) ›", SavedGameCount);

	/// <summary>
	/// Gets the services it works with - the phone's database, the sign-in, the game night, profiles, saved
	/// games and player links - from the app (dependency injection).
	/// </summary>
	public PlayerEntryViewModel(ILocalDatabaseService local, IAuthService auth, GameNight gameNight, IProfileService profiles, SavedGames savedGames, IPlayerLinkService links)
	{
		_links = links;
		_savedGames = savedGames;
		_local = local;
		_auth = auth;
		_gameNight = gameNight;
		_profiles = profiles;
	}

	/// <summary>
	/// Shows the saved players, then (online) brings linked players' names and pictures up to date and takes
	/// away players who removed the link or blocked the user.
	/// </summary>
	public async Task LoadAsync()
	{
		await _auth.InitializeAsync();
		_allSaved = await _local.GetPlayersAsync(CurrentAccountId);

		// No game is open while this page shows, so old and empty games can be cleaned up now.
		SavedGameCount = (await _savedGames.GetAsync()).Count;

		// Back from a finished game: the players who were playing at the end, in seat order.
		if (_gameNight.NextLineup is { } lineup)
		{
			_gameNight.NextLineup = null;
			SelectedPlayers.Clear();
			foreach (var player in lineup)
				SelectedPlayers.Add(player);
		}
		ShowSavedPlayers();

		// Then, when online: the user as a saved player, and linked players' latest pictures and nicknames.
		try
		{
			if (await _profiles.LoadMineAsync() is { } me)
				await SaveLinkedAsync(me, isMe: true);
			foreach (var player in _allSaved.Where(p => p.IsLinked && p.LinkedAccountId != CurrentAccountId).ToList())
				if (await _profiles.GetAsync(player.LinkedAccountId!) is { } profile)
					await SaveLinkedAsync(profile, isMe: false);
		}
		catch (Exception ex) when (ex is HttpRequestException or CloudException or InvalidOperationException)
		{
			// Offline: the saved copies will do.
		}
		try
		{
			// Who removed the link both ways, or blocked the user: their saved player goes.
			var removed = await _links.TakeRemovedLinksAsync();
			foreach (var player in _allSaved.Where(p => p.LinkedAccountId is { } id && id != CurrentAccountId && removed.Contains(id)).ToList())
				await HideAsync(player);
		}
		catch (Exception ex) when (ex is HttpRequestException or CloudException or InvalidOperationException)
		{
			// Offline: checked again next time.
		}
		RefreshLists();
	}

	/// <summary>
	/// The saved player for this account, made or brought up to date from
	/// their profile: the picture always, the name unless the user gave them
	/// their own name for them.
	/// </summary>
	private async Task<Player> SaveLinkedAsync(UserProfile profile, bool isMe)
	{
		var player = _allSaved.FirstOrDefault(p => p.LinkedAccountId == profile.Uid);
		if (player is null)
		{
			player = new Player { OwnerAccountId = CurrentAccountId, LinkedAccountId = profile.Uid };
			_allSaved.Add(player);
		}
		else if (player.PhotoRef == profile.PhotoRef && (player.HasCustomName || player.Name == profile.DisplayName))
			return player; // nothing new

		player.PhotoRef = profile.PhotoRef;
		if (!player.HasCustomName || isMe)
		{
			player.Name = profile.DisplayName;
			player.AvatarInitials = Player.InitialsFrom(profile.DisplayName);
		}
		await _local.SavePlayerAsync(player);
		return player;
	}

	/// <summary>Players are plain objects: show changed names and pictures by filling the lists again.</summary>
	private void RefreshLists()
	{
		var byId = _allSaved.ToDictionary(p => p.Id);
		var selected = SelectedPlayers.Select(p => byId.GetValueOrDefault(p.Id) ?? p).ToList();
		SelectedPlayers.Clear();
		foreach (var player in selected)
			SelectedPlayers.Add(player);
		ShowSavedPlayers();
	}

	/// <summary>Adds the person with this personal code: to this game, and to the saved players.</summary>
	[RelayCommand]
	private async Task AddByCodeAsync()
	{
		if (UserProfile.NormalizeCode(PlayerCode) is not { } code)
		{
			StatusMessage = L.F("A personal code has {0} letters and numbers, e.g. AB3K-9QX7.", UserProfile.CodeLength);
			return;
		}

		StatusMessage = L.T("Looking up the code...");
		UserProfile? profile;
		try
		{
			profile = await _profiles.FindByCodeAsync(code);
		}
		catch (Exception ex) when (ex is HttpRequestException or CloudException)
		{
			StatusMessage = ex is HttpRequestException ? L.T("No connection - adding by code needs internet.") : ex.Message;
			return;
		}
		if (profile is null)
		{
			StatusMessage = L.F("Nobody has the code {0}. Check it with them.", code);
			return;
		}
		if (profile.Uid != CurrentAccountId)
		{
			try
			{
				if ((await _links.GetBlockedAsync()).Any(b => b.Uid == profile.Uid))
				{
					StatusMessage = L.T("You blocked this player. Unblock them under Account > Blocked players to add them again.");
					return;
				}
				if (await _links.IsBlockedByAsync(profile.Uid))
				{
					StatusMessage = L.T("This player can't be added.");
					return;
				}
			}
			catch (Exception ex) when (ex is HttpRequestException or CloudException)
			{
				StatusMessage = ex is HttpRequestException ? L.T("No connection - adding by code needs internet.") : ex.Message;
				return;
			}
		}

		var player = await SaveLinkedAsync(profile, isMe: profile.Uid == CurrentAccountId);
		if (!SelectedPlayers.Any(p => p.Id == player.Id))
			SelectedPlayers.Add(player);
		PlayerCode = string.Empty;
		StatusMessage = string.Empty;
		RefreshLists();
	}

	/// <summary>
	/// Your own name for a player, on this phone only. For a linked player it
	/// stays, even when they change their nickname.
	/// </summary>
	public async Task RenameAsync(Player player, string name)
	{
		name = name.Trim();
		if (name.Length == 0 || name == player.Name) return;

		player.Name = name;
		player.AvatarInitials = Player.InitialsFrom(name);
		player.HasCustomName = player.IsLinked;
		await _local.SavePlayerAsync(player);
		RefreshLists();
	}

	/// <summary>
	/// Removes a player from the saved players. They're only hidden, not
	/// erased, so earlier games still show their name. You can't delete
	/// yourself: you'd come straight back as the player linked to your account.
	/// A player added by code can also lose the link both ways (they lose you
	/// from their saved players too), or be blocked (then they also can't add
	/// you by your code any more).
	/// </summary>
	public async Task<bool> DeleteSavedPlayerAsync(Player player, PlayerRemoval removal = PlayerRemoval.OnlyHere)
	{
		if (player.LinkedAccountId == CurrentAccountId)
		{
			StatusMessage = L.T("You can't delete yourself from the saved players.");
			return false;
		}

		if (removal != PlayerRemoval.OnlyHere && player.LinkedAccountId is { } other)
		{
			try
			{
				if (removal == PlayerRemoval.Block)
					await _links.BlockAsync(other, player.Name);
				else
					await _links.UnlinkBothWaysAsync(other);
			}
			catch (Exception ex) when (ex is HttpRequestException or CloudException or InvalidOperationException)
			{
				StatusMessage = ex is HttpRequestException ? L.T("No connection - removing the link both ways or blocking needs internet.") : ex.Message;
				return false;
			}
		}

		await HideAsync(player);
		StatusMessage = removal switch
		{
			PlayerRemoval.Block => L.F("{0} is removed and blocked.", player.Name),
			PlayerRemoval.BothWays => L.F("{0} is removed, and you'll be removed from their players.", player.Name),
			_ => string.Empty
		};
		return true;
	}

	/// <summary>Takes a saved player off the list (and out of this game).</summary>
	private async Task HideAsync(Player player)
	{
		player.IsTemporary = true;
		await _local.SavePlayerAsync(player);
		_allSaved.Remove(player);
		var playing = SelectedPlayers.FirstOrDefault(p => p.Id == player.Id);
		if (playing is not null)
			SelectedPlayers.Remove(playing);
		ShowSavedPlayers();
	}

	/// <summary>The saved players minus who's already playing, alphabetical.</summary>
	private void ShowSavedPlayers()
	{
		var playing = SelectedPlayers.Select(p => p.Id).ToHashSet();
		SavedPlayers.Clear();
		foreach (var player in _allSaved.Where(saved => !playing.Contains(saved.Id)).OrderBy(saved => saved.Name, StringComparer.CurrentCultureIgnoreCase))
			SavedPlayers.Add(player);
		HasSavedPlayers = SavedPlayers.Count > 0;
	}

	/// <summary>Adds a saved player to this game.</summary>
	[RelayCommand]
	private void AddSavedPlayer(Player player)
	{
		if (!SelectedPlayers.Any(p => p.Id == player.Id))
			SelectedPlayers.Add(player);
		ShowSavedPlayers();
	}

	/// <summary>Adds a new player by name: to this game and to the saved players.</summary>
	[RelayCommand]
	private async Task AddNewPlayerAsync()
	{
		if (string.IsNullOrWhiteSpace(NewPlayerName))
			return;

		var player = new Player
		{
			OwnerAccountId = CurrentAccountId,
			Name = NewPlayerName.Trim(),
			AvatarInitials = Player.InitialsFrom(NewPlayerName)
		};

		await _local.SavePlayerAsync(player);
		_allSaved.Add(player);
		SelectedPlayers.Add(player);
		NewPlayerName = string.Empty;
		ShowSavedPlayers();
	}

	/// <summary>Moves a player one seat up (-1) or down (+1); the order here is the seat order on the scoreboard.</summary>
	public void Move(Player player, int direction)
	{
		var from = SelectedPlayers.IndexOf(player);
		var to = from + direction;
		if (from < 0 || to < 0 || to >= SelectedPlayers.Count) return;
		SelectedPlayers.Move(from, to);
	}

	/// <summary>Takes a player out; a saved player shows up under Saved players again.</summary>
	[RelayCommand]
	private void RemoveSelectedPlayer(Player player)
	{
		SelectedPlayers.Remove(player);
		ShowSavedPlayers();
	}
}

/// <summary>How far deleting a saved player goes.</summary>
public enum PlayerRemoval
{
	/// <summary>Only off this phone's saved players.</summary>
	OnlyHere,

	/// <summary>Also off the other person's saved players (a player added by code).</summary>
	BothWays,

	/// <summary>Both ways, and they can't add you by code any more.</summary>
	Block
}
