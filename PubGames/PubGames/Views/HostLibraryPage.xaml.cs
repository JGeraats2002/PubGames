using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

public partial class HostLibraryPage : ContentPage
{
	private readonly HostLibraryViewModel _vm;

	public HostLibraryPage(HostLibraryViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	/// <summary>Also runs when coming back from the edit page, so saved changes and deletions show immediately.</summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync();
	}

	private async void OnNewGameClicked(object? sender, EventArgs e) =>
		await Shell.Current.GoToAsync(nameof(HostCreateGamePage));

	private async void OnGameTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is PubGame game)
			await Shell.Current.GoToAsync(nameof(HostCreateGamePage), new Dictionary<string, object> { ["gameId"] = game.Id });
	}
}
