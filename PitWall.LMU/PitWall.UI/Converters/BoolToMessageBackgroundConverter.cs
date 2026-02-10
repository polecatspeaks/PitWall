using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PitWall.UI.Converters;

/// <summary>
/// Converts a boolean to message background brush.
/// True (User messages) = #1A1A1A, False (Assistant messages) = #0D0D0D
/// </summary>
public class BoolToMessageBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush UserBackground = new(Color.Parse("#1A1A1A"));
    private static readonly SolidColorBrush AssistantBackground = new(Color.Parse("#0D0D0D"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? UserBackground : AssistantBackground;
        }

        return AssistantBackground;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
