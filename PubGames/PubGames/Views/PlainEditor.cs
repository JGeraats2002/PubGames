using Microsoft.Maui.Handlers;

namespace PubGames.Views;

/// <summary>
/// A text box without its own line or padding, so several of them under each
/// other - with pictures in between - look like one text (the rules of a
/// game). Otherwise an ordinary Editor.
/// </summary>
public class PlainEditor : Editor
{
	static PlainEditor() => EditorHandler.Mapper.AppendToMapping(nameof(PlainEditor), (handler, view) =>
	{
#if ANDROID
		if (view is not PlainEditor) return;
		handler.PlatformView.Background = null;
		handler.PlatformView.SetPadding(0, 0, 0, 0);
#endif
	});
}
