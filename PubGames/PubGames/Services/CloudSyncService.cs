using PubGames.Models;

namespace PubGames.Services;

public interface ICloudSyncService
{
	/// <summary>Pull anything the account owns (players, purchases, sessions) from the cloud into local storage.</summary>
	Task PullAccountDataAsync(string accountId);

	/// <summary>Push local, not-yet-synced sessions/scores up to the cloud.</summary>
	Task PushPendingSessionsAsync();

	/// <summary>
	/// Verifies a Google Play Billing purchase token against your backend, and
	/// only THEN writes the purchase record - the backend is the source of
	/// truth for "owned", never the device alone.
	/// </summary>
	Task<bool> VerifyAndRecordPurchaseAsync(string accountId, string gameId, string playBillingToken);

	Task SubmitApprovalRequestAsync(ApprovalRequest request);
	Task ResolveApprovalRequestAsync(string requestId, bool approved, string resolvedByUserId);
}

/// <summary>
/// Placeholder implementation. Replace the bodies below with real HTTP calls
/// (or Firebase SDK calls) to your backend once it exists. Kept as a stub so
/// the rest of the app - and the offline-first local flow - works and is
/// testable before the backend is built.
/// </summary>
public class CloudSyncService : ICloudSyncService
{
	private readonly ILocalDatabaseService _local;
	private readonly HttpClient _http;

	// TODO: point this at your real API once it's deployed.
	private const string ApiBaseUrl = "https://api.yourdomain.com/";

	public CloudSyncService(ILocalDatabaseService local)
	{
		_local = local;
		_http = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };
	}

	public Task PullAccountDataAsync(string accountId)
	{
		// TODO: GET /accounts/{accountId}/players, /purchases, /sessions
		// and upsert results into ILocalDatabaseService.
		return Task.CompletedTask;
	}

	public Task PushPendingSessionsAsync()
	{
		// TODO: query local sessions where IsSynced == false, POST each to
		// the backend, then mark IsSynced = true locally on success.
		return Task.CompletedTask;
	}

	public Task<bool> VerifyAndRecordPurchaseAsync(string accountId, string gameId, string playBillingToken)
	{
		// TODO: POST { accountId, gameId, playBillingToken } to your backend.
		// Backend calls the Google Play Developer API to verify the token
		// server-side, then inserts the Purchase row and returns success.
		return Task.FromResult(false);
	}

	public Task SubmitApprovalRequestAsync(ApprovalRequest request)
	{
		// TODO: POST the request; backend should push a notification to every
		// head host on the org (the "Sanne wants to delete Kings cup" card).
		return _local.SaveApprovalRequestAsync(request);
	}

	public Task ResolveApprovalRequestAsync(string requestId, bool approved, string resolvedByUserId)
	{
		// TODO: PATCH the request status server-side, and if approved, apply
		// the underlying action (publish or delete the game) there too, so
		// it's consistent even if this device goes offline right after.
		return Task.CompletedTask;
	}
}
