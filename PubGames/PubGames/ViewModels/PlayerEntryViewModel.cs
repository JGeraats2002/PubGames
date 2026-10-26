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

	private string CurrentAccountId => _auth.AccountId;

	/// <summary>Who's playing, in seat order. Tapping one takes them out again.</summary>
	public ObservableCollection<Player> SelectedPlayers { get; } = new();

	/// <summary>Saved players who aren't playing yet: only who's left to choose from.</summary>
	public ObservableCollection<Player> SavedPlayers { get; } = new();

	/// <summary>Every saved player of the account, playing or not.</summary>
	private List<Player> _allSaved = new();

	[ObservableProperty]
	private bool hasSavedPlayers;

	[ObservableProperty]
	private string newPlayerName = string.Empty;

	/// <summary>Someone's personal code, to add them as a linked player.</summary>
	[ObservableProperty]
	private string playerCode = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasStatusMessage))]
	private string statusMessage = string.Empty;

	public bool HasStatusMessage => StatusMessage.Length > 0;

	private readonly SavedGames _savedGames;

	/// <summary>How many games are saved, for the button to the Saved games page.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SavedGamesButtonText))]
	private int savedGameCount;

	public string SavedGamesButtonText => SavedGameCount == 0 ? "Saved games ›" : $"Saved games ({SavedGameCount}) ›";

	public PlayerEntryViewModel(ILocalDatabaseService local, IAuthService auth, GameNight gameNight, IProfileService profiles, SavedGames savedGames)
	{
		_savedGames = savedGames;
		_local = local;
		_auth = auth;
		_gameNight = gameNight;
		_profiles = profiles;
	}

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
			foreach (var p in lineup)
				SelectedPlayers.Add(p);
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
		foreach (var p in selected)
			SelectedPlayers.Add(p);
		ShowSavedPlayers();
	}

	/// <summary>Adds the person with this personal code: to this game, and to the saved players.</summary>
	[RelayCommand]
	private async Task AddByCodeAsync()
	{
		if (UserProfile.NormalizeCode(PlayerCode) is not { } code)
		{
			StatusMessage = $"A personal code has {UserProfile.CodeLength} letters and numbers, e.g. AB3K-9QX7.";
			return;
		}

		StatusMessage = "Looking up the code...";
		UserProfile? profile;
		try
		{
			profile = await _profiles.FindByCodeAsync(code);
		}
		catch (Exception ex) when (ex is HttpRequestException or CloudException)
		{
			StatusMessage = ex is HttpRequestException ? "No connection - adding by code needs internet." : ex.Message;
			return;
		}
		if (profile is null)
		{
			StatusMessage = $"Nobody has the code {code}. Check it with them.";
			return;
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
	/// </summary>
	public async Task<bool> DeleteSavedPlayerAsync(Player player)
	{
		if (player.LinkedAccountId == CurrentAccountId)
		{
			StatusMessage = "You can't delete yourself from the saved players.";
			return false;
		}

		player.IsTemporary = true;
		await _local.SavePlayerAsync(player);
		_allSaved.Remove(player);
		StatusMessage = string.Empty;
		ShowSavedPlayers();
		return true;
	}

	/// <summary>The saved players minus who's already playing, alphabetical.</summary>
	private void ShowSavedPlayers()
	{
		var playing = SelectedPlayers.Select(p => p.Id).ToHashSet();
		SavedPlayers.Clear();
		foreach (var p in _allSaved.Where(p => !playing.Contains(p.Id)).OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
			SavedPlayers.Add(p);
		HasSavedPlayers = SavedPlayers.Count > 0;
	}

	[RelayCommand]
	private void AddSavedPlayer(Player player)
	{
		if (!SelectedPlayers.Any(p => p.Id == player.Id))
			SelectedPlayers.Add(player);
		ShowSavedPlayers();
	}

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
