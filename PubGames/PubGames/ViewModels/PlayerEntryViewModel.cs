using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

public partial class PlayerEntryViewModel : ObservableObject
{
	private readonly ILocalDatabaseService _local;

	// TODO: replace with your real logged-in account id (from auth state).
	private const string CurrentAccountId = "local-account";

	public ObservableCollection<Player> SelectedPlayers { get; } = new();
	public ObservableCollection<Player> SavedPlayers { get; } = new();

	[ObservableProperty]
	private string newPlayerName = string.Empty;

	public PlayerEntryViewModel(ILocalDatabaseService local)
	{
		_local = local;
	}

	public async Task LoadAsync()
	{
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
			AvatarInitials = InitialsFrom(NewPlayerName)
		};

		await _local.SavePlayerAsync(player);
		SavedPlayers.Add(player);
		SelectedPlayers.Add(player);
		NewPlayerName = string.Empty;
	}

	[RelayCommand]
	private void RemoveSelectedPlayer(Player player) => SelectedPlayers.Remove(player);

	private static string InitialsFrom(string name)
	{
		var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
		return parts.Length >= 2
			? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
			: name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
	}
}
