using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class CategoryGamesPage : ContentPage
{
	private readonly CategoryGamesViewModel _vm;

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

	private async void OnSaveClicked(object? sender, EventArgs e) => await _vm.SaveAsync();

	private async void OnFilterClicked(object? sender, EventArgs e) => await FilterPage.ShowAsync(Navigation, _vm.Filters);

	private void OnGameTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is GameChoice choice)
			_vm.Toggle(choice);
	}
}
