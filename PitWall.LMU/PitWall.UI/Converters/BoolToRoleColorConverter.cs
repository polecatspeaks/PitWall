using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PitWall.UI.Converters;

/// <summary>
/// Converts a boolean to role color for chat messages.
/// True (User) = BrushWarning (#FFB800), False (Assistant) = BrushSuccess (#00FF41)
/// </summary>
public class BoolToRoleColorConverter : IValueConverter
{
    private static readonly SolidColorBrush UserColor = new(Color.Parse("#FFB800"));
    private static readonly SolidColorBrush AssistantColor = new(Color.Parse("#00FF41"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? UserColor : AssistantColor;
        }

        return AssistantColor;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
