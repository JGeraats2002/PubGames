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
		// TODO: pass the selected players along (e.g. via a shared session-builder
		// service) so the library page knows who's in the group before a game starts.
		await Shell.Current.GoToAsync(nameof(GameLibraryPage));
	}
}
