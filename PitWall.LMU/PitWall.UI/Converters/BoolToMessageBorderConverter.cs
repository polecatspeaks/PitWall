using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PitWall.UI.Converters;

/// <summary>
/// Converts a boolean to message border brush.
/// True (User messages) = #2A2A2A, False (Assistant messages) = BrushInfo (#00D9FF)
/// </summary>
public class BoolToMessageBorderConverter : IValueConverter
{
    private static readonly SolidColorBrush UserBorder = new(Color.Parse("#2A2A2A"));
    private static readonly SolidColorBrush AssistantBorder = new(Color.Parse("#00D9FF"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? UserBorder : AssistantBorder;
        }

        return AssistantBorder;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
