using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics.Platform;
using PubGames.Services;

namespace PubGames.Views;

/// <summary>
/// A picture in a game's rules as players see it: always as wide as the
/// space it's in, on every phone, with its height following from its shape,
/// on a cream-coloured background so it's clear where the picture begins and
/// ends and it can't be mistaken for the text. (A plain Image only shrinks
/// pictures to fit; it never makes them wider.)
/// </summary>
public class RulesImage : ContentView
{
	/// <summary>How much of the cream background shows around the picture.</summary>
	public const double FrameWidth = 8;

	public static readonly BindableProperty ImageRefProperty = BindableProperty.Create(
		nameof(ImageRef), typeof(string), typeof(RulesImage),
		propertyChanged: (view, _, _) => ((RulesImage)view).Load());

	/// <summary>The picture's reference (see IImageStore).</summary>
	public string? ImageRef
	{
		get => (string?)GetValue(ImageRefProperty);
		set => SetValue(ImageRefProperty, value);
	}

	private readonly Image _image = new() { Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Fill };

	/// <summary>The picture's height divided by its width; 0 until it's known.</summary>
	private double _shape;

	public RulesImage()
	{
		HorizontalOptions = LayoutOptions.Fill;
		Content = new Border
		{
			BackgroundColor = (Color)Application.Current!.Resources["SurfaceMuted"],
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = 8 },
			Padding = FrameWidth,
			Content = _image
		};
		SizeChanged += (_, _) => Fit();
	}

	private async void Load()
	{
		_shape = 0;
		var reference = ImageRef;
		if (string.IsNullOrWhiteSpace(reference))
		{
			// No picture (yet): an empty cream place, where one is being chosen or made.
			_image.Source = null;
			_image.HeightRequest = 80;
			return;
		}

		_image.HeightRequest = -1;

		var store = IPlatformApplication.Current!.Services.GetRequiredService<IImageStore>();
		_image.Source = ImageSource.FromStream(ct => store.OpenAsync(reference, ct));
		try
		{
			var shape = await Task.Run(async () =>
			{
				await using var stream = await store.OpenAsync(reference);
				using var image = PlatformImage.FromStream(stream);
				return image.Width > 0 ? (double)image.Height / image.Width : 0;
			});
			// Shows another picture by now.
			if (reference != ImageRef) return;
			_shape = shape;
			Fit();
		}
		catch (Exception)
		{
			// Offline or gone: it keeps the size Image gives it.
		}
	}

	private void Fit()
	{
		var width = Width - 2 * FrameWidth;
		if (_shape > 0 && width > 0)
			_image.HeightRequest = width * _shape;
	}
}
