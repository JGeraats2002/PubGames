using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>One pass plan on the page: its name, Google Play's price, and whether it's the one running.</summary>
public sealed record PassPlanRow(PassPlan Plan, string Price, bool IsCurrent)
{
	/// <summary>The plan's name: Week, Month or Year.</summary>
	public string Name => Plan.Name;

	/// <summary>The plan's button: its price per period, or "Your plan".</summary>
	public string ButtonText => IsCurrent ? "Your plan" : $"{Price} / {Plan.Name.ToLowerInvariant()}";
}

/// <summary>
/// Account > Pub Games Pass: whether the pass runs and until when, the
/// three plans with their prices, managing the subscription in Google Play,
/// and restoring purchases (games and the pass) on a new phone.
/// </summary>
public partial class SubscriptionViewModel : ObservableObject
{
	private readonly IStoreService _store;
	private readonly ILocalDatabaseService _local;

	/// <summary>
	/// Gets the services it works with - the store and the phone's database - from the app (dependency
	/// injection).
	/// </summary>
	public SubscriptionViewModel(IStoreService store, ILocalDatabaseService local)
	{
		_store = store;
		_local = local;
	}

	/// <summary>The three plans with Google Play's prices.</summary>
	public ObservableCollection<PassPlanRow> Plans { get; } = new();

	/// <summary>Whether the pass runs, until when, and whether it renews.</summary>
	[ObservableProperty]
	private string status = string.Empty;

	/// <summary>Whether the pass runs: Manage subscription shows.</summary>
	[ObservableProperty]
	private bool hasPass;

	/// <summary>What happened with a purchase or restore, or what went wrong.</summary>
	[ObservableProperty]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	/// <summary>Buying or restoring: a spinner shows and the buttons wait.</summary>
	[ObservableProperty]
	private bool isBusy;

	/// <summary>Shows whether the pass runs, and the plans with Google Play's prices.</summary>
	public async Task LoadAsync()
	{
		ShowStatus(await _store.RefreshAsync());
		var prices = await _store.PassPricesAsync();
		Plans.Clear();
		foreach (var planPrice in prices)
			Plans.Add(new PassPlanRow(planPrice.Plan, planPrice.Price, _store.Current.HasPass && _store.Current.PassPlan == planPrice.Plan.BasePlanId));
	}

	/// <summary>Shows until when the pass runs and whether it renews.</summary>
	private void ShowStatus(Entitlements e)
	{
		HasPass = e.HasPass;
		var until = e.PassRunsUntil?.ToLocalTime().ToString("d MMMM yyyy", CultureInfo.CurrentCulture);
		Status = !e.HasPass
			? "You don't have the Pub Games Pass."
			: e.Renews ? $"Your pass runs and renews on {until}." : $"Your pass runs until {until} and doesn't renew.";
	}



	/// <summary>Buys a pass plan with Google Play.</summary>
	public async Task BuyAsync(PassPlanRow row)
	{
		if (row.IsCurrent || IsBusy) return;
		IsBusy = true;
		try
		{
			var (outcome, problem) = await _store.BuyPassAsync(row.Plan);
			StatusMessage = outcome switch
			{
				PurchaseOutcome.Bought => "Thanks! Every game is unlocked.",
				PurchaseOutcome.Pending => "The pass starts as soon as Google Play confirms the payment.",
				PurchaseOutcome.Failed => problem ?? "The purchase didn't go through.",
				_ => string.Empty
			};
			await LoadAsync();
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Checks every purchase of this account with Google Play again and unlocks what was bought.
	/// </summary>
	public async Task RestoreAsync()
	{
		if (IsBusy) return;
		IsBusy = true;
		StatusMessage = "Restoring...";
		try
		{
			var owned = await _store.RestoreAsync(await _local.GetGamesAsync());
			StatusMessage = owned.HasPass || owned.Games.Count > 0
				? $"Restored: {(owned.HasPass ? "the Pub Games Pass" : string.Empty)}{(owned.HasPass && owned.Games.Count > 0 ? " and " : string.Empty)}{(owned.Games.Count > 0 ? $"{owned.Games.Count} {(owned.Games.Count == 1 ? "game" : "games")}" : string.Empty)}."
				: "Nothing to restore for this account.";
			await LoadAsync();
		}
		catch (Exception ex) when (ex is StoreException or HttpRequestException or CloudException)
		{
			StatusMessage = ex is HttpRequestException ? "No connection - try again when you're online." : ex.Message;
		}
		finally
		{
			IsBusy = false;
		}
	}
}
