using Android.BillingClient.Api;
using PubGames.Models;
using PlayPurchase = Android.BillingClient.Api.Purchase;

namespace PubGames.Services;

/// <summary>
/// The Google Play Billing part of StoreService: connecting to Play, the
/// prices Play shows, the purchase screen, and the purchases Play knows for
/// this account. Every purchase goes to the server to be checked before it
/// counts.
/// </summary>
public partial class StoreService
{
	private BillingClient? _client;

	/// <summary>The purchase screen that's open: its outcome arrives in OnPurchasesUpdated.</summary>
	private TaskCompletionSource<(BillingResult Result, IList<PlayPurchase>? Purchases)>? _flow;

	/// <summary>
	/// Receives Google Play's answer when the purchase screen closes, and hands it to the purchase that's
	/// waiting for it.
	/// </summary>
	private sealed class PurchasesListener(StoreService store) : Java.Lang.Object, IPurchasesUpdatedListener
	{
		/// <summary>The purchase screen closed: bought, cancelled or failed.</summary>
		public void OnPurchasesUpdated(BillingResult result, IList<PlayPurchase>? purchases) =>
			store._flow?.TrySetResult((result, purchases));
	}

	/// <summary>
	/// Connects to Google Play Billing (once; it reconnects by itself). Throws with a readable message when
	/// Play payments aren't available.
	/// </summary>
	private async Task<BillingClient> ConnectAsync()
	{
		if (_client is { IsReady: true } ready) return ready;
		_client ??= BillingClient.NewBuilder(Platform.AppContext)
			.SetListener(new PurchasesListener(this))
			.EnablePendingPurchases(PendingPurchasesParams.NewBuilder().EnableOneTimeProducts().Build())
			.EnableAutoServiceReconnection()
			.Build();
		var result = await _client.StartConnectionAsync();
		if (result.ResponseCode != BillingResponseCode.Ok)
			throw new StoreException(result.ResponseCode == BillingResponseCode.BillingUnavailable
				? "Google Play payments aren't available on this phone. Install the app from Google Play to buy."
				: "Couldn't reach Google Play. Try again in a moment.");
		return _client;
	}

	/// <summary>
	/// A product's details from Google Play - its price in the player's currency and what's needed to buy it
	/// - or null when Play doesn't know it.
	/// </summary>
	private async Task<ProductDetails?> DetailsAsync(string productId, string type)
	{
		var client = await ConnectAsync();
		var query = QueryProductDetailsParams.NewBuilder()
			.SetProductList([QueryProductDetailsParams.Product.NewBuilder().SetProductId(productId).SetProductType(type).Build()])
			.Build();
		var result = await client.QueryProductDetailsAsync(query);
		return result.ProductDetails?.FirstOrDefault();
	}

	/// <inheritdoc />
	public async Task<string> GamePriceAsync(PubGame game)
	{
		try
		{
			var details = await DetailsAsync(Prices.ProductIdFor(game.Id), BillingClient.ProductType.Inapp);
			if (details?.OneTimePurchaseOfferDetailsList?.FirstOrDefault()?.FormattedPrice is { Length: > 0 } price)
				return price;
		}
		catch (Exception)
		{
			// Play not reachable: the fixed price.
		}
		return Prices.Format(game.Price);
	}

	/// <inheritdoc />
	public async Task<List<PassPlanPrice>> PassPricesAsync()
	{
		IList<ProductDetails.SubscriptionOfferDetails>? offers = null;
		try
		{
			offers = (await DetailsAsync(Prices.PassProductId, BillingClient.ProductType.Subs))?.GetSubscriptionOfferDetails();
		}
		catch (Exception)
		{
			// Play not reachable: the fixed prices.
		}
		return Prices.PassPlans.Select(plan =>
		{
			var price = offers?.FirstOrDefault(o => o.BasePlanId == plan.BasePlanId)?.PricingPhases?.PricingPhaseList?.LastOrDefault()?.FormattedPrice;
			return new PassPlanPrice(plan, string.IsNullOrEmpty(price) ? Prices.Format(plan.Price) : price);
		}).ToList();
	}

	/// <inheritdoc />
	public async Task<(PurchaseOutcome Outcome, string? Problem)> BuyGameAsync(PubGame game)
	{
		try
		{
			var details = await DetailsAsync(Prices.ProductIdFor(game.Id), BillingClient.ProductType.Inapp)
				?? throw new StoreException("This game can't be bought yet. Try again later.");
			var offer = details.OneTimePurchaseOfferDetailsList?.FirstOrDefault();
			var product = BillingFlowParams.ProductDetailsParams.NewBuilder().SetProductDetails(details);
			if (offer?.OfferToken is { Length: > 0 } token)
				product.SetOfferToken(token);
			return await LaunchAsync(product.Build(), purchase => VerifyAsync("game", purchase.PurchaseToken, game.Id));
		}
		catch (Exception ex) when (ex is StoreException or HttpRequestException or CloudException)
		{
			return (PurchaseOutcome.Failed, Problem(ex));
		}
	}

