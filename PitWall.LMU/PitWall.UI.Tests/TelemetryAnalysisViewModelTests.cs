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
            Assert.Equal(0, vm.CursorPosition);
            Assert.Null(vm.SelectedReferenceLap);
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
        public void LoadReferenceLapData_WithValidData_PopulatesReferenceSeries()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 2, sampleCount: 50);
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.LoadReferenceLapData(2);
            
            Assert.Equal(50, vm.ReferenceSpeedData.Count);
            Assert.Equal(50, vm.ReferenceThrottleData.Count);
            Assert.Equal(50, vm.ReferenceBrakeData.Count);
            Assert.Equal(50, vm.ReferenceSteeringData.Count);
            Assert.Equal(50, vm.ReferenceTireTempData.Count);
        }

        [Fact]
        public void LoadReferenceLapData_NegativeLap_DoesNothing()
        {
            var buffer = new TelemetryBuffer();
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.LoadReferenceLapData(-1);
            
            Assert.Empty(vm.ReferenceSpeedData);
        }

        [Fact]
        public void SelectedReferenceLap_WhenSet_LoadsData()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 3, sampleCount: 75);
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.SelectedReferenceLap = 3;
            
            Assert.Equal(75, vm.ReferenceSpeedData.Count);
        }

        [Fact]
        public void SelectedReferenceLap_NegativeValue_ClearsData()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 3, sampleCount: 75);
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.SelectedReferenceLap = 3;
            
            vm.SelectedReferenceLap = -1;
            
            Assert.Empty(vm.ReferenceSpeedData);
        }

        [Fact]
        public void SelectedReferenceLap_Null_ClearsData()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 3, sampleCount: 75);
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.SelectedReferenceLap = 3;
            
            vm.SelectedReferenceLap = null;
            
            Assert.Empty(vm.ReferenceSpeedData);
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
        public void RefreshAvailableLaps_PreservesSelection()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 1, sampleCount: 10);
            AddSampleData(buffer, lapNumber: 2, sampleCount: 10);
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.SelectedReferenceLap = 1;
            
            vm.RefreshAvailableLaps();
            
            Assert.Equal(1, vm.SelectedReferenceLap);
        }

        [Fact]
        public void RefreshAvailableLaps_InvalidSelection_SetsFirstLap()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 2, sampleCount: 10);
            AddSampleData(buffer, lapNumber: 3, sampleCount: 10);
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.SelectedReferenceLap = 1;
            
            vm.RefreshAvailableLaps();
            
            Assert.Equal(2, vm.SelectedReferenceLap);
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
        public void UpdateCursorData_WithReferenceData_ShowsDelta()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 1, sampleCount: 100, speedBase: 100);
            AddSampleData(buffer, lapNumber: 2, sampleCount: 100, speedBase: 110);
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.LoadCurrentLapData(1);
            vm.LoadReferenceLapData(2);
            
            vm.UpdateCursorData(0.5);
            
            var speedRow = vm.CursorData[0];
            Assert.NotEqual("--", speedRow.Delta);
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
        public async void ExportToCsvAsync_WithData_CreatesFile()
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
                if (System.IO.File.Exists(tempFile))
                    System.IO.File.Delete(tempFile);
            }
        }

        [Fact]
        public async void ExportToCsvAsync_NoLap_UpdatesStatus()
        {
            var buffer = new TelemetryBuffer();
            var vm = new TelemetryAnalysisViewModel(buffer);
            vm.CurrentLap = -1;
            
            await vm.ExportToCsvAsync();
            
            Assert.Contains("No lap selected", vm.StatusMessage);
        }

        [Fact]
        public async void ExportToCsvAsync_NoData_UpdatesStatus()
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
        public void SelectReferenceLapCommand_ValidLap_SetsProperty()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 5, sampleCount: 10);
            var vm = new TelemetryAnalysisViewModel(buffer);
            
            vm.SelectReferenceLapCommand.Execute(5);
            
            Assert.Equal(5, vm.SelectedReferenceLap);
        }

        [Fact]
        public void SelectReferenceLapCommand_NegativeLap_DoesNothing()
        {
            var vm = new TelemetryAnalysisViewModel();
            vm.SelectedReferenceLap = 1;
            
            vm.SelectReferenceLapCommand.Execute(-1);
            
            Assert.Equal(1, vm.SelectedReferenceLap);
        }

        [Fact]
        public void PreviousSectorCommand_ExecutesWithoutError()
        {
            var vm = new TelemetryAnalysisViewModel();
            
            vm.PreviousSectorCommand.Execute(null);
            
            Assert.Contains("not yet implemented", vm.StatusMessage);
        }

        [Fact]
        public void NextSectorCommand_ExecutesWithoutError()
        {
            var vm = new TelemetryAnalysisViewModel();
            
            vm.NextSectorCommand.Execute(null);
            
            Assert.Contains("not yet implemented", vm.StatusMessage);
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
        public void DataSeriesUpdated_FiresWhenReferenceDataLoaded()
        {
            var buffer = new TelemetryBuffer();
            AddSampleData(buffer, lapNumber: 2, sampleCount: 10);
            var vm = new TelemetryAnalysisViewModel(buffer);
            var fired = false;
            vm.DataSeriesUpdated += (s, e) => fired = true;
            
            vm.LoadReferenceLapData(2);
            
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
            Assert.Equal("--", speedRow.ReferenceValue);
            Assert.Equal("--", speedRow.Delta);
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
                    Timestamp = DateTime.UtcNow.AddSeconds(i * 0.01)
                });
            }
        }
    }
}
