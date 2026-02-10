using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace PitWall.UI.Converters;

/// <summary>
/// Converts a boolean to HorizontalAlignment for chat messages.
/// True (User messages) = Right, False (Assistant messages) = Left
/// </summary>
public class BoolToAlignmentUserConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        return HorizontalAlignment.Left;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
