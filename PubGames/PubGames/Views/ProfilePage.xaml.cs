using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>Settings > Profile: picture, name, nickname and the player code.</summary>
public partial class ProfilePage : ContentPage
{
	private readonly AccountViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app) and the ⓘ help.</summary>
	public ProfilePage(AccountViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Profile);
	}

	/// <summary>The owner's friend code: the next one is fetched as soon as the shown one runs out.</summary>
	private IDispatcherTimer? _codeTimer;

	/// <summary>
	/// Every time the page shows: the profile is loaded, and for the owner the friend code - which is
	/// refreshed once it runs out.
	/// </summary>
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadAsync(withStats: false);
		if (!_vm.IsOwner) return;
		_codeTimer ??= Dispatcher.CreateTimer();
		_codeTimer.Interval = TimeSpan.FromSeconds(20);
		_codeTimer.Tick -= OnCodeTimerTick;
		_codeTimer.Tick += OnCodeTimerTick;
		_codeTimer.Start();
	}

	/// <summary>Leaving the page: the friend code is no longer checked.</summary>
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_codeTimer?.Stop();
	}

	/// <summary>Every 20 seconds: once the friend code has run out, the next one is fetched.</summary>
	private async void OnCodeTimerTick(object? sender, EventArgs e)
	{
		if (_vm.FriendCodeValidUntil is not { } until || DateTime.UtcNow >= until)
			await _vm.LoadFriendCodeAsync();
	}
}
