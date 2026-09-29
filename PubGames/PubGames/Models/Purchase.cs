using SQLite;

namespace PubGames.Models;

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
