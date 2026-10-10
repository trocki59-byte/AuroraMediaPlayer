using System.Globalization;

namespace AuroraMediaPlayer.Converters;

public sealed class InvertedBoolConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is not true;

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is not true;
}
