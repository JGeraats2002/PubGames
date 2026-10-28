using PubGames.Services;
using PubGames.ViewModels;

namespace PubGames.Views;

/// <summary>
/// The language screen: shown by itself the first time someone signs in, and opened from Account &gt; Settings
/// &gt; Language. Confirming there builds the app again in the new language (App, AppLanguage.Changed).
/// </summary>
public partial class LanguagePage : ContentPage
{
	private readonly LanguageViewModel _vm;

	/// <summary>Sets up the page with its view model (handed in by the app).</summary>
	public LanguagePage(LanguageViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
		_vm.Confirmed += OnConfirmed;
	}

	/// <summary>From Settings: back to Account, and then the app is built again in the new language.</summary>
	private async void OnConfirmed(object? sender, EventArgs e)
	{
		if (_vm.IsFirstTime) return;
		await Navigation.PopAsync();
		if (_vm.ChangedLanguage)
			AppLanguage.RaiseChanged();
	}

	/// <summary>
	/// The first time after signing in: the screen on its own, which finishes once a language is confirmed.
	/// </summary>
	public static async Task ChooseFirstTimeAsync(Window window, IServiceProvider services)
	{
		var vm = services.GetRequiredService<LanguageViewModel>();
		vm.IsFirstTime = true;
		var chosen = new TaskCompletionSource();
		vm.Confirmed += (_, _) => chosen.TrySetResult();
		// On top of the sign-in page, so the window's root doesn't change twice in a row.
		var host = window.Page!;
		await host.Navigation.PushModalAsync(new LanguagePage(vm));
		await chosen.Task;
		await host.Navigation.PopModalAsync();
	}

	/// <summary>Leaving without confirming keeps the app in its language.</summary>
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_vm.Leave();
	}
}
