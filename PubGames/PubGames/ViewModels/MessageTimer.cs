namespace PubGames.ViewModels;

/// <summary>
/// Makes a page's message line temporary: a confirmation, problem or error
/// disappears by itself after a few seconds (longer texts stay a little
/// longer). Not cleared: "Sending..."-style messages, which last until the
/// result replaces them, and lasting messages - a state of the page, like
/// "This request no longer exists" - shown with ShowLasting.
/// </summary>
public sealed class MessageTimer
{
	private int _version;
	private string? _lasting;

	/// <summary>Call from the message property's changed hook.</summary>
	public async void Shown(string text, Func<string> current, Action<string> set)
	{
		var version = ++_version;
		if (string.IsNullOrEmpty(text) || text.EndsWith("...") || text == _lasting) return;
		await Task.Delay(Duration(text));
		if (version == _version && current() == text)
			set(string.Empty);
	}

	/// <summary>A message that stays: it describes the page, not something that just happened.</summary>
	public void ShowLasting(string text, Action<string> set)
	{
		_lasting = text;
		set(text);
	}

	public static TimeSpan Duration(string text) =>
		TimeSpan.FromSeconds(Math.Clamp(3 + text.Length / 25.0, 4, 10));
}
