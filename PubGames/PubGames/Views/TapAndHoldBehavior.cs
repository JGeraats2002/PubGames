namespace PubGames.Views;

/// <summary>
/// Tap and hold on the same view, e.g. a saved player: Tapped for a short
/// tap, Held for a long press. Uses Android's own click and long-click
/// events (MAUI has no hold gesture). Don't combine it with a
/// TapGestureRecognizer on the same view. The sender of both events is the
/// view, so its BindingContext says which item it was.
/// </summary>
public class TapAndHoldBehavior : Behavior<View>
{
	public event EventHandler? Tapped;
	public event EventHandler? Held;

	private View? _view;
#if ANDROID
	private Android.Views.View? _native;
#endif

	protected override void OnAttachedTo(View view)
	{
		base.OnAttachedTo(view);
		_view = view;
		view.HandlerChanged += OnHandlerChanged;
		Hook();
	}

	protected override void OnDetachingFrom(View view)
	{
		view.HandlerChanged -= OnHandlerChanged;
		Unhook();
		_view = null;
		base.OnDetachingFrom(view);
	}

	private void OnHandlerChanged(object? sender, EventArgs e) => Hook();

	/// <summary>(Re)connects to the native view whenever MAUI creates or replaces it.</summary>
	private void Hook()
	{
#if ANDROID
		Unhook();
		if (_view?.Handler?.PlatformView is not Android.Views.View native) return;

		_native = native;
		native.Clickable = true;
		native.LongClickable = true;
		native.Click += OnClick;
		native.LongClick += OnLongClick;
#endif
	}

	private void Unhook()
	{
#if ANDROID
		if (_native is null) return;
		var native = _native;
		_native = null;

		// When MAUI throws the view away (e.g. the list is refilled), the native
		// view is already gone: then there's nothing to disconnect from.
		if (native.Handle == IntPtr.Zero) return;
		try
		{
			native.Click -= OnClick;
			native.LongClick -= OnLongClick;
		}
		catch (ObjectDisposedException)
		{
			// Disposed in the meantime: nothing to disconnect from either.
		}
#endif
	}

#if ANDROID
	private void OnClick(object? sender, EventArgs e) => Tapped?.Invoke(_view, EventArgs.Empty);

	private void OnLongClick(object? sender, Android.Views.View.LongClickEventArgs e)
	{
		// Handled: a hold doesn't also count as a tap.
		e.Handled = true;
		_native?.PerformHapticFeedback(Android.Views.FeedbackConstants.LongPress);
		Held?.Invoke(_view, EventArgs.Empty);
	}
#endif
}
