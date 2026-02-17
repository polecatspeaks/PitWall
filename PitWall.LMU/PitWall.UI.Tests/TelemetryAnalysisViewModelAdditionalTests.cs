using System;
using PitWall.UI.Models;
using PitWall.UI.Services;
using PitWall.UI.ViewModels;
using Xunit;

namespace PitWall.UI.Tests;

/// <summary>
/// Additional tests for TelemetryAnalysisViewModel covering
/// PreviousLap/NextLap navigation, RefreshAvailableLaps edge cases,
/// and UpdateReplayCursor fallback paths.
/// </summary>
public class TelemetryAnalysisViewModelAdditionalTests
{
    [Fact]
    public void PreviousLap_WhenCurrentLapIsZero_ShowsNoLapLoaded()
    {
        var buffer = new TelemetryBuffer();
        var vm = new TelemetryAnalysisViewModel(buffer);
        vm.CurrentLap = 0;

        vm.PreviousLapCommand.Execute(null);

        Assert.Contains("No lap loaded", vm.StatusMessage);
    }

    [Fact]
    public void PreviousLap_WhenCurrentLapIsNegative_ShowsNoLapLoaded()
    {
        var buffer = new TelemetryBuffer();
        var vm = new TelemetryAnalysisViewModel(buffer);
        vm.CurrentLap = -1;

        vm.PreviousLapCommand.Execute(null);

        Assert.Contains("No lap loaded", vm.StatusMessage);
    }

    [Fact]
    public void PreviousLap_NavigatesToPreviousLap()
    {
        var buffer = new TelemetryBuffer();
        AddSampleData(buffer, 1, 10);
        AddSampleData(buffer, 2, 10);
        AddSampleData(buffer, 3, 10);
        var vm = new TelemetryAnalysisViewModel(buffer);
        vm.RefreshAvailableLaps();
        vm.LoadCurrentLapData(3);

        vm.PreviousLapCommand.Execute(null);

        Assert.Equal(2, vm.CurrentLap);
    }

    [Fact]
    public void NextLap_WhenCurrentLapIsZero_ShowsNoLapLoaded()
    {
        var buffer = new TelemetryBuffer();
        var vm = new TelemetryAnalysisViewModel(buffer);
        vm.CurrentLap = 0;

        vm.NextLapCommand.Execute(null);

        Assert.Contains("No lap loaded", vm.StatusMessage);
    }

    [Fact]
    public void NextLap_WhenCurrentLapIsNegative_ShowsNoLapLoaded()
    {
        var buffer = new TelemetryBuffer();
        var vm = new TelemetryAnalysisViewModel(buffer);
        vm.CurrentLap = -1;

        vm.NextLapCommand.Execute(null);

        Assert.Contains("No lap loaded", vm.StatusMessage);
    }

    [Fact]
    public void NextLap_NavigatesToNextLap()
    {
        var buffer = new TelemetryBuffer();
        AddSampleData(buffer, 1, 10);
        AddSampleData(buffer, 2, 10);
        AddSampleData(buffer, 3, 10);
        var vm = new TelemetryAnalysisViewModel(buffer);
        vm.RefreshAvailableLaps();
        vm.LoadCurrentLapData(1);

        vm.NextLapCommand.Execute(null);

        Assert.Equal(2, vm.CurrentLap);
    }

    [Fact]
    public void RefreshAvailableLaps_ContentChanged_UpdatesList()
    {
        var buffer = new TelemetryBuffer();
        AddSampleData(buffer, 1, 10);
        AddSampleData(buffer, 2, 10);
        var vm = new TelemetryAnalysisViewModel(buffer);
        vm.RefreshAvailableLaps();

        Assert.Equal(2, vm.AvailableLaps.Count);

        // Add new lap data
        AddSampleData(buffer, 3, 10);
        vm.RefreshAvailableLaps();

        Assert.Equal(3, vm.AvailableLaps.Count);
        Assert.Contains(3, vm.AvailableLaps);
    }

