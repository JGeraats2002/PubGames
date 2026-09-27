using PubGames.ViewModels;

namespace PubGames.Views;

public partial class PlayerEntryPage : ContentPage
{
	private readonly PlayerEntryViewModel _vm;

	public PlayerEntryPage(PlayerEntryViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	private async void OnChooseGameClicked(object? sender, EventArgs e)
	{
		if (_vm.SelectedPlayers.Count == 0)
		{
			await DisplayAlertAsync("Who's playing?", "Add at least one player first.", "OK");
			return;
		}

		await Shell.Current.GoToAsync(nameof(GameLibraryPage), new ShellNavigationQueryParameters
		{
			["players"] = _vm.SelectedPlayers.ToList()
		});
	}
}
