using PubGames.ViewModels;

namespace PubGames.Views;

public partial class CategoryGamesPage : ContentPage
{
	private readonly CategoryGamesViewModel _vm;

	public CategoryGamesPage(CategoryGamesViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_vm.Finished += async (_, message) =>
		{
			await DisplayAlertAsync("Saved", message, "OK");
			await Shell.Current.GoToAsync("..");
		};
	}

	private async void OnSaveClicked(object? sender, EventArgs e) => await _vm.SaveAsync();
}
