using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>The host's own game list: every game including drafts, to open for editing or deleting.</summary>
public partial class HostLibraryViewModel : ObservableObject
{
	private readonly ILocalDatabaseService _local;
	private readonly ICloudSyncService _cloud;
	private readonly IAuthService _auth;

	public ObservableCollection<PubGame> Games { get; } = new();

	[ObservableProperty]
	private string syncMessage = string.Empty;

	public HostLibraryViewModel(ILocalDatabaseService local, ICloudSyncService cloud, IAuthService auth)
	{
		_local = local;
		_cloud = cloud;
		_auth = auth;
	}

	public async Task LoadAsync()
	{
		await ShowLocalAsync();

		try
		{
			// Only admins may read drafts from the cloud; other hosts see the public library plus their own local drafts.
			if (_auth.CurrentUser?.IsAdmin == true)
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
