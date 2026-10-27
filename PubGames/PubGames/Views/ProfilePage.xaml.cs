using PubGames.Models;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>Settings > Profile: picture, name, nickname and the player code.</summary>
public partial class ProfilePage : ContentPage
{
	private readonly AccountViewModel _vm;

	public ProfilePage(AccountViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		InfoButton.Attach(this, () => Help.Profile);
	}

	/// <summary>The owner's friend code: the next one is fetched as soon as the shown one runs out.</summary>
	private IDispatcherTimer? _codeTimer;

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

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_codeTimer?.Stop();
	}

	private async void OnCodeTimerTick(object? sender, EventArgs e)
	{
		if (_vm.FriendCodeValidUntil is not { } until || DateTime.UtcNow >= until)
			await _vm.LoadFriendCodeAsync();
	}
}
