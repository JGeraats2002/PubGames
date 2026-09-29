using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>The admin library: every game including drafts, for admins and moderators to open for editing or deleting.</summary>
public partial class AdminLibraryViewModel : ObservableObject
{
	private readonly ILocalDatabaseService _local;
	private readonly ICloudSyncService _cloud;
	private readonly ITeamService _team;

	public ObservableCollection<PubGame> Games { get; } = new();

	[ObservableProperty]
	private string syncMessage = string.Empty;

	public AdminLibraryViewModel(ILocalDatabaseService local, ICloudSyncService cloud, ITeamService team)
	{
		_local = local;
		_cloud = cloud;
		_team = team;
	}

	public async Task LoadAsync()
	{
		await ShowLocalAsync();

		try
		{
			// Only team members may read drafts and pending games from the cloud (see firestore.rules).
			if (_team.Me is not null)
				await _cloud.PullAllGamesAsync();
			else
				await _cloud.PullPublishedGamesAsync();
			SyncMessage = string.Empty;
		}
		catch (Exception ex)
		{
			SyncMessage = ex is HttpRequestException
				? "Offline - showing the games saved on this phone."
				: $"Couldn't refresh games: {ex.Message}";
			return;
		}

		await ShowLocalAsync();
	}

	private async Task ShowLocalAsync()
	{
		Games.Clear();
		foreach (var game in await _local.GetManageableGamesAsync())
			Games.Add(game);
	}
}
