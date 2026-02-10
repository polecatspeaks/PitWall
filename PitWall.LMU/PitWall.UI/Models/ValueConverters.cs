using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PitWall.UI.Models;

/// <summary>
/// Converts fuel laps remaining to status color brush.
/// Red if < 2 laps, amber if < 4 laps, green otherwise.
/// TODO: Move to Converters folder when available
/// </summary>
public class FuelStatusConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not double fuelLaps)
		{
			return Brushes.Gray;
		}

		if (fuelLaps < 2.0)
		{
			return new SolidColorBrush(Color.Parse("#FF0033")); // Critical red
		}

		if (fuelLaps < 4.0)
		{
			return new SolidColorBrush(Color.Parse("#FFB800")); // Warning amber
		}

		return new SolidColorBrush(Color.Parse("#00FF41")); // Success green
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}

/// <summary>
/// Converts tire wear percentage to status color brush.
/// Red if < 15%, amber if < 30%, green otherwise.
/// </summary>
public class TireWearConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not double wearPercentage)
		{
			return Brushes.Gray;
		}

		if (wearPercentage < 15.0)
		{
			return new SolidColorBrush(Color.Parse("#FF0033")); // Critical red
		}

		if (wearPercentage < 30.0)
		{
			return new SolidColorBrush(Color.Parse("#FFB800")); // Warning amber
		}

		return new SolidColorBrush(Color.Parse("#00FF41")); // Success green
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}

/// <summary>
/// Converts tire temperature to gradient color brush.
/// Blue (cold) → Green (optimal) → Red (hot)
/// Optimal range: 85-105°C
/// </summary>
public class TireTempConverter : IValueConverter
{
	private const double ColdTemp = 70.0;
	private const double OptimalMinTemp = 85.0;
	private const double OptimalMaxTemp = 105.0;
	private const double HotTemp = 120.0;

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not double tempC)
		{
			return Brushes.Gray;
		}

		if (tempC < ColdTemp)
		{
			return new SolidColorBrush(Color.Parse("#00D9FF"));
		}

		if (tempC < OptimalMinTemp)
		{
			var ratio = (tempC - ColdTemp) / (OptimalMinTemp - ColdTemp);
			return new SolidColorBrush(InterpolateColor(
				Color.Parse("#00D9FF"),
				Color.Parse("#00FF41"),
				ratio
			));
		}

		if (tempC <= OptimalMaxTemp)
		{
			return new SolidColorBrush(Color.Parse("#00FF41"));
		}

		if (tempC < HotTemp)
		{
			var ratio = (tempC - OptimalMaxTemp) / (HotTemp - OptimalMaxTemp);
			return new SolidColorBrush(InterpolateColor(
				Color.Parse("#00FF41"),
				Color.Parse("#FFB800"),
				ratio
			));
		}

		return new SolidColorBrush(Color.Parse("#FF0033"));
	}

	private static Color InterpolateColor(Color start, Color end, double ratio)
	{
		ratio = Math.Clamp(ratio, 0.0, 1.0);
		return Color.FromRgb(
			(byte)(start.R + (end.R - start.R) * ratio),
			(byte)(start.G + (end.G - start.G) * ratio),
			(byte)(start.B + (end.B - start.B) * ratio)
		);
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}

/// <summary>
/// Converts boolean to background brush for chat messages.
/// User messages get darker background, assistant messages lighter.
/// </summary>
public class BoolToBackgroundConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is bool isUser && isUser)
		{
			return new SolidColorBrush(Color.Parse("#2A2A2A"));
		}
		return new SolidColorBrush(Color.Parse("#1A1A1A"));
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}

/// <summary>
/// Converts boolean to horizontal alignment.
/// User messages align right, assistant messages align left.
/// </summary>
public class BoolToAlignmentConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is bool isUser && isUser)
		{
			return Avalonia.Layout.HorizontalAlignment.Right;
		}
		return Avalonia.Layout.HorizontalAlignment.Left;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}