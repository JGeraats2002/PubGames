using System.Text.Json;
using System.Text.Json.Nodes;
using PubGames.Models;

namespace PubGames.Services;

/// <summary>How a purchase went.</summary>
public enum PurchaseOutcome
{
	Bought,
	Cancelled,

	/// <summary>Paid with a method that takes a while (like a bank transfer): unlocked once Google confirms it.</summary>
	Pending,

	Failed
}

/// <summary>A pass plan with its price as Google Play shows it to this player.</summary>
public sealed record PassPlanPrice(PassPlan Plan, string Price);

/// <summary>
/// Buying games and the Pub Games Pass with Google Play Billing. The app
/// never decides by itself that something is bought: Google Play's purchase
/// is checked by the server (functions/index.js), which records it in
/// entitlements/{uid}. This service reads that, keeps a copy on the phone for
/// offline use, and refreshes the pass with the server once it has run out.
/// </summary>
public interface IStoreService
{
	/// <summary>What the account owns as last seen (also offline).</summary>
	Entitlements Current { get; }

	/// <summary>Fresh from the server when online; otherwise the copy on this phone.</summary>
	Task<Entitlements> RefreshAsync();

	bool Owns(PubGame game) => Current.Owns(game);

	/// <summary>The game's price as Google Play shows it, or the fixed price when Play can't be reached.</summary>
	Task<string> GamePriceAsync(PubGame game);

	/// <summary>The pass plans with their prices from Google Play (or the fixed prices).</summary>
	Task<List<PassPlanPrice>> PassPricesAsync();

	Task<(PurchaseOutcome Outcome, string? Problem)> BuyGameAsync(PubGame game);

	Task<(PurchaseOutcome Outcome, string? Problem)> BuyPassAsync(PassPlan plan);

	/// <summary>Checks every purchase Google Play knows for this account again (a new phone, or a check that failed). Returns what's owned now.</summary>
	Task<Entitlements> RestoreAsync(IEnumerable<PubGame> games);

	/// <summary>
	/// The owner's current friend code: unlocks the game for free - checked
	/// by the server. Only games, not the pass. Null when it worked, otherwise why not.
	/// </summary>
	Task<string?> RedeemCodeAsync(string code, PubGame game);

	/// <summary>The owner only: the friend code now ("K7QM-3XPA") and until when it works; null when it can't be fetched.</summary>
	Task<(string Code, DateTime ValidUntil)?> FriendCodeAsync();

	/// <summary>The owner only: a new friend code right away, working for 30 minutes; the old one stops working.</summary>
	Task<(string Code, DateTime ValidUntil)?> RenewFriendCodeAsync();
}

/// <inheritdoc />
public partial class StoreService(FirestoreClient firestore, IAuthService auth, FunctionsClient functions) : IStoreService
{
	/// <inheritdoc />
	public Entitlements Current { get; private set; } = Entitlements.None;

	private string? _loadedFor;

	private string CacheKey => $"entitlements:{auth.AccountId}";

	/// <inheritdoc />
	public async Task<Entitlements> RefreshAsync()
	{
		if (auth.CurrentUser is not { } user) return Current = Entitlements.None;
		if (_loadedFor != user.Uid)
		{
			Current = ReadCache();
			_loadedFor = user.Uid;
		}
		try
		{
			var fresh = Parse(await firestore.GetAsync("entitlements", user.Uid));
			// The pass ran out by the server's last check: perhaps Google renewed it since.
			if (!fresh.HasPass && fresh.PassUntil is not null && fresh.PassRenews)
			{
				await CallAsync("refreshPass", new JsonObject());
				fresh = Parse(await firestore.GetAsync("entitlements", user.Uid));
			}
			Current = fresh;
			WriteCache(fresh);
		}
		catch (Exception ex) when (ex is HttpRequestException or CloudException or StoreException or TaskCanceledException)
		{
			// Offline: what this phone saw last.
		}
		return Current;
	}

	/// <summary>
	/// What an account owns, read from its entitlements document (none when there isn't one yet).
	/// </summary>
	private static Entitlements Parse(Dictionary<string, object?>? fields)
	{
		if (fields is null) return Entitlements.None;
		var games = (fields.GetValueOrDefault("games") as List<object?> ?? []).OfType<string>().ToHashSet();
		return new Entitlements(games, fields.GetValueOrDefault("passUntil") as DateTime?,
			fields.GetValueOrDefault("passPlan") as string, fields.GetValueOrDefault("passRenews") as bool? ?? false);
	}

	/// <summary>What an account owns, as saved on the phone for offline use.</summary>
	private sealed record Cached(List<string> Games, DateTime? PassUntil, string? PassPlan, bool PassRenews);

	/// <summary>What the account owned when this phone last checked.</summary>
	private Entitlements ReadCache()
	{
		try
		{
			var json = Preferences.Default.Get(CacheKey, string.Empty);
			if (json.Length == 0 || JsonSerializer.Deserialize<Cached>(json) is not { } c) return Entitlements.None;
			return new Entitlements(c.Games.ToHashSet(), c.PassUntil, c.PassPlan, c.PassRenews);
		}
		catch (JsonException)
		{
			return Entitlements.None;
		}
	}

	/// <summary>Saves what the account owns on this phone, for offline use.</summary>
	private void WriteCache(Entitlements e) =>
		Preferences.Default.Set(CacheKey, JsonSerializer.Serialize(new Cached(e.Games.ToList(), e.PassUntil, e.PassPlan, e.PassRenews)));

	/// <summary>Calls a server function as the signed-in user (see FunctionsClient).</summary>
	private Task<JsonNode?> CallAsync(string function, JsonObject data) => functions.CallAsync(function, data);

	/// <summary>The server checks a purchase and records it; then this phone reads the result.</summary>
	private async Task VerifyAsync(string kind, string purchaseToken, string? gameId)
	{
		var data = new JsonObject { ["kind"] = kind, ["purchaseToken"] = purchaseToken };
		if (gameId is not null) data["gameId"] = gameId;
		await CallAsync("verifyPurchase", data);
	}

	/// <inheritdoc />
	public async Task<string?> RedeemCodeAsync(string code, PubGame game)
	{
		var data = new JsonObject { ["code"] = code.Trim(), ["gameId"] = game.Id };
		try
		{
			await CallAsync("redeemCode", data);
		}
		catch (Exception ex) when (ex is StoreException or HttpRequestException)
		{
			return ex is HttpRequestException ? "No connection - a code needs internet." : ex.Message;
		}
		await RefreshAsync();
		return null;
	}

	/// <inheritdoc />
	public Task<(string Code, DateTime ValidUntil)?> FriendCodeAsync() => FriendCodeFromAsync("todaysCode");

	/// <inheritdoc />
	public Task<(string Code, DateTime ValidUntil)?> RenewFriendCodeAsync() => FriendCodeFromAsync("renewCode");

	/// <summary>
	/// The owner's friend code from the server function `function` (todaysCode or renewCode), with until when
	/// it works.
	/// </summary>
	private async Task<(string Code, DateTime ValidUntil)?> FriendCodeFromAsync(string function)
	{
		try
		{
			var result = await CallAsync(function, new JsonObject());
			if (result?["code"]?.GetValue<string>() is not { } code
				|| !DateTime.TryParse(result["validUntil"]?.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture,
					System.Globalization.DateTimeStyles.AdjustToUniversal, out var until))
				return null;
			return (code, until);
		}
		catch (Exception ex) when (ex is StoreException or HttpRequestException or JsonException)
		{
			return null;
		}
	}

	/// <summary>A message the player understands for what went wrong with a purchase.</summary>
	private static string Problem(Exception ex) => ex switch
	{
		HttpRequestException => "No connection - try again when you're online. Nothing was charged twice: Restore purchases picks it up later.",
		StoreException store => store.Message,
		_ => ex.Message
	};

#if !ANDROID
	/// <inheritdoc />
	public Task<string> GamePriceAsync(PubGame game) => Task.FromResult(Prices.Format(game.Price));

	/// <inheritdoc />
	public Task<List<PassPlanPrice>> PassPricesAsync() =>
		Task.FromResult(Prices.PassPlans.Select(p => new PassPlanPrice(p, Prices.Format(p.Price))).ToList());

	/// <inheritdoc />
	public Task<(PurchaseOutcome Outcome, string? Problem)> BuyGameAsync(PubGame game) =>
		Task.FromResult<(PurchaseOutcome, string?)>((PurchaseOutcome.Failed, "Buying is only possible in the Android app."));

	/// <inheritdoc />
	public Task<(PurchaseOutcome Outcome, string? Problem)> BuyPassAsync(PassPlan plan) =>
		Task.FromResult<(PurchaseOutcome, string?)>((PurchaseOutcome.Failed, "Buying is only possible in the Android app."));

	/// <inheritdoc />
	public Task<Entitlements> RestoreAsync(IEnumerable<PubGame> games) => RefreshAsync();
#endif
}

/// <summary>A purchase that couldn't be completed or checked, with a message for the player.</summary>
public class StoreException(string message) : Exception(message);
