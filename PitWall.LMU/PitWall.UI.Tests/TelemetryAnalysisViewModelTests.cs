using System;
using System.Linq;
using PitWall.UI.Models;
using PitWall.UI.Services;
using PitWall.UI.ViewModels;
using Xunit;

namespace PitWall.UI.Tests
{
    public class TelemetryAnalysisViewModelTests
    {
        [Fact]
        public void Constructor_InitializesProperties()
        {
            var vm = new TelemetryAnalysisViewModel();
            
            Assert.Equal(1, vm.CurrentLap);
            Assert.False(vm.IsPlayingBack);
            Assert.True(double.IsNaN(vm.CursorPosition));
            Assert.Equal("No telemetry data available", vm.StatusMessage);
            Assert.Equal(9, vm.CursorData.Count);
        }

        [Fact]
        public void LoadCurrentLapData_WithValidData_PopulatesSeries()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 1, sampleCount: 100);
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.LoadCurrentLapData(1);
            
            Assert.Equal(100, vm.SpeedData.Count);
            Assert.Equal(100, vm.ThrottleData.Count);
            Assert.Equal(100, vm.BrakeData.Count);
            Assert.Equal(100, vm.SteeringData.Count);
            Assert.Equal(100, vm.TireTempData.Count);
            Assert.Contains("100 samples", vm.StatusMessage);
        }

        [Fact]
        public void LoadCurrentLapData_NoData_ClearsSeries()
        {
            var buffer = new TelemetryBuffer();
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.LoadCurrentLapData(5);
            
            Assert.Empty(vm.SpeedData);
            Assert.Contains("No data found", vm.StatusMessage);
        }

        [Fact]
        public void LoadCurrentLapData_NegativeLap_DoesNothing()
        {
            var buffer = new TelemetryBuffer();
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.LoadCurrentLapData(-1);
            
            Assert.Empty(vm.SpeedData);
        }

        [Fact]
        public void RefreshAvailableLaps_WithData_PopulatesLapsList()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 1, sampleCount: 10);
            AddSampleData(buffer, lapNumber: 2, sampleCount: 10);
            AddSampleData(buffer, lapNumber: 3, sampleCount: 10);
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.RefreshAvailableLaps();
            
            Assert.Equal(3, vm.AvailableLaps.Count);
            Assert.Contains(1, vm.AvailableLaps);
            Assert.Contains(2, vm.AvailableLaps);
            Assert.Contains(3, vm.AvailableLaps);
        }

        [Fact]
        public void RefreshAvailableLaps_NoData_ClearsList()
        {
            var buffer = new TelemetryBuffer();
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.RefreshAvailableLaps();
            
            Assert.Empty(vm.AvailableLaps);
            Assert.Contains("No telemetry", vm.StatusMessage);
        }

        [Fact]
        public void RefreshAvailableLaps_PreservesLapList()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 1, sampleCount: 10);
            AddSampleData(buffer, lapNumber: 2, sampleCount: 10);
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.RefreshAvailableLaps();
            
            Assert.Equal(2, vm.AvailableLaps.Count);
            Assert.Contains(1, vm.AvailableLaps);
            Assert.Contains(2, vm.AvailableLaps);
        }

        [Fact]
        public void RefreshAvailableLaps_WithNewData_UpdatesList()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 2, sampleCount: 10);
            AddSampleData(buffer, lapNumber: 3, sampleCount: 10);
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.RefreshAvailableLaps();
            
            Assert.Equal(2, vm.AvailableLaps.Count);
        }

        [Fact]
        public void UpdateCursorData_WithValidPosition_UpdatesTable()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 1, sampleCount: 100);
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.LoadCurrentLapData(1);
            
            vm.UpdateCursorData(0.5);
            
            Assert.Equal(0.5, vm.CursorPosition);
            Assert.NotEqual("--", vm.CursorData[0].CurrentValue);
        }

        [Fact]
        public void UpdateCursorData_ZeroValues_ShowsZero()
        {
            var buffer = new TelemetryBuffer();
            buffer.Add(new TelemetrySampleDto
            {
                LapNumber = 1,
                SpeedKph = 0,
                ThrottlePosition = 0,
                BrakePosition = 0,
                SteeringAngle = 0,
                TyreTempsC = new[] { 0.0, 0.0, 0.0, 0.0 },
                FuelLiters = 0
            });
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.LoadCurrentLapData(1);

            vm.UpdateCursorData(0.0);

            Assert.Equal("0.0", vm.CursorData[0].CurrentValue);
        }

        [Fact]
        public void UpdateCursorData_OutOfBounds_HandlesGracefully()
        {
            var buffer = new TelemetryBuffer();
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.UpdateCursorData(100.0);
            
            Assert.Equal(100.0, vm.CursorPosition);
        }

        [Fact]
        public void UpdateReplayCursor_WithSample_UpdatesCursorTable()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 1, sampleCount: 50, speedBase: 120);
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.LoadCurrentLapData(1);

            var sample = buffer.GetLapData(1)[10];
            vm.UpdateReplayCursor(sample);

            Assert.True(vm.CursorPosition >= 0);
            Assert.NotEqual("--", vm.CursorData[0].CurrentValue);
        }

        [Fact]
        public void UpdateReplayCursor_ShowsIndividualTireTemps()
        {
            var buffer = new TelemetryBuffer();
            buffer.Add(new TelemetrySampleDto
            {
                LapNumber = 1,
                SpeedKph = 200,
                ThrottlePosition = 1.0,
                BrakePosition = 0,
                SteeringAngle = 0,
                TyreTempsC = new[] { 85.0, 90.0, 82.0, 88.0 },
                FuelLiters = 50
            });
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.LoadCurrentLapData(1);

            var sample = buffer.GetLapData(1)[0];
            vm.UpdateReplayCursor(sample);

            Assert.Equal("85.0", vm.CursorData[4].CurrentValue); // FL
            Assert.Equal("90.0", vm.CursorData[5].CurrentValue); // FR
            Assert.Equal("82.0", vm.CursorData[6].CurrentValue); // RL
            Assert.Equal("88.0", vm.CursorData[7].CurrentValue); // RR
        }

        [Fact]
        public void GetSampleAtTime_WithValidLap_ReturnsSample()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 1, sampleCount: 100);
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.LoadCurrentLapData(1);
            
            var sample = vm.GetSampleAtTime(0.5);
            
            Assert.NotNull(sample);
        }

        [Fact]
        public void UpdateReplayCursor_DifferentLap_AutoSwitchesDisplayedLap()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 1, sampleCount: 50, speedBase: 120);
            AddSampleData(buffer, lapNumber: 2, sampleCount: 50, speedBase: 200);
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.LoadCurrentLapData(1);

            // Set cursor to a known position on lap 1
            vm.UpdateCursorData(0.1);

            // Replay moves to lap 2 — should auto-switch displayed lap
            var lap2Sample = buffer.GetLapData(2)[0];
            vm.UpdateReplayCursor(lap2Sample);

            // ViewModel should now display lap 2 with the cursor showing lap 2 data
            Assert.Equal(2, vm.CurrentLap);
            Assert.Equal("200.0", vm.CursorData[0].CurrentValue);
        }

        [Fact]
        public void UpdateReplayCursor_MouseHovering_DoesNotOverwriteCursor()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 1, sampleCount: 50, speedBase: 120);
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.LoadCurrentLapData(1);

            vm.UpdateCursorData(0.1);
            var speedBefore = vm.CursorData[0].CurrentValue;

            vm.IsMouseHovering = true;
            var sample = buffer.GetLapData(1)[25];
            vm.UpdateReplayCursor(sample);

            // Cursor should still reflect the hover value, not the replay sample
            Assert.Equal(speedBefore, vm.CursorData[0].CurrentValue);
        }

        [Fact]
        public void LapDisplay_UpdatesWhenCurrentLapChanges()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 1, sampleCount: 10);
            AddSampleData(buffer, lapNumber: 2, sampleCount: 10);
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.RefreshAvailableLaps();

            vm.LoadCurrentLapData(1);

            Assert.Equal("LAP 1 / 2", vm.LapDisplay);

            vm.LoadCurrentLapData(2);
            Assert.Equal("LAP 2 / 2", vm.LapDisplay);
        }

        [Fact]
        public void LapDisplay_DefaultsToLapDash()
        {
            var vm = new TelemetryAnalysisViewModel();
            Assert.Equal("LAP --", vm.LapDisplay);
        }

        [Fact]
        public void GetSampleAtTime_NoLap_ReturnsNull()
        {
            var buffer = new TelemetryBuffer();
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.CurrentLap = -1;
            
            var sample = vm.GetSampleAtTime(0.5);
            
            Assert.Null(sample);
        }

        [Fact]
        public void GetSampleAtTime_NoData_ReturnsNull()
        {
            var buffer = new TelemetryBuffer();
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.LoadCurrentLapData(1);
            
            var sample = vm.GetSampleAtTime(0.5);
            
            Assert.Null(sample);
        }

        [Fact]
        public async Task ExportToCsvAsync_WithData_CreatesFile()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 1, sampleCount: 10);
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.LoadCurrentLapData(1);
            var tempFile = System.IO.Path.GetTempFileName();
            
            try
            {
                await vm.ExportToCsvAsync(tempFile);
                
                Assert.True(System.IO.File.Exists(tempFile));
                var content = await System.IO.File.ReadAllTextAsync(tempFile);
                Assert.Contains("Speed(km/h)", content);
                Assert.Contains("Throttle(%)", content);
            }
            finally
            {
                try
                {
                    if (System.IO.File.Exists(tempFile))
                        System.IO.File.Delete(tempFile);
                }
                catch
                {
                    // Best-effort cleanup for temp files.
                }
            }
        }

        [Fact]
        public async Task ExportToCsvAsync_NoLap_UpdatesStatus()
        {
            var buffer = new TelemetryBuffer();
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.CurrentLap = -1;
            
            await vm.ExportToCsvAsync();
            
            Assert.Contains("No lap selected", vm.StatusMessage);
        }

        [Fact]
        public async Task ExportToCsvAsync_NoData_UpdatesStatus()
        {
            var buffer = new TelemetryBuffer();
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.LoadCurrentLapData(5);
            
            await vm.ExportToCsvAsync();
            
            Assert.Contains("No data found", vm.StatusMessage);
        }

        [Fact]
        public void UpdateTrackContext_UpdatesProperties()
        {
            var vm = new TelemetryAnalysisViewModel();
            var status = new TrackSegmentStatus
            {
                TrackName = "Spa-Francorchamps",
                SectorName = "Sector 2",
                CornerLabel = "Eau Rouge",
                SegmentType = "Corner"
            };
            
            vm.UpdateTrackContext(status);
            
            Assert.Equal("Spa-Francorchamps", vm.TrackName);
            Assert.Equal("Sector 2", vm.SectorLabel);
            Assert.Equal("Eau Rouge", vm.CornerLabel);
            Assert.Equal("Corner", vm.SegmentType);
        }

        [Fact]
        public void PreviousLapCommand_ExecutesWithoutError()
        {
            var vm = new TelemetryAnalysisViewModel();
            
            vm.PreviousLapCommand.Execute(null);
            
            // With no buffer data and default CurrentLap=1, navigates to
            // "Already on first available lap" because no laps are loaded.
            Assert.Contains("Already on first", vm.StatusMessage);
        }

        [Fact]
        public void NextLapCommand_ExecutesWithoutError()
        {
            var vm = new TelemetryAnalysisViewModel();
            
            vm.NextLapCommand.Execute(null);
            
            Assert.Contains("Already on last", vm.StatusMessage);
        }

        [Fact]
        public void ZoomInCommand_ExecutesWithoutError()
        {
            var vm = new TelemetryAnalysisViewModel();
            
            vm.ZoomInCommand.Execute(null);
            
            Assert.Contains("mouse wheel", vm.StatusMessage);
        }

        [Fact]
        public void ZoomOutCommand_ExecutesWithoutError()
        {
            var vm = new TelemetryAnalysisViewModel();
            
            vm.ZoomOutCommand.Execute(null);
            
            Assert.Contains("mouse wheel", vm.StatusMessage);
        }

        [Fact]
        public void DataSeriesUpdated_FiresWhenDataLoaded()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 1, sampleCount: 10);
            var vm = new TelemetryAnalysisViewModel(buffer);
            var fired = false;
            vm.DataSeriesUpdated += (s, e) => fired = true;
            
            vm.LoadCurrentLapData(1);
            
            Assert.True(fired);
        }

        [Fact]
        public void DataSeriesUpdated_FiresWhenDataCleared()
        {
            var buffer = new TelemetryBuffer();
            var vm = new TelemetryAnalysisViewModel(buffer);
            var fired = false;
            vm.DataSeriesUpdated += (s, e) => fired = true;
            
            vm.LoadCurrentLapData(99);
            
            Assert.True(fired);
        }

        [Fact]
        public void TelemetryDataSeries_TimeIncrementsCorrectly()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 1, sampleCount: 5);
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.LoadCurrentLapData(1);
            
            Assert.Equal(0.0, vm.SpeedData[0].Time);
            Assert.Equal(0.01, vm.SpeedData[1].Time);
            Assert.Equal(0.04, vm.SpeedData[4].Time);
        }

        [Fact]
        public void ThrottleData_ScaledToPercentage()
        {
            var buffer = new TelemetryBuffer();
            buffer.Add(new TelemetrySampleDto
            {
                LapNumber = 1,
                ThrottlePosition = 0.5,
                SpeedKph = 100,
                BrakePosition = 0,
                SteeringAngle = 0,
                TyreTempsC = new[] { 80.0, 80.0, 80.0, 80.0 }
            });
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.LoadCurrentLapData(1);
            
            Assert.Equal(50.0, vm.ThrottleData[0].Value);
        }

        [Fact]
        public void BrakeData_ScaledToPercentage()
        {
            var buffer = new TelemetryBuffer();
            buffer.Add(new TelemetrySampleDto
            {
                LapNumber = 1,
                BrakePosition = 0.75,
                SpeedKph = 100,
                ThrottlePosition = 0,
                SteeringAngle = 0,
                TyreTempsC = new[] { 80.0, 80.0, 80.0, 80.0 }
            });
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.LoadCurrentLapData(1);
            
            Assert.Equal(75.0, vm.BrakeData[0].Value);
        }

        [Fact]
        public void TireTempData_UsesAverage()
        {
            var buffer = new TelemetryBuffer();
            buffer.Add(new TelemetrySampleDto
            {
                LapNumber = 1,
                TyreTempsC = new[] { 80.0, 82.0, 84.0, 86.0 },
                SpeedKph = 100,
                ThrottlePosition = 0,
                BrakePosition = 0,
                SteeringAngle = 0
            });
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.LoadCurrentLapData(1);
            
            Assert.Equal(83.0, vm.TireTempData[0].Value);
        }

        [Fact]
        public void CursorDataRow_InitializedCorrectly()
        {
            var vm = new TelemetryAnalysisViewModel();
            
            var speedRow = vm.CursorData[0];
            Assert.Equal("vSpeed", speedRow.Parameter);
            Assert.Equal("km/h", speedRow.Unit);
            Assert.Equal("--", speedRow.CurrentValue);
        }

        [Fact]
        public void RefreshAvailableLaps_NoChangeInData_DoesNotUpdateCollection()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 1, sampleCount: 10);
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.RefreshAvailableLaps();
            var initialCount = vm.AvailableLaps.Count;
            
            vm.RefreshAvailableLaps();
            
            Assert.Equal(initialCount, vm.AvailableLaps.Count);
        }

        [Fact]
        public void LoadCurrentLapData_MultipleChannels_AllPopulated()
        {
            var buffer = new TelemetryBuffer();
            buffer.Add(new TelemetrySampleDto
            {
                LapNumber = 1,
                SpeedKph = 150.5,
                ThrottlePosition = 0.8,
                BrakePosition = 0.2,
                SteeringAngle = -15.5,
                TyreTempsC = new[] { 85.0, 86.0, 87.0, 88.0 },
                FuelLiters = 45.5
            });
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.LoadCurrentLapData(1);
            
            Assert.Equal(150.5, vm.SpeedData[0].Value);
            Assert.Equal(80.0, vm.ThrottleData[0].Value);
            Assert.Equal(20.0, vm.BrakeData[0].Value);
            Assert.Equal(-15.5, vm.SteeringData[0].Value);
            Assert.Equal(86.5, vm.TireTempData[0].Value);
        }

        private void AddSampleData(TelemetryBuffer buffer, int lapNumber, int sampleCount, double speedBase = 100.0)
        {
            var baseTime = new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < sampleCount; i++)
            {
                buffer.Add(new TelemetrySampleDto
                {
                    LapNumber = lapNumber,
                    SpeedKph = speedBase + i,
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
}
