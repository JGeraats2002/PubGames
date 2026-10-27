using System.Net.Http.Json;
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

public partial class StoreService(FirestoreClient firestore, IAuthService auth) : IStoreService
{
	private const string FunctionsUrl = $"https://europe-west1-{AuthConfig.FirebaseProjectId}.cloudfunctions.net";

	private readonly HttpClient _http = new();

	public Entitlements Current { get; private set; } = Entitlements.None;

	private string? _loadedFor;

	private string CacheKey => $"entitlements:{auth.AccountId}";

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

	private static Entitlements Parse(Dictionary<string, object?>? fields)
	{
		if (fields is null) return Entitlements.None;
		var games = (fields.GetValueOrDefault("games") as List<object?> ?? []).OfType<string>().ToHashSet();
		return new Entitlements(games, fields.GetValueOrDefault("passUntil") as DateTime?,
			fields.GetValueOrDefault("passPlan") as string, fields.GetValueOrDefault("passRenews") as bool? ?? false);
	}

	private sealed record Cached(List<string> Games, DateTime? PassUntil, string? PassPlan, bool PassRenews);

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

	private void WriteCache(Entitlements e) =>
		Preferences.Default.Set(CacheKey, JsonSerializer.Serialize(new Cached(e.Games.ToList(), e.PassUntil, e.PassPlan, e.PassRenews)));

	/// <summary>
	/// Calls one of the server's functions (a Firebase "callable"): signed in
	/// as the user, so the server knows whose purchase it is.
	/// </summary>
	private async Task<JsonNode?> CallAsync(string function, JsonObject data)
	{
		var token = await auth.GetIdTokenAsync() ?? throw new StoreException("Sign in first.");
		HttpResponseMessage? response = null;
		// Very busy (every server spot taken): wait a moment and try once more.
		for (var attempt = 0; attempt < 2; attempt++)
		{
			response?.Dispose();
			using var request = new HttpRequestMessage(HttpMethod.Post, $"{FunctionsUrl}/{function}")
			{
				Content = JsonContent.Create(new JsonObject { ["data"] = data.DeepClone() })
			};
			request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
			response = await _http.SendAsync(request);
			if (response.StatusCode is not (System.Net.HttpStatusCode.TooManyRequests or System.Net.HttpStatusCode.ServiceUnavailable)) break;
			await Task.Delay(TimeSpan.FromSeconds(2));
		}
		using var _ = response;
		var body = JsonNode.Parse(await response!.Content.ReadAsStringAsync());
		if (body?["error"] is { } error)
			throw new StoreException(error["message"]?.GetValue<string>() ?? "The purchase couldn't be checked.");
		if (!response.IsSuccessStatusCode)
			throw new StoreException("The purchase couldn't be checked right now.");
		return body?["result"];
	}

	/// <summary>The server checks a purchase and records it; then this phone reads the result.</summary>
	private async Task VerifyAsync(string kind, string purchaseToken, string? gameId)
	{
		var data = new JsonObject { ["kind"] = kind, ["purchaseToken"] = purchaseToken };
		if (gameId is not null) data["gameId"] = gameId;
		await CallAsync("verifyPurchase", data);
	}

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

	public Task<(string Code, DateTime ValidUntil)?> FriendCodeAsync() => FriendCodeFromAsync("todaysCode");

	public Task<(string Code, DateTime ValidUntil)?> RenewFriendCodeAsync() => FriendCodeFromAsync("renewCode");

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

	private static string Problem(Exception ex) => ex switch
	{
		HttpRequestException => "No connection - try again when you're online. Nothing was charged twice: Restore purchases picks it up later.",
		StoreException store => store.Message,
		_ => ex.Message
	};

#if !ANDROID
	public Task<string> GamePriceAsync(PubGame game) => Task.FromResult(Prices.Format(game.Price));

	public Task<List<PassPlanPrice>> PassPricesAsync() =>
		Task.FromResult(Prices.PassPlans.Select(p => new PassPlanPrice(p, Prices.Format(p.Price))).ToList());

	public Task<(PurchaseOutcome Outcome, string? Problem)> BuyGameAsync(PubGame game) =>
		Task.FromResult<(PurchaseOutcome, string?)>((PurchaseOutcome.Failed, "Buying is only possible in the Android app."));

	public Task<(PurchaseOutcome Outcome, string? Problem)> BuyPassAsync(PassPlan plan) =>
		Task.FromResult<(PurchaseOutcome, string?)>((PurchaseOutcome.Failed, "Buying is only possible in the Android app."));

	public Task<Entitlements> RestoreAsync(IEnumerable<PubGame> games) => RefreshAsync();
#endif
}

/// <summary>A purchase that couldn't be completed or checked, with a message for the player.</summary>
public class StoreException(string message) : Exception(message);
