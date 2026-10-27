using Android.App;
using Android.Content.PM;
using Android.OS;

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
}
