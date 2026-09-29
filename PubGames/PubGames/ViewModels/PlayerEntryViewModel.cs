using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

public partial class PlayerEntryViewModel : ObservableObject
{
	private readonly ILocalDatabaseService _local;
	private readonly IAuthService _auth;

	private string CurrentAccountId => _auth.AccountId;

	public ObservableCollection<Player> SelectedPlayers { get; } = new();
	public ObservableCollection<Player> SavedPlayers { get; } = new();

	[ObservableProperty]
	private string newPlayerName = string.Empty;

	public PlayerEntryViewModel(ILocalDatabaseService local, IAuthService auth)
	{
		_local = local;
		_auth = auth;
	}

	public async Task LoadAsync()
	{
		await _auth.InitializeAsync();
		var saved = await _local.GetPlayersAsync(CurrentAccountId);
		SavedPlayers.Clear();
		foreach (var p in saved)
			SavedPlayers.Add(p);
	}

	[RelayCommand]
	private void AddSavedPlayer(Player player)
	{
		if (!SelectedPlayers.Any(p => p.Id == player.Id))
			SelectedPlayers.Add(player);
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
		SavedPlayers.Add(player);
		SelectedPlayers.Add(player);
		NewPlayerName = string.Empty;
	}

	[RelayCommand]
	private void RemoveSelectedPlayer(Player player) => SelectedPlayers.Remove(player);
}
