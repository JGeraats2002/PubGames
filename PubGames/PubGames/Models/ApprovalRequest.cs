using SQLite;

namespace PubGames.Models;

public enum ApprovalRequestType
{
	PublishGame,
	DeleteGame
}

public enum ApprovalRequestStatus
{
	Pending,
	Approved,
	Denied
}

/// <summary>
/// Covers both "publish this new game" and "delete this game" requests from
/// a moderator who lacks the matching permission. The game itself is not
/// blocked while pending: a publish-pending game just isn't in the public
/// library yet, and a delete-pending game stays fully playable with a
/// warning badge until an admin resolves the request. Stored in the cloud
/// "approvals" collection so it reaches every admin's phone.
/// </summary>
public class ApprovalRequest
{
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	[Indexed]
	public string GameId { get; set; } = string.Empty;

	[Indexed]
	public string TeamId { get; set; } = string.Empty;

	/// <summary>Shown on the admin's approval card so they don't have to look the game up.</summary>
	public string GameName { get; set; } = string.Empty;

	public ApprovalRequestType Type { get; set; }

	public string RequestedByUserId { get; set; } = string.Empty;

	/// <summary>Checked by firestore.rules: a moderator can only file requests in their own name.</summary>
	public string RequestedByEmail { get; set; } = string.Empty;

	public string RequestedByName { get; set; } = string.Empty;

	[Ignore]
	public string Summary => $"{(string.IsNullOrEmpty(RequestedByName) ? RequestedByEmail : RequestedByName)} wants to {(Type == ApprovalRequestType.DeleteGame ? "delete" : "publish")} \"{GameName}\"";

	public ApprovalRequestStatus Status { get; set; } = ApprovalRequestStatus.Pending;

	public string? ResolvedByUserId { get; set; }

	public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

	public DateTime? ResolvedAt { get; set; }
}

/// <summary>
/// A one-time unlock of a paid game, tied to the account (not the device),
/// so it's valid on any device the person logs into. Written server-side
/// only after Google Play Billing confirms the purchase - never trust a
/// client-only "unlocked" flag.
/// </summary>
public class Purchase
{
	[PrimaryKey]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	[Indexed]
	public string AccountId { get; set; } = string.Empty;

	[Indexed]
	public string GameId { get; set; } = string.Empty;

	public decimal PricePaid { get; set; }

	/// <summary>Google Play Billing purchase token, kept for server-side receipt verification.</summary>
	public string TransactionId { get; set; } = string.Empty;

	public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
}
