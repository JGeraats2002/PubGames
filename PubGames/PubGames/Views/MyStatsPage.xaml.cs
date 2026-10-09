using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class MyStatsPage : ContentPage
{
	private readonly MyStatsViewModel _vm;

	public MyStatsPage(MyStatsViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.MyStats);
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	private async void OnFilterClicked(object? sender, EventArgs e) => await FilterPage.ShowAsync(Navigation, _vm.Filters);

	private async void OnGameTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is StatsGameRow row)
			await Shell.Current.GoToAsync(nameof(GameStatsPage), new ShellNavigationQueryParameters { ["row"] = row });
	}
}
