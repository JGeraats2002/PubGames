using PubGames.ViewModels;

namespace PubGames.Views;

public partial class CategoriesPage : ContentPage
{
	private readonly CategoriesViewModel _vm;

	public CategoriesPage(CategoriesViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	/// <summary>Also runs when coming back from choosing a category's games, so the counts update.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	private async void OnAddClicked(object? sender, EventArgs e)
	{
		var name = await DisplayPromptAsync(
			_vm.IsAdmin ? "New category" : "Propose a new category",
			_vm.IsAdmin ? "Name of the category:" : "Name of the category. An admin reviews it first:",
			_vm.IsAdmin ? "Add" : "Submit for review", "Cancel", placeholder: "e.g. Party", maxLength: 30);
		if (string.IsNullOrWhiteSpace(name)) return;

		if (await _vm.AddAsync(name) is { } done)
			await DisplayAlertAsync("Done", done, "OK");
	}

	/// <summary>Admins choose which games are in the category.</summary>
	private async void OnCategoryTapped(object? sender, TappedEventArgs e)
	{
		if (!_vm.IsAdmin || e.Parameter is not CategoryRow row) return;
		await Shell.Current.GoToAsync(nameof(CategoryGamesPage), new Dictionary<string, object> { ["categoryId"] = row.Category.Id });
	}
}
