namespace PubGames.Views;

/// <summary>The phone's on-screen keyboard.</summary>
public static class SoftKeyboard
{
	/// <summary>
	/// Closes the keyboard if it's open and takes the focus off the text box, e.g. when going to another page:
	/// a keyboard left open there only covers the page.
	/// </summary>
	public static void Hide()
	{
#if ANDROID
		if (Platform.CurrentActivity is not { } activity) return;
		var view = activity.CurrentFocus ?? activity.Window?.DecorView;
		if (view?.WindowToken is null) return;
		if (activity.GetSystemService(Android.Content.Context.InputMethodService) is Android.Views.InputMethods.InputMethodManager keyboard)
			keyboard.HideSoftInputFromWindow(view.WindowToken, Android.Views.InputMethods.HideSoftInputFlags.None);
		activity.CurrentFocus?.ClearFocus();
#endif
	}
}
