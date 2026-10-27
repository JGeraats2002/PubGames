using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>Account &gt; My stats: the user's stats per game, with search and filters.</summary>
public partial class MyStatsPage : ContentPage
{
	private readonly MyStatsViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public MyStatsPage(MyStatsViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.MyStats);
	}

	/// <summary>Every time the page shows: the stats are loaded fresh.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	/// <summary>Opens the filters (number of players, categories).</summary>
	private async void OnFilterClicked(object? sender, EventArgs e) => await FilterPage.ShowAsync(Navigation, _vm.Filters);

	/// <summary>Opens the stats of the tapped game.</summary>
	private async void OnGameTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is StatsGameRow row)
			await Shell.Current.GoToAsync(nameof(GameStatsPage), new ShellNavigationQueryParameters { ["row"] = row });
	}
}
