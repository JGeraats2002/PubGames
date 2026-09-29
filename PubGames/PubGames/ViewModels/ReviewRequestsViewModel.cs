using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>Admins: every review request from moderators that still needs a decision, oldest first.</summary>
public partial class ReviewRequestsViewModel : ObservableObject
{
	private readonly ILocalDatabaseService _local;
	private readonly ICloudSyncService _cloud;

	// TODO: replace with the team the user picked once multiple teams exist.
	private const string CurrentTeamId = AuthService.DefaultTeamId;

	public ObservableCollection<ReviewRequest> Requests { get; } = new();

	[ObservableProperty]
	private string syncMessage = string.Empty;

	public ReviewRequestsViewModel(ILocalDatabaseService local, ICloudSyncService cloud)
	{
		_local = local;
		_cloud = cloud;
	}

	public async Task LoadAsync()
	{
		Show(await _local.GetRequestsWaitingForReviewAsync(CurrentTeamId));
		try
		{
			Show(await _cloud.PullRequestsWaitingForReviewAsync(CurrentTeamId));
			SyncMessage = string.Empty;
		}
		catch (Exception ex)
		{
			SyncMessage = ex is HttpRequestException
				? "Offline - showing the requests saved on this phone."
				: $"Couldn't refresh requests: {ex.Message}";
		}
	}

	private void Show(IEnumerable<ReviewRequest> requests)
	{
		Requests.Clear();
		foreach (var r in requests)
			Requests.Add(r);
	}
}
