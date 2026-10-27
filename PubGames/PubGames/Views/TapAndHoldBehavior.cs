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

	/// <summary>Put on a view: listens for taps and holds on it, once Android has made the view.</summary>
	protected override void OnAttachedTo(View view)
	{
		base.OnAttachedTo(view);
		_view = view;
		view.HandlerChanged += OnHandlerChanged;
		Hook();
	}

	/// <summary>Taken off a view: stops listening.</summary>
	protected override void OnDetachingFrom(View view)
	{
		view.HandlerChanged -= OnHandlerChanged;
		Unhook();
		_view = null;
		base.OnDetachingFrom(view);
	}

	/// <summary>Android made (or remade) the view: listen on the new one.</summary>
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

	/// <summary>Stops listening on the Android view.</summary>
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
	/// <summary>A short tap.</summary>
	private void OnClick(object? sender, EventArgs e) => Tapped?.Invoke(_view, EventArgs.Empty);

	/// <summary>A hold: the phone vibrates briefly and the hold is reported.</summary>
	private void OnLongClick(object? sender, Android.Views.View.LongClickEventArgs e)
	{
		// Handled: a hold doesn't also count as a tap.
		e.Handled = true;
		_native?.PerformHapticFeedback(Android.Views.FeedbackConstants.LongPress);
		Held?.Invoke(_view, EventArgs.Empty);
	}
#endif
}
