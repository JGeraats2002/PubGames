using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>The games of a category: tick the games that belong in it.</summary>
public partial class CategoryGamesPage : ContentPage
{
	private readonly CategoryGamesViewModel _vm;

	/// <summary>
	/// Sets up the page with its view model and the ⓘ help, and goes back once the games are saved.
	/// </summary>
	public CategoryGamesPage(CategoryGamesViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.CategoryGames);
		_vm.Finished += async (_, message) =>
		{
			await DisplayAlertAsync("Saved", message, "OK");
			await Shell.Current.GoToAsync("..");
		};
	}

	/// <summary>Saves which games are in the category.</summary>
	private async void OnSaveClicked(object? sender, EventArgs e) => await _vm.SaveAsync();

	/// <summary>Opens the filters (number of players, categories).</summary>
	private async void OnFilterClicked(object? sender, EventArgs e) => await FilterPage.ShowAsync(Navigation, _vm.Filters);

	/// <summary>Ticks or unticks the tapped game.</summary>
	private void OnGameTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is GameChoice choice)
			_vm.Toggle(choice);
	}
}
