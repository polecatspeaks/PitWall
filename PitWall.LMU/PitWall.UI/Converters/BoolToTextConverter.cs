using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace PitWall.UI.Converters;

/// <summary>
/// Converts a boolean value to text based on pipe-separated parameter.
/// Parameter format: "TrueText|FalseText"
/// Example: "Fuel Save: ON|Fuel Save: OFF"
/// </summary>
public class BoolToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue || parameter is not string paramStr)
            return string.Empty;

        var parts = paramStr.Split('|');
        if (parts.Length != 2)
            return string.Empty;

        return boolValue ? parts[0] : parts[1];
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
