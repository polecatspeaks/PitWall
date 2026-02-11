using System;
using System.Globalization;
using Avalonia.Layout;
using Avalonia.Media;
using Xunit;
using PitWall.UI.Models;

namespace PitWall.UI.Tests;

/// <summary>
/// Comprehensive unit tests for all value converters in PitWall.UI.
/// Tests Convert and ConvertBack methods, edge cases, null values, and boundary conditions.
/// </summary>
public class ValueConverterTests
{
	#region BoolToAlignmentConverter Tests

	[Fact]
	public void BoolToAlignmentConverter_WithTrue_ReturnsRight()
	{
		// Arrange
		var converter = new BoolToAlignmentConverter();

		// Act
		var result = converter.Convert(true, typeof(HorizontalAlignment), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal(HorizontalAlignment.Right, result);
	}

	[Fact]
	public void BoolToAlignmentConverter_WithFalse_ReturnsLeft()
	{
		// Arrange
		var converter = new BoolToAlignmentConverter();

		// Act
		var result = converter.Convert(false, typeof(HorizontalAlignment), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal(HorizontalAlignment.Left, result);
	}

	[Fact]
	public void BoolToAlignmentConverter_WithNull_ReturnsLeft()
	{
		// Arrange
		var converter = new BoolToAlignmentConverter();

		// Act
		var result = converter.Convert(null, typeof(HorizontalAlignment), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal(HorizontalAlignment.Left, result);
	}

	[Fact]
	public void BoolToAlignmentConverter_WithInvalidType_ReturnsLeft()
	{
		// Arrange
		var converter = new BoolToAlignmentConverter();

		// Act
		var result = converter.Convert("not a bool", typeof(HorizontalAlignment), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal(HorizontalAlignment.Left, result);
	}

	[Fact]
	public void BoolToAlignmentConverter_ConvertBack_ThrowsNotImplementedException()
	{
		// Arrange
		var converter = new BoolToAlignmentConverter();

		// Act & Assert
		Assert.Throws<NotImplementedException>(() =>
			converter.ConvertBack(HorizontalAlignment.Right, typeof(bool), null, CultureInfo.InvariantCulture));
	}

	#endregion

	#region BoolToBackgroundConverter Tests

	[Fact]
	public void BoolToBackgroundConverter_WithTrue_ReturnsDarkerBackground()
	{
		// Arrange
		var converter = new BoolToBackgroundConverter();

		// Act
		var result = converter.Convert(true, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#2A2A2A"), brush.Color);
	}

	[Fact]
	public void BoolToBackgroundConverter_WithFalse_ReturnsLighterBackground()
	{
		// Arrange
		var converter = new BoolToBackgroundConverter();

		// Act
		var result = converter.Convert(false, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#1A1A1A"), brush.Color);
	}

	[Fact]
	public void BoolToBackgroundConverter_WithNull_ReturnsLighterBackground()
	{
		// Arrange
		var converter = new BoolToBackgroundConverter();

		// Act
		var result = converter.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#1A1A1A"), brush.Color);
	}

	[Fact]
	public void BoolToBackgroundConverter_WithInvalidType_ReturnsLighterBackground()
	{
		// Arrange
		var converter = new BoolToBackgroundConverter();

		// Act
		var result = converter.Convert("not a bool", typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#1A1A1A"), brush.Color);
	}

	[Fact]
	public void BoolToBackgroundConverter_ConvertBack_ThrowsNotImplementedException()
	{
		// Arrange
		var converter = new BoolToBackgroundConverter();

		// Act & Assert
		Assert.Throws<NotImplementedException>(() =>
			converter.ConvertBack(Brushes.Black, typeof(bool), null, CultureInfo.InvariantCulture));
	}

	#endregion

	#region TireWearConverter Tests

	[Fact]
	public void TireWearConverter_WithCriticalWear_ReturnsRedBrush()
	{
		// Arrange
		var converter = new TireWearConverter();

		// Act
		var result = converter.Convert(10.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#FF0033"), brush.Color);
	}

	[Fact]
	public void TireWearConverter_WithExactly15Percent_ReturnsAmberBrush()
	{
		// Arrange
		var converter = new TireWearConverter();

		// Act
		var result = converter.Convert(15.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#FFB800"), brush.Color);
	}

	[Fact]
	public void TireWearConverter_WithWarningWear_ReturnsAmberBrush()
	{
		// Arrange
		var converter = new TireWearConverter();

		// Act
		var result = converter.Convert(20.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#FFB800"), brush.Color);
	}

	[Fact]
	public void TireWearConverter_WithExactly30Percent_ReturnsGreenBrush()
	{
		// Arrange
		var converter = new TireWearConverter();

		// Act
		var result = converter.Convert(30.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#00FF41"), brush.Color);
	}

	[Fact]
	public void TireWearConverter_WithGoodWear_ReturnsGreenBrush()
	{
		// Arrange
		var converter = new TireWearConverter();

		// Act
		var result = converter.Convert(50.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#00FF41"), brush.Color);
	}

	[Fact]
	public void TireWearConverter_WithZeroWear_ReturnsRedBrush()
	{
		// Arrange
		var converter = new TireWearConverter();

		// Act
		var result = converter.Convert(0.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#FF0033"), brush.Color);
	}

	[Fact]
	public void TireWearConverter_WithNegativeWear_ReturnsRedBrush()
	{
		// Arrange
		var converter = new TireWearConverter();

		// Act
		var result = converter.Convert(-5.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#FF0033"), brush.Color);
	}

	[Fact]
	public void TireWearConverter_WithNull_ReturnsGrayBrush()
	{
		// Arrange
		var converter = new TireWearConverter();

		// Act
		var result = converter.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal(Brushes.Gray, result);
	}

	[Fact]
	public void TireWearConverter_WithInvalidType_ReturnsGrayBrush()
	{
		// Arrange
		var converter = new TireWearConverter();

		// Act
		var result = converter.Convert("not a double", typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal(Brushes.Gray, result);
	}

	[Fact]
	public void TireWearConverter_ConvertBack_ThrowsNotImplementedException()
	{
		// Arrange
		var converter = new TireWearConverter();

		// Act & Assert
		Assert.Throws<NotImplementedException>(() =>
			converter.ConvertBack(Brushes.Green, typeof(double), null, CultureInfo.InvariantCulture));
	}

	#endregion

	#region FuelStatusConverter Tests

	[Fact]
	public void FuelStatusConverter_WithCriticalFuel_ReturnsRedBrush()
	{
		// Arrange
		var converter = new FuelStatusConverter();

		// Act
		var result = converter.Convert(1.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#FF0033"), brush.Color);
	}

	[Fact]
	public void FuelStatusConverter_WithExactly2Laps_ReturnsAmberBrush()
	{
		// Arrange
		var converter = new FuelStatusConverter();

		// Act
		var result = converter.Convert(2.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#FFB800"), brush.Color);
	}

	[Fact]
	public void FuelStatusConverter_WithWarningFuel_ReturnsAmberBrush()
	{
		// Arrange
		var converter = new FuelStatusConverter();

		// Act
		var result = converter.Convert(3.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#FFB800"), brush.Color);
	}

	[Fact]
	public void FuelStatusConverter_WithExactly4Laps_ReturnsGreenBrush()
	{
		// Arrange
		var converter = new FuelStatusConverter();

		// Act
		var result = converter.Convert(4.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#00FF41"), brush.Color);
	}

	[Fact]
	public void FuelStatusConverter_WithGoodFuel_ReturnsGreenBrush()
	{
		// Arrange
		var converter = new FuelStatusConverter();

		// Act
		var result = converter.Convert(10.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#00FF41"), brush.Color);
	}

	[Fact]
	public void FuelStatusConverter_WithZeroFuel_ReturnsRedBrush()
	{
		// Arrange
		var converter = new FuelStatusConverter();

		// Act
		var result = converter.Convert(0.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#FF0033"), brush.Color);
	}

	[Fact]
	public void FuelStatusConverter_WithNegativeFuel_ReturnsRedBrush()
	{
		// Arrange
		var converter = new FuelStatusConverter();

		// Act
		var result = converter.Convert(-1.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#FF0033"), brush.Color);
	}

	[Fact]
	public void FuelStatusConverter_WithNull_ReturnsGrayBrush()
	{
		// Arrange
		var converter = new FuelStatusConverter();

		// Act
		var result = converter.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal(Brushes.Gray, result);
	}

	[Fact]
	public void FuelStatusConverter_WithInvalidType_ReturnsGrayBrush()
	{
		// Arrange
		var converter = new FuelStatusConverter();

		// Act
		var result = converter.Convert("not a double", typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal(Brushes.Gray, result);
	}

	[Fact]
	public void FuelStatusConverter_ConvertBack_ThrowsNotImplementedException()
	{
		// Arrange
		var converter = new FuelStatusConverter();

		// Act & Assert
		Assert.Throws<NotImplementedException>(() =>
			converter.ConvertBack(Brushes.Green, typeof(double), null, CultureInfo.InvariantCulture));
	}

	#endregion

	#region TireTempConverter Tests

	[Fact]
	public void TireTempConverter_WithVeryColdTemp_ReturnsBlueBrush()
	{
		// Arrange
		var converter = new TireTempConverter();

		// Act
		var result = converter.Convert(50.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#00D9FF"), brush.Color);
	}

	[Fact]
	public void TireTempConverter_WithExactly70Degrees_ReturnsBlueBrush()
	{
		// Arrange
		var converter = new TireTempConverter();

		// Act
		var result = converter.Convert(70.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#00D9FF"), brush.Color);
	}

	[Fact]
	public void TireTempConverter_WithWarmingUpTemp_ReturnsInterpolatedBrush()
	{
		// Arrange
		var converter = new TireTempConverter();

		// Act - temperature between cold (70) and optimal min (85)
		var result = converter.Convert(77.5, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		// Should be interpolated between blue and green
		Assert.NotEqual(Color.Parse("#00D9FF"), brush.Color);
		Assert.NotEqual(Color.Parse("#00FF41"), brush.Color);
	}

	[Fact]
	public void TireTempConverter_WithExactly85Degrees_ReturnsGreenBrush()
	{
		// Arrange
		var converter = new TireTempConverter();

		// Act
		var result = converter.Convert(85.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#00FF41"), brush.Color);
	}

	[Fact]
	public void TireTempConverter_WithOptimalTemp_ReturnsGreenBrush()
	{
		// Arrange
		var converter = new TireTempConverter();

		// Act
		var result = converter.Convert(95.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#00FF41"), brush.Color);
	}

	[Fact]
	public void TireTempConverter_WithExactly105Degrees_ReturnsGreenBrush()
	{
		// Arrange
		var converter = new TireTempConverter();

		// Act
		var result = converter.Convert(105.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#00FF41"), brush.Color);
	}

	[Fact]
	public void TireTempConverter_WithGettingHotTemp_ReturnsInterpolatedBrush()
	{
		// Arrange
		var converter = new TireTempConverter();

		// Act - temperature between optimal max (105) and hot (120)
		var result = converter.Convert(112.5, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		// Should be interpolated between green and amber
		Assert.NotEqual(Color.Parse("#00FF41"), brush.Color);
		Assert.NotEqual(Color.Parse("#FFB800"), brush.Color);
	}

	[Fact]
	public void TireTempConverter_WithExactly120Degrees_ReturnsRedBrush()
	{
		// Arrange
		var converter = new TireTempConverter();

		// Act
		var result = converter.Convert(120.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#FF0033"), brush.Color);
	}

	[Fact]
	public void TireTempConverter_WithVeryHotTemp_ReturnsRedBrush()
	{
		// Arrange
		var converter = new TireTempConverter();

		// Act
		var result = converter.Convert(150.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#FF0033"), brush.Color);
	}

	[Fact]
	public void TireTempConverter_WithZeroTemp_ReturnsBlueBrush()
	{
		// Arrange
		var converter = new TireTempConverter();

		// Act
		var result = converter.Convert(0.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#00D9FF"), brush.Color);
	}

	[Fact]
	public void TireTempConverter_WithNegativeTemp_ReturnsBlueBrush()
	{
		// Arrange
		var converter = new TireTempConverter();

		// Act
		var result = converter.Convert(-10.0, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(result);
		Assert.IsType<SolidColorBrush>(result);
		var brush = (SolidColorBrush)result;
		Assert.Equal(Color.Parse("#00D9FF"), brush.Color);
	}

	[Fact]
	public void TireTempConverter_WithNull_ReturnsGrayBrush()
	{
		// Arrange
		var converter = new TireTempConverter();

		// Act
		var result = converter.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal(Brushes.Gray, result);
	}

	[Fact]
	public void TireTempConverter_WithInvalidType_ReturnsGrayBrush()
	{
		// Arrange
		var converter = new TireTempConverter();

		// Act
		var result = converter.Convert("not a double", typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal(Brushes.Gray, result);
	}

	[Fact]
	public void TireTempConverter_ConvertBack_ThrowsNotImplementedException()
	{
		// Arrange
		var converter = new TireTempConverter();

		// Act & Assert
		Assert.Throws<NotImplementedException>(() =>
			converter.ConvertBack(Brushes.Green, typeof(double), null, CultureInfo.InvariantCulture));
	}

	[Fact]
	public void TireTempConverter_BoundaryValues_ReturnCorrectColors()
	{
		// Arrange
		var converter = new TireTempConverter();

		// Act & Assert - Test all boundary values
		// < 70: Blue
		var result69 = converter.Convert(69.9, typeof(IBrush), null, CultureInfo.InvariantCulture);
		Assert.Equal(Color.Parse("#00D9FF"), ((SolidColorBrush)result69!).Color);

		// 70-85: Interpolated (blue to green)
		var result71 = converter.Convert(71.0, typeof(IBrush), null, CultureInfo.InvariantCulture);
		Assert.IsType<SolidColorBrush>(result71);

		// 85-105: Green
		var result86 = converter.Convert(86.0, typeof(IBrush), null, CultureInfo.InvariantCulture);
		Assert.Equal(Color.Parse("#00FF41"), ((SolidColorBrush)result86!).Color);

		var result104 = converter.Convert(104.0, typeof(IBrush), null, CultureInfo.InvariantCulture);
		Assert.Equal(Color.Parse("#00FF41"), ((SolidColorBrush)result104!).Color);

		// 105-120: Interpolated (green to amber)
		var result106 = converter.Convert(106.0, typeof(IBrush), null, CultureInfo.InvariantCulture);
		Assert.IsType<SolidColorBrush>(result106);

		// >= 120: Red
		var result121 = converter.Convert(121.0, typeof(IBrush), null, CultureInfo.InvariantCulture);
		Assert.Equal(Color.Parse("#FF0033"), ((SolidColorBrush)result121!).Color);
	}

	#endregion
}