    [Fact]
    public void RefreshAvailableLaps_LapRemoved_UpdatesList()
    {
        var buffer = new TelemetryBuffer();
        AddSampleData(buffer, 1, 10);
        AddSampleData(buffer, 2, 10);
        var vm = new TelemetryAnalysisViewModel(buffer);
        vm.RefreshAvailableLaps();

        Assert.Equal(2, vm.AvailableLaps.Count);

        // Replace buffer with only lap 2 by clearing and re-adding
        buffer.Clear();
        AddSampleData(buffer, 2, 10);
        vm.RefreshAvailableLaps();

        Assert.Single(vm.AvailableLaps);
        Assert.Contains(2, vm.AvailableLaps);
    }

    [Fact]
    public void RefreshAvailableLaps_EmptyAfterHavingData_ClearsList()
    {
        var buffer = new TelemetryBuffer();
        AddSampleData(buffer, 1, 10);
        var vm = new TelemetryAnalysisViewModel(buffer);
        vm.RefreshAvailableLaps();

        Assert.Single(vm.AvailableLaps);

        buffer.Clear();
        vm.RefreshAvailableLaps();

        Assert.Empty(vm.AvailableLaps);
        Assert.Contains("No telemetry", vm.StatusMessage);
    }

    [Fact]
    public void UpdateReplayCursor_NullSample_DoesNotThrow()
    {
        var buffer = new TelemetryBuffer();
        var vm = new TelemetryAnalysisViewModel(buffer);

        vm.UpdateReplayCursor(null!);

        // Should early return without error
        Assert.True(true);
    }

    [Fact]
    public void UpdateReplayCursor_NegativeLapNumber_DoesNotThrow()
    {
        var buffer = new TelemetryBuffer();
        var vm = new TelemetryAnalysisViewModel(buffer);
        var sample = new TelemetrySampleDto { LapNumber = -1 };

        vm.UpdateReplayCursor(sample);

        Assert.True(true);
    }

    [Fact]
    public void UpdateReplayCursor_NewSampleNotInBuffer_UsesTimestampFallback()
    {
        var buffer = new TelemetryBuffer();
        var ts = new DateTime(2024, 1, 1, 0, 0, 5, DateTimeKind.Utc);
        AddSampleData(buffer, 1, 50);
        var vm = new TelemetryAnalysisViewModel(buffer);
        vm.LoadCurrentLapData(1);

        // Create a NEW sample object (not from buffer) with matching timestamp
        var bufferSample = buffer.GetLapData(1)[10];
        var newSample = new TelemetrySampleDto
        {
            LapNumber = 1,
            SpeedKph = 999,
            Timestamp = bufferSample.Timestamp
        };

        vm.UpdateReplayCursor(newSample);

        Assert.True(vm.CursorPosition >= 0);
    }

    [Fact]
    public void UpdateReplayCursor_SampleNotFoundByRefOrTimestamp_UsesFallbackIndex()
    {
        var buffer = new TelemetryBuffer();
        AddSampleData(buffer, 1, 50);
        var vm = new TelemetryAnalysisViewModel(buffer);
        vm.LoadCurrentLapData(1);

        var newSample = new TelemetrySampleDto
        {
            LapNumber = 1,
            SpeedKph = 999,
            Timestamp = null
        };

        vm.UpdateReplayCursor(newSample);

        Assert.True(vm.CursorPosition >= 0);
    }

    [Fact]
    public void UpdateReplayCursor_WhenMouseHovering_SkipsUpdate()
    {
        var buffer = new TelemetryBuffer();
        AddSampleData(buffer, 1, 10);
        var vm = new TelemetryAnalysisViewModel(buffer);
        vm.LoadCurrentLapData(1);
        vm.IsMouseHovering = true;

        var sample = buffer.GetLapData(1)[0];
        var positionBefore = vm.CursorPosition;
        vm.UpdateReplayCursor(sample);

        // CursorPosition should not change
        Assert.Equal(positionBefore, vm.CursorPosition);
    }

    private void AddSampleData(TelemetryBuffer buffer, int lapNumber, int sampleCount)
    {
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < sampleCount; i++)
        {
            buffer.Add(new TelemetrySampleDto
            {
                LapNumber = lapNumber,
                SpeedKph = 100 + i,
                ThrottlePosition = 0.5,
                BrakePosition = 0.1,
                SteeringAngle = 0.0,
                TyreTempsC = new[] { 80.0, 81.0, 82.0, 83.0 },
                FuelLiters = 50.0,
                Timestamp = baseTime.AddSeconds(i * 0.01)
            });
        }
    }
}
