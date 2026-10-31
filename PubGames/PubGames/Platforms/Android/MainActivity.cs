using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace PubGames;

/// <summary>
/// The app's one Android screen (activity): MAUI shows every page inside it. Rotating or resizing doesn't
/// restart it (ConfigurationChanges).
/// </summary>
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
	ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
		ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	/// <summary>Whether the keyboard was open the last time the screen was checked.</summary>
	private bool _keyboardWasOpen;

	/// <summary>Starts the screen and begins watching the keyboard.</summary>
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		Window?.DecorView.ViewTreeObserver?.AddOnGlobalLayoutListener(new LayoutListener(CheckKeyboard));
	}

	/// <summary>
	/// The keyboard closed - with its own button, the back button or a swipe:
	/// the text box loses its focus too, so typing has really ended. On the
	/// scoreboard that's when a typed score is checked and counted.
	/// </summary>
	private void CheckKeyboard()
	{
		if (Window?.DecorView is not { } decor) return;
		var open = ViewCompat.GetRootWindowInsets(decor)?.IsVisible(WindowInsetsCompat.Type.Ime()) ?? false;
		if (_keyboardWasOpen && !open && CurrentFocus is Android.Widget.EditText focused)
			focused.ClearFocus();
		_keyboardWasOpen = open;
	}

	/// <summary>Calls back every time Android lays out the screen - also when the keyboard opens or closes.</summary>
	private sealed class LayoutListener(Action changed) : Java.Lang.Object, Android.Views.ViewTreeObserver.IOnGlobalLayoutListener
	{
		/// <summary>The screen was laid out: check the keyboard again.</summary>
		public void OnGlobalLayout() => changed();
	}
}
