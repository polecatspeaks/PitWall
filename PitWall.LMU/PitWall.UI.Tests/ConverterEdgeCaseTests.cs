using System;
using System.Globalization;
using Avalonia.Layout;
using Avalonia.Media;
using PitWall.UI.Converters;
using Xunit;

namespace PitWall.UI.Tests;

/// <summary>
/// Tests for converter edge cases: non-bool inputs and ConvertBack methods.
/// </summary>
public class ConverterEdgeCaseTests
{
    // --- BoolToAlignmentUserConverter ---

    [Fact]
    public void BoolToAlignmentUserConverter_NonBoolValue_ReturnsLeft()
    {
        var converter = new BoolToAlignmentUserConverter();
        var result = converter.Convert("not a bool", typeof(HorizontalAlignment), null, CultureInfo.InvariantCulture);
        Assert.Equal(HorizontalAlignment.Left, result);
    }

    [Fact]
    public void BoolToAlignmentUserConverter_ConvertBack_ThrowsNotImplemented()
    {
        var converter = new BoolToAlignmentUserConverter();
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(HorizontalAlignment.Right, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    // --- BoolToMessageBackgroundConverter ---

    [Fact]
    public void BoolToMessageBackgroundConverter_NonBoolValue_ReturnsAssistantBackground()
    {
        var converter = new BoolToMessageBackgroundConverter();
        var result = converter.Convert(42, typeof(IBrush), null, CultureInfo.InvariantCulture);
        Assert.NotNull(result);
    }

    [Fact]
    public void BoolToMessageBackgroundConverter_ConvertBack_ThrowsNotImplemented()
    {
        var converter = new BoolToMessageBackgroundConverter();
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(null, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    // --- BoolToMessageBorderConverter ---

    [Fact]
    public void BoolToMessageBorderConverter_NonBoolValue_ReturnsAssistantBorder()
    {
        var converter = new BoolToMessageBorderConverter();
        var result = converter.Convert("text", typeof(IBrush), null, CultureInfo.InvariantCulture);
        Assert.NotNull(result);
    }

    [Fact]
    public void BoolToMessageBorderConverter_ConvertBack_ThrowsNotImplemented()
    {
        var converter = new BoolToMessageBorderConverter();
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(null, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    // --- BoolToRoleColorConverter ---

    [Fact]
    public void BoolToRoleColorConverter_NonBoolValue_ReturnsAssistantColor()
    {
        var converter = new BoolToRoleColorConverter();
        var result = converter.Convert(3.14, typeof(IBrush), null, CultureInfo.InvariantCulture);
        Assert.NotNull(result);
    }

    [Fact]
    public void BoolToRoleColorConverter_ConvertBack_ThrowsNotImplemented()
    {
        var converter = new BoolToRoleColorConverter();
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(null, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    // --- BoolToTextConverter ---

    [Fact]
    public void BoolToTextConverter_NonBoolValue_ReturnsEmpty()
    {
        var converter = new BoolToTextConverter();
        var result = converter.Convert("not bool", typeof(string), "Yes|No", CultureInfo.InvariantCulture);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BoolToTextConverter_NullParameter_ReturnsEmpty()
    {
        var converter = new BoolToTextConverter();
        var result = converter.Convert(true, typeof(string), null, CultureInfo.InvariantCulture);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BoolToTextConverter_ConvertBack_ThrowsNotImplemented()
    {
        var converter = new BoolToTextConverter();
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack("Yes", typeof(bool), "Yes|No", CultureInfo.InvariantCulture));
    }
}
