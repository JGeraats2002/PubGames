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

	/// <summary>The requests waiting for a decision.</summary>
	public ObservableCollection<ReviewRequest> Requests { get; } = new();

	/// <summary>About refreshing from the cloud: offline, or what went wrong.</summary>
	[ObservableProperty]
	private string syncMessage = string.Empty;

	/// <summary>
	/// Gets the services it works with - the phone's database and cloud sync - from the app (dependency
	/// injection).
	/// </summary>
	public ReviewRequestsViewModel(ILocalDatabaseService local, ICloudSyncService cloud)
	{
		_local = local;
		_cloud = cloud;
	}

	/// <summary>
	/// Shows the requests waiting for review as saved on the phone, then refreshes them from the cloud.
	/// </summary>
	public async Task LoadAsync()
	{
		await ShowAsync(await _local.GetRequestsWaitingForReviewAsync(CurrentTeamId));
		try
		{
			await ShowAsync(await _cloud.PullRequestsWaitingForReviewAsync(CurrentTeamId));
			SyncMessage = string.Empty;
		}
		catch (Exception ex)
		{
			SyncMessage = ex is HttpRequestException
				? L.T("Offline - showing the requests saved on this phone.")
				: L.F("Couldn't refresh requests: {0}", ex.Message);
		}
	}

	/// <summary>Shows the requests, with the flags of the languages each one touches.</summary>
	private async Task ShowAsync(IEnumerable<ReviewRequest> requests)
	{
		var list = requests.ToList();
		foreach (var request in list.Where(r => r.HasGameProposal))
		{
			var current = request.Type == ReviewRequestType.GameChanges ? await _local.GetGameAsync(request.GameId) : null;
			var proposed = request.ProposedGame(current);
			request.TouchedLanguages = TranslationChange.Between(current, proposed)
				.Select(c => c.Language).Distinct().ToList();
			request.ShownSubjectName = proposed.ShownName;
		}
		Requests.Clear();
		foreach (var request in list)
			Requests.Add(request);
	}
}
