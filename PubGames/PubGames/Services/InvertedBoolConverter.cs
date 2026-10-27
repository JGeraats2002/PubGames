using System.Globalization;

namespace PubGames.Services;

/// <summary>Shows the opposite of a yes/no value, e.g. a button only while something is NOT busy.</summary>
public class InvertedBoolConverter : IValueConverter
{
	/// <summary>Yes becomes no and no becomes yes.</summary>
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is bool b ? !b : value;

	/// <summary>The same the other way round.</summary>
	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is bool b ? !b : value;
}
