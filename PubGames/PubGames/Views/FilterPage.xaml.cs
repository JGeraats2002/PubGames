using PubGames.Models;

namespace PubGames.Views;

/// <summary>
/// The filters behind the Filter button next to a search bar: the number of
/// players and the categories under their main categories. The list of games
/// behind it follows every change straight away (CategoryFilterSet.Changed).
/// </summary>
public partial class FilterPage : ContentPage
{
	private readonly CategoryFilterSet _filters;

	/// <summary>Shows the filters as they're set now.</summary>
	private FilterPage(CategoryFilterSet filters)
	{
		InitializeComponent();
		BindingContext = _filters = filters;
		PlayersSlider.Maximum = CategoryFilterSet.MaxPlayers;
		PlayersSlider.Value = filters.Players;
	}

	/// <summary>Opens the filters over the page that lists the games.</summary>
	public static Task ShowAsync(INavigation navigation, CategoryFilterSet filters) =>
		navigation.PushModalAsync(new FilterPage(filters));

	/// <summary>Whole players only: the slider snaps to the nearest number.</summary>
	private void OnPlayersChanged(object? sender, ValueChangedEventArgs e)
	{
		var players = (int)Math.Round(e.NewValue);
		_filters.Players = players;
		if (Math.Abs(PlayersSlider.Value - players) > 0.001)
			PlayersSlider.Value = players;
	}

	/// <summary>Turns a category filter on or off.</summary>
	private void OnCategoryTapped(object? sender, CategoryChoice choice) => _filters.Toggle(choice);

	/// <summary>Clears every filter.</summary>
	private void OnClearClicked(object? sender, EventArgs e)
	{
		_filters.Clear();
		PlayersSlider.Value = 0;
	}

	/// <summary>Closes the filters; the list shows what fits.</summary>
	private async void OnDoneClicked(object? sender, EventArgs e) => await Navigation.PopModalAsync();
}
