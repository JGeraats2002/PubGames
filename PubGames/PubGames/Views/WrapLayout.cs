using Microsoft.Maui.Layouts;

namespace PubGames.Views;

/// <summary>
/// Puts its children next to each other and goes on to the next line when one doesn't fit, like words in a
/// sentence. Used for category clouds and other chips instead of FlexLayout, which can make a chip a little
/// too narrow on some phones, so its last letter falls off ("Kaarte" for "Kaarten"). Here every child gets the
/// width it asks for, rounded up, plus a little extra, in every language, font size and screen.
/// </summary>
public class WrapLayout : Layout
{
	/// <inheritdoc />
	protected override ILayoutManager CreateLayoutManager() => new WrapLayoutManager(this);
}

/// <summary>Measures and places the children of a WrapLayout, line by line.</summary>
internal sealed class WrapLayoutManager : ILayoutManager
{
	/// <summary>
	/// Extra width for every child, so rounding to screen pixels and the border around a chip never cut off
	/// text. Too small to see.
	/// </summary>
	private const double Slack = 4;

	private readonly WrapLayout _layout;

	/// <summary>The manager for one WrapLayout.</summary>
	public WrapLayoutManager(WrapLayout layout) => _layout = layout;

	/// <inheritdoc />
	public Size Measure(double widthConstraint, double heightConstraint)
	{
		var padding = _layout.Padding;
		var available = widthConstraint - padding.HorizontalThickness;
		var lineX = 0.0;
		var lineY = 0.0;
		var lineHeight = 0.0;
		var widest = 0.0;

		foreach (var child in _layout)
		{
			if (child.Visibility == Visibility.Collapsed) continue;

			var size = child.Measure(available, double.PositiveInfinity);
			var width = Math.Ceiling(size.Width) + Slack;
			if (lineX > 0 && lineX + width > available)
			{
				lineY += lineHeight;
				lineX = 0;
				lineHeight = 0;
			}
			lineX += width;
			lineHeight = Math.Max(lineHeight, Math.Ceiling(size.Height));
			widest = Math.Max(widest, lineX);
		}

		var totalWidth = double.IsInfinity(widthConstraint) ? widest + padding.HorizontalThickness : widthConstraint;
		return new Size(totalWidth, lineY + lineHeight + padding.VerticalThickness);
	}

	/// <inheritdoc />
	public Size ArrangeChildren(Rect bounds)
	{
		var padding = _layout.Padding;
		var left = bounds.Left + padding.Left;
		var top = bounds.Top + padding.Top;
		var available = bounds.Width - padding.HorizontalThickness;
		var lineX = 0.0;
		var lineY = 0.0;
		var lineHeight = 0.0;

		foreach (var child in _layout)
		{
			if (child.Visibility == Visibility.Collapsed) continue;

			var size = child.DesiredSize;
			var width = Math.Min(Math.Ceiling(size.Width) + Slack, available);
			var height = Math.Ceiling(size.Height);
			if (lineX > 0 && lineX + width > available)
			{
				lineY += lineHeight;
				lineX = 0;
				lineHeight = 0;
			}
			child.Arrange(new Rect(left + lineX, top + lineY, width, height));
			lineX += width;
			lineHeight = Math.Max(lineHeight, height);
		}

		return new Size(bounds.Width, lineY + lineHeight + padding.VerticalThickness);
	}
}
