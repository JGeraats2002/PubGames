using Android.App;
using Android.Runtime;

namespace PubGames;

/// <summary>The Android application: starts .NET MAUI when Android starts the app.</summary>
[Application]
public class MainApplication : MauiApplication
{
	/// <summary>Made by Android when the app starts.</summary>
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	/// <summary>Builds the app with its services and pages (see MauiProgram).</summary>
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
