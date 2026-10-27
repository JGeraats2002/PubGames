using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PubGames.Models;
using PubGames.Services;

namespace PubGames.ViewModels;

/// <summary>One pass plan on the page: its name, Google Play's price, and whether it's the one running.</summary>
public sealed record PassPlanRow(PassPlan Plan, string Price, bool IsCurrent)
{
	public string Name => Plan.Name;

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

	public SubscriptionViewModel(IStoreService store, ILocalDatabaseService local)
	{
		_store = store;
		_local = local;
	}

	public ObservableCollection<PassPlanRow> Plans { get; } = new();

	[ObservableProperty]
	private string status = string.Empty;

	[ObservableProperty]
	private bool hasPass;

	[ObservableProperty]
	private string statusMessage = string.Empty;

	/// <summary>Temporary messages clear themselves (see MessageTimer).</summary>
	private readonly MessageTimer _statusMessageTimer = new();

	partial void OnStatusMessageChanged(string value) => _statusMessageTimer.Shown(value, () => StatusMessage, v => StatusMessage = v);

	[ObservableProperty]
	private bool isBusy;

	public async Task LoadAsync()
	{
		ShowStatus(await _store.RefreshAsync());
		var prices = await _store.PassPricesAsync();
		Plans.Clear();
		foreach (var p in prices)
			Plans.Add(new PassPlanRow(p.Plan, p.Price, _store.Current.HasPass && _store.Current.PassPlan == p.Plan.BasePlanId));
	}

	private void ShowStatus(Entitlements e)
	{
		HasPass = e.HasPass;
		var until = e.PassRunsUntil?.ToLocalTime().ToString("d MMMM yyyy", CultureInfo.CurrentCulture);
		Status = !e.HasPass
			? "You don't have the Pub Games Pass."
			: e.Renews ? $"Your pass runs and renews on {until}." : $"Your pass runs until {until} and doesn't renew.";
	}



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
