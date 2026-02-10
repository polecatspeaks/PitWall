using System;
using System.Globalization;
using Avalonia.Layout;
using Avalonia.Media;
using Xunit;
using PitWall.UI.Converters;

namespace PitWall.UI.Tests;

/// <summary>
/// Tests for XAML value converters used in the ATLAS UI.
/// </summary>
public class ConverterTests
{
	[Fact]
	public void BoolToTextConverter_WithTrueValue_ReturnsFirstText()
	{
		// Arrange
		var converter = new BoolToTextConverter();
		var parameter = "Fuel Save: ON|Fuel Save: OFF";

		// Act
		var result = converter.Convert(true, typeof(string), parameter, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal("Fuel Save: ON", result);
	}

	[Fact]
	public void BoolToTextConverter_WithFalseValue_ReturnsSecondText()
	{
		// Arrange
		var converter = new BoolToTextConverter();
		var parameter = "Fuel Save: ON|Fuel Save: OFF";

		// Act
		var result = converter.Convert(false, typeof(string), parameter, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal("Fuel Save: OFF", result);
	}

	[Fact]
	public void BoolToAlignmentUserConverter_WithTrue_ReturnsRight()
	{
		// Arrange
		var converter = new BoolToAlignmentUserConverter();

		// Act
		var result = converter.Convert(true, typeof(HorizontalAlignment), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal(HorizontalAlignment.Right, result);
	}

	[Fact]
	public void BoolToAlignmentUserConverter_WithFalse_ReturnsLeft()
	{
		// Arrange
		var converter = new BoolToAlignmentUserConverter();

		// Act
		var result = converter.Convert(false, typeof(HorizontalAlignment), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal(HorizontalAlignment.Left, result);
	}

	[Fact]
	public void BoolToMessageBackgroundConverter_ReturnsValidBrush()
	{
		// Arrange
		var converter = new BoolToMessageBackgroundConverter();

		// Act - Test both user and assistant
		var userBrush = converter.Convert(true, typeof(IBrush), null, CultureInfo.InvariantCulture);
		var assistantBrush = converter.Convert(false, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(userBrush);
		Assert.NotNull(assistantBrush);
		Assert.IsType<SolidColorBrush>(userBrush);
		Assert.IsType<SolidColorBrush>(assistantBrush);
	}

	[Fact]
	public void BoolToMessageBorderConverter_ReturnsValidBrush()
	{
		// Arrange
		var converter = new BoolToMessageBorderConverter();

		// Act - Test both user and assistant
		var userBrush = converter.Convert(true, typeof(IBrush), null, CultureInfo.InvariantCulture);
		var assistantBrush = converter.Convert(false, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(userBrush);
		Assert.NotNull(assistantBrush);
		Assert.IsType<SolidColorBrush>(userBrush);
		Assert.IsType<SolidColorBrush>(assistantBrush);
	}

	[Fact]
	public void BoolToRoleColorConverter_ReturnsValidBrush()
	{
		// Arrange
		var converter = new BoolToRoleColorConverter();

		// Act - Test both user and assistant
		var userBrush = converter.Convert(true, typeof(IBrush), null, CultureInfo.InvariantCulture);
		var assistantBrush = converter.Convert(false, typeof(IBrush), null, CultureInfo.InvariantCulture);

		// Assert
		Assert.NotNull(userBrush);
		Assert.NotNull(assistantBrush);
		Assert.IsType<SolidColorBrush>(userBrush);
		Assert.IsType<SolidColorBrush>(assistantBrush);
	}

	[Fact]
	public void BoolToTextConverter_WithInvalidParameter_ReturnsEmpty()
	{
		// Arrange
		var converter = new BoolToTextConverter();
		var invalidParameter = "NoSeparator";

		// Act
		var result = converter.Convert(true, typeof(string), invalidParameter, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal(string.Empty, result);
	}

	[Fact]
	public void BoolToTextConverter_WithMultiplePipeOptions_UsesSplitCorrectly()
	{
		// Arrange
		var converter = new BoolToTextConverter();
		var parameter = "ON|OFF";

		// Act
		var resultTrue = converter.Convert(true, typeof(string), parameter, CultureInfo.InvariantCulture);
		var resultFalse = converter.Convert(false, typeof(string), parameter, CultureInfo.InvariantCulture);

		// Assert
		Assert.Equal("ON", resultTrue);
		Assert.Equal("OFF", resultFalse);
	}
}