	/// <inheritdoc />
	public async Task<(PurchaseOutcome Outcome, string? Problem)> BuyPassAsync(PassPlan plan)
	{
		try
		{
			var details = await DetailsAsync(Prices.PassProductId, BillingClient.ProductType.Subs)
				?? throw new StoreException("The Pub Games Pass isn't available yet. Try again later.");
			var offer = details.GetSubscriptionOfferDetails()?.FirstOrDefault(o => o.BasePlanId == plan.BasePlanId)
				?? throw new StoreException($"The {plan.Name.ToLowerInvariant()} pass isn't available yet.");
			var product = BillingFlowParams.ProductDetailsParams.NewBuilder().SetProductDetails(details).SetOfferToken(offer.OfferToken).Build();
			return await LaunchAsync(product, purchase => VerifyAsync("pass", purchase.PurchaseToken, null));
		}
		catch (Exception ex) when (ex is StoreException or HttpRequestException or CloudException)
		{
			return (PurchaseOutcome.Failed, Problem(ex));
		}
	}

	/// <summary>Opens Google Play's purchase screen; a completed purchase goes to the server before it counts.</summary>
	private async Task<(PurchaseOutcome Outcome, string? Problem)> LaunchAsync(BillingFlowParams.ProductDetailsParams product, Func<PlayPurchase, Task> verify)
	{
		var client = await ConnectAsync();
		if (auth.CurrentUser is not { } user) return (PurchaseOutcome.Failed, "Sign in first.");
		if (Platform.CurrentActivity is not { } activity) return (PurchaseOutcome.Failed, "Try again.");

		var flow = BillingFlowParams.NewBuilder()
			.SetProductDetailsParamsList([product])
			// So the server can check the purchase is this account's.
			.SetObfuscatedAccountId(user.Uid)
			.Build();
		_flow = new TaskCompletionSource<(BillingResult, IList<PlayPurchase>?)>(TaskCreationOptions.RunContinuationsAsynchronously);
		var started = client.LaunchBillingFlow(activity, flow);
		if (started.ResponseCode != BillingResponseCode.Ok)
			return started.ResponseCode == BillingResponseCode.ItemAlreadyOwned
				? (PurchaseOutcome.Failed, "You already own this. Tap Restore purchases under Account > Pub Games Pass.")
				: (PurchaseOutcome.Failed, "Couldn't open Google Play's payment screen.");

		var (result, purchases) = await _flow.Task;
		if (result.ResponseCode == BillingResponseCode.UserCancelled) return (PurchaseOutcome.Cancelled, null);
		if (result.ResponseCode == BillingResponseCode.ItemAlreadyOwned)
			return (PurchaseOutcome.Failed, "You already own this. Tap Restore purchases under Account > Pub Games Pass.");
		if (result.ResponseCode != BillingResponseCode.Ok || purchases is not { Count: > 0 })
			return (PurchaseOutcome.Failed, "The purchase didn't go through.");

		var purchase = purchases[0];
		if (purchase.PurchaseState == PurchaseState.Pending) return (PurchaseOutcome.Pending, null);
		try
		{
			await verify(purchase);
		}
		catch (Exception ex) when (ex is StoreException or HttpRequestException)
		{
			return (PurchaseOutcome.Failed, Problem(ex));
		}
		await RefreshAsync();
		return (PurchaseOutcome.Bought, null);
	}

	/// <inheritdoc />
	public async Task<Entitlements> RestoreAsync(IEnumerable<PubGame> games)
	{
		var byProduct = games.Where(g => g.IsPaid).ToDictionary(g => Prices.ProductIdFor(g.Id), g => g.Id);
		var client = await ConnectAsync();
		foreach (var type in new[] { BillingClient.ProductType.Inapp, BillingClient.ProductType.Subs })
		{
			var result = await client.QueryPurchasesAsync(QueryPurchasesParams.NewBuilder().SetProductType(type).Build());
			foreach (var purchase in result.Purchases ?? [])
			{
				if (purchase.PurchaseState != PurchaseState.Purchased) continue;
				foreach (var productId in purchase.Products)
				{
					if (productId == Prices.PassProductId)
						await VerifyAsync("pass", purchase.PurchaseToken, null);
					else if (byProduct.TryGetValue(productId, out var gameId))
						await VerifyAsync("game", purchase.PurchaseToken, gameId);
				}
			}
		}
		return await RefreshAsync();
	}
}
