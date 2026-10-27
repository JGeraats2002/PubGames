namespace PubGames.Views;

/// <summary>
/// Tells a page how much of it the on-screen keyboard covers, so it can keep
/// its bottom buttons (like Previous / Next) right above the keyboard. The
/// keyboard's position is measured on the screen, so it works the same
/// whether Android shrinks the window for the keyboard or lays it over the
/// page. Only on Android; elsewhere it reports nothing.
/// </summary>
public static class KeyboardWatcher
{
	/// <summary>
	/// While the page is shown, calls <paramref name="covered"/> with the
	/// height (in device-independent units) the keyboard covers of the page:
	/// 0 when the keyboard is closed or doesn't cover it.
	/// </summary>
	public static void Track(ContentPage page, Action<double> covered)
	{
#if ANDROID
		Android.Views.View? decor = null;
		Android.Views.SoftInput? previousMode = null;
		IDispatcherTimer? timer = null;
		var last = 0.0;

		void Measure()
		{
			if (decor is null || page.Handler?.PlatformView is not Android.Views.View view || !view.IsAttachedToWindow) return;
			var insets = AndroidX.Core.View.ViewCompat.GetRootWindowInsets(decor);
			var keyboard = insets?.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.Ime())?.Bottom ?? 0;
			var overlap = 0;
			if (keyboard > 0)
			{
				var decorAt = new int[2];
				decor.GetLocationOnScreen(decorAt);
				var keyboardTop = decorAt[1] + decor.Height - keyboard;
				var viewAt = new int[2];
				view.GetLocationOnScreen(viewAt);
				overlap = Math.Max(0, viewAt[1] + view.Height - keyboardTop);
			}
			var units = overlap / (view.Context?.Resources?.DisplayMetrics?.Density ?? 1);
			if (Math.Abs(units - last) < 1) return;
			last = units;
			covered(units);
		}

		var listener = new LayoutListener(Measure);
		page.Appearing += (_, _) =>
		{
			if (Platform.CurrentActivity?.Window is not { } window) return;
			// No panning of the whole window: the page itself makes room for the keyboard.
			previousMode = window.Attributes?.SoftInputMode;
			window.SetSoftInputMode(Android.Views.SoftInput.AdjustResize);
			decor = window.DecorView;
			decor.ViewTreeObserver?.AddOnGlobalLayoutListener(listener);
			// Not every keyboard change lays out the page again: check now and then too.
			timer ??= page.Dispatcher.CreateTimer();
			timer.Interval = TimeSpan.FromMilliseconds(250);
			timer.Tick += Tick;
			timer.Start();
		};
		page.Disappearing += (_, _) =>
		{
			if (timer is not null)
			{
				timer.Stop();
				timer.Tick -= Tick;
			}
			decor?.ViewTreeObserver?.RemoveOnGlobalLayoutListener(listener);
			if (previousMode is { } mode)
				Platform.CurrentActivity?.Window?.SetSoftInputMode(mode);
			decor = null;
			if (last != 0)
			{
				last = 0;
				covered(0);
			}
		};

		void Tick(object? sender, EventArgs e) => Measure();
#endif
	}

#if ANDROID
	/// <summary>
	/// Calls back every time Android lays out the screen - also when the keyboard opens or closes.
	/// </summary>
	private sealed class LayoutListener(Action changed) : Java.Lang.Object, Android.Views.ViewTreeObserver.IOnGlobalLayoutListener
	{
		/// <summary>The screen was laid out: measure the keyboard again.</summary>
		public void OnGlobalLayout() => changed();
	}
#endif
}
