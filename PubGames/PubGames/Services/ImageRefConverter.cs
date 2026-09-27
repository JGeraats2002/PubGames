using System.Globalization;

namespace PubGames.Services;

/// <summary>
/// Turns an image reference ("fsimg:..." or a URL) into an ImageSource that
/// loads lazily through IImageStore, so XAML can bind straight to
/// PubGame.CoverImageUrl or a rules image. Empty references show nothing.
/// </summary>
public class ImageRefConverter : IValueConverter
{
	private static IImageStore Store =>
		IPlatformApplication.Current!.Services.GetRequiredService<IImageStore>();

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is string reference && !string.IsNullOrWhiteSpace(reference)
			? ImageSource.FromStream(ct => Store.OpenAsync(reference, ct))
			: null;

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
