using System;
using PitWall.UI.Models;
using PitWall.UI.ViewModels;
using Xunit;

namespace PitWall.UI.Tests
{
    public class DashboardViewModelComprehensiveTests
    {
        [Fact]
        public void Constructor_InitializesWithDefaultValues()
        {
            // Act
            var vm = new DashboardViewModel();

            // Assert
            Assert.Equal("-- L", vm.FuelLiters);
            Assert.Equal("-- LAPS", vm.FuelLaps);
            Assert.Equal(0, vm.FuelPercentage);
            Assert.Equal("-- L/lap", vm.FuelConsumptionRate);
            Assert.Equal("-- KPH", vm.SpeedDisplay);
            Assert.Equal("--%", vm.ThrottleDisplay);
            Assert.Equal("--%", vm.BrakeDisplay);
            Assert.Equal("--", vm.SteeringDisplay);
            Assert.Equal("Awaiting strategy...", vm.StrategyMessage);
            Assert.Equal(0, vm.StrategyConfidence);
            Assert.Equal(0, vm.NextPitLap);
            Assert.Equal("--:--.---", vm.LastLapTime);
            Assert.Equal("--:--.---", vm.BestLapTime);
            Assert.Equal("+-.---", vm.LapTimeDelta);
            Assert.Equal("CLEAR", vm.WeatherConditions);
            Assert.Equal(0, vm.TrackTempC);
            Assert.Equal(100.0, vm.GripPercentage);
            Assert.Equal(0, vm.CurrentLap);
            Assert.Equal(30, vm.TotalLaps);
            Assert.Equal(1, vm.Position);
            Assert.Equal("--", vm.GapToLeader);
            Assert.Equal("TRACK", vm.TrackName);
            Assert.Equal("--", vm.CurrentSector);
            Assert.Equal("--", vm.CurrentCorner);
            Assert.Equal("--", vm.CurrentSegment);
            Assert.Equal("CAR", vm.CarName);
            Assert.Equal("--", vm.CarClass);
            Assert.Equal("--", vm.CarPower);
            Assert.Equal("--", vm.CarWeight);
            Assert.Equal("--", vm.CarDimensions);
        }

        [Fact]
        public void Constructor_InitializesCollections()
        {
            // Act
            var vm = new DashboardViewModel();

            // Assert
            Assert.NotNull(vm.Alerts);
            Assert.Empty(vm.Alerts);
            Assert.NotNull(vm.RelativePositions);
            Assert.Empty(vm.RelativePositions);
            Assert.NotNull(vm.Standings);
            Assert.Empty(vm.Standings);
        }

        #region UpdateTelemetry Tests

        [Fact]
        public void UpdateTelemetry_UpdatesFuelDisplay()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry = new TelemetrySampleDto
            {
                FuelLiters = 45.7
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert
            Assert.Equal("45.7 L", vm.FuelLiters);
        }

        [Fact]
        public void UpdateTelemetry_UpdatesSpeedDisplay()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry = new TelemetrySampleDto
            {
                SpeedKph = 235.8
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert
            Assert.Equal("235.8 KPH", vm.SpeedDisplay);
        }

        [Fact]
        public void UpdateTelemetry_UpdatesThrottle_NormalRange()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry = new TelemetrySampleDto
            {
                ThrottlePosition = 0.75
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert
            Assert.Equal(75, vm.ThrottlePercent);
            Assert.Equal("75%", vm.ThrottleDisplay);
        }

        [Fact]
        public void UpdateTelemetry_ClampThrottle_AboveMax()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry = new TelemetrySampleDto
            {
                ThrottlePosition = 1.5
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert
            Assert.Equal(100, vm.ThrottlePercent);
            Assert.Equal("100%", vm.ThrottleDisplay);
        }

        [Fact]
        public void UpdateTelemetry_ClampThrottle_BelowMin()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry = new TelemetrySampleDto
            {
                ThrottlePosition = -0.5
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert
            Assert.Equal(0, vm.ThrottlePercent);
            Assert.Equal("0%", vm.ThrottleDisplay);
        }

        [Fact]
        public void UpdateTelemetry_UpdatesBrake_NormalRange()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry = new TelemetrySampleDto
            {
                BrakePosition = 0.85
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert
            Assert.Equal(85, vm.BrakePercent);
            Assert.Equal("85%", vm.BrakeDisplay);
        }

        [Fact]
        public void UpdateTelemetry_ClampBrake_AboveMax()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry = new TelemetrySampleDto
            {
                BrakePosition = 2.0
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert
            Assert.Equal(100, vm.BrakePercent);
            Assert.Equal("100%", vm.BrakeDisplay);
        }

        [Fact]
        public void UpdateTelemetry_ClampBrake_BelowMin()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry = new TelemetrySampleDto
            {
                BrakePosition = -0.2
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert
            Assert.Equal(0, vm.BrakePercent);
            Assert.Equal("0%", vm.BrakeDisplay);
        }

        [Fact]
        public void UpdateTelemetry_UpdatesSteering_Centered()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry = new TelemetrySampleDto
            {
                SteeringAngle = 0.0
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert
            Assert.Equal(50, vm.SteeringPercent);
            Assert.Equal("0.00", vm.SteeringDisplay);
        }

        [Fact]
        public void UpdateTelemetry_UpdatesSteering_FullLeft()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry = new TelemetrySampleDto
            {
                SteeringAngle = -1.0
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert
            Assert.Equal(0, vm.SteeringPercent);
            Assert.Equal("-1.00", vm.SteeringDisplay);
        }

        [Fact]
        public void UpdateTelemetry_UpdatesSteering_FullRight()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry = new TelemetrySampleDto
            {
                SteeringAngle = 1.0
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert
            Assert.Equal(100, vm.SteeringPercent);
            Assert.Equal("1.00", vm.SteeringDisplay);
        }

        [Fact]
        public void UpdateTelemetry_ClampSteering_AboveMax()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry = new TelemetrySampleDto
            {
                SteeringAngle = 1.5
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert
            Assert.Equal(100, vm.SteeringPercent);
        }

        [Fact]
        public void UpdateTelemetry_ClampSteering_BelowMin()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry = new TelemetrySampleDto
            {
                SteeringAngle = -1.5
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert
            Assert.Equal(0, vm.SteeringPercent);
        }

        [Fact]
        public void UpdateTelemetry_UpdatesTireTemperatures_AllFourTires()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry = new TelemetrySampleDto
            {
                TyreTempsC = new[] { 85.5, 87.2, 83.9, 86.1 }
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert
            Assert.Equal(85.5, vm.TireFLTemp);
            Assert.Equal(87.2, vm.TireFRTemp);
            Assert.Equal(83.9, vm.TireRLTemp);
            Assert.Equal(86.1, vm.TireRRTemp);
        }

        [Fact]
        public void UpdateTelemetry_HandlesMissingTireTemperatures()
        {
            // Arrange
            var vm = new DashboardViewModel();
            vm.TireFLTemp = 50;
            vm.TireFRTemp = 50;
            vm.TireRLTemp = 50;
            vm.TireRRTemp = 50;
            
            var telemetry = new TelemetrySampleDto
            {
                TyreTempsC = new double[0]
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert - temperatures should not be updated
            Assert.Equal(50, vm.TireFLTemp);
            Assert.Equal(50, vm.TireFRTemp);
            Assert.Equal(50, vm.TireRLTemp);
            Assert.Equal(50, vm.TireRRTemp);
        }

        [Fact]
        public void UpdateTelemetry_HandlesPartialTireData()
        {
            // Arrange
            var vm = new DashboardViewModel();
            vm.TireFLTemp = 50;
            vm.TireFRTemp = 50;
            vm.TireRLTemp = 50;
            vm.TireRRTemp = 50;
            
            var telemetry = new TelemetrySampleDto
            {
                TyreTempsC = new[] { 85.0, 86.0 }
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert - should not update if less than 4 values
            Assert.Equal(50, vm.TireFLTemp);
        }

        [Fact]
        public void UpdateTelemetry_ZeroValues_DisplaysZero()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry = new TelemetrySampleDto
            {
                FuelLiters = 0.0,
                SpeedKph = 0.0,
                ThrottlePosition = 0.0,
                BrakePosition = 0.0,
                SteeringAngle = 0.0,
                TyreTempsC = new[] { 0.0, 0.0, 0.0, 0.0 }
            };

            // Act
            vm.UpdateTelemetry(telemetry);

            // Assert
            Assert.Equal("0.0 L", vm.FuelLiters);
            Assert.Equal("0.0 KPH", vm.SpeedDisplay);
            Assert.Equal("0%", vm.ThrottleDisplay);
            Assert.Equal("0%", vm.BrakeDisplay);
            Assert.Equal("0.00", vm.SteeringDisplay);
        }

        #endregion

        #region UpdateRecommendation Tests

        [Fact]
        public void UpdateRecommendation_UpdatesStrategyMessage()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var recommendation = new RecommendationDto
            {
                Recommendation = "Pit on lap 15 for fuel",
                Confidence = 0.85
            };

            // Act
            vm.UpdateRecommendation(recommendation);

            // Assert
            Assert.Equal("Pit on lap 15 for fuel", vm.StrategyMessage);
            Assert.Equal(0.85, vm.StrategyConfidence);
        }

        [Fact]
        public void UpdateRecommendation_EmptyMessage_ShowsDefault()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var recommendation = new RecommendationDto
            {
                Recommendation = "",
                Confidence = 0.5
            };

            // Act
            vm.UpdateRecommendation(recommendation);

            // Assert
            Assert.Equal("Awaiting strategy...", vm.StrategyMessage);
            Assert.Equal(0.5, vm.StrategyConfidence);
        }

        [Fact]
        public void UpdateRecommendation_NullMessage_ShowsDefault()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var recommendation = new RecommendationDto
            {
                Recommendation = null,
                Confidence = 0.7
            };

            // Act
            vm.UpdateRecommendation(recommendation);

            // Assert
            Assert.Equal("Awaiting strategy...", vm.StrategyMessage);
            Assert.Equal(0.7, vm.StrategyConfidence);
        }

        [Fact]
        public void UpdateRecommendation_WhitespaceMessage_ShowsDefault()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var recommendation = new RecommendationDto
            {
                Recommendation = "   ",
                Confidence = 0.6
            };

            // Act
            vm.UpdateRecommendation(recommendation);

            // Assert
            Assert.Equal("Awaiting strategy...", vm.StrategyMessage);
        }

        #endregion

        #region UpdateTrackContext Tests

        [Fact]
        public void UpdateTrackContext_UpdatesAllProperties()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var status = new TrackSegmentStatus
            {
                TrackName = "Monza",
                SectorName = "Sector 2",
                CornerLabel = "Lesmo 1",
                SegmentType = "Fast Corner"
            };

            // Act
            vm.UpdateTrackContext(status);

            // Assert
            Assert.Equal("Monza", vm.TrackName);
            Assert.Equal("Sector 2", vm.CurrentSector);
            Assert.Equal("Lesmo 1", vm.CurrentCorner);
            Assert.Equal("Fast Corner", vm.CurrentSegment);
        }

        [Fact]
        public void UpdateTrackContext_HandlesEmptyStrings()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var status = new TrackSegmentStatus
            {
                TrackName = "",
                SectorName = "",
                CornerLabel = "",
                SegmentType = ""
            };

            // Act
            vm.UpdateTrackContext(status);

            // Assert
            Assert.Equal("", vm.TrackName);
            Assert.Equal("", vm.CurrentSector);
            Assert.Equal("", vm.CurrentCorner);
            Assert.Equal("", vm.CurrentSegment);
        }

        #endregion

        #region UpdateCarSpec Tests

        [Fact]
        public void UpdateCarSpec_FullSpec_UpdatesAllProperties()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var spec = new CarSpec
            {
                Name = "Ferrari 488 GT3",
                Category = "GT3",
                Power = "550 HP",
                WeightKg = 1250,
                LengthMm = 4700,
                WidthMm = 1950,
                HeightMm = 1230
            };

            // Act
            vm.UpdateCarSpec(spec, null);

            // Assert
            Assert.Equal("Ferrari 488 GT3", vm.CarName);
            Assert.Equal("GT3", vm.CarClass);
            Assert.Equal("550 HP", vm.CarPower);
            Assert.Equal("1250kg", vm.CarWeight);
            Assert.Equal("L 4700mm W 1950mm H 1230mm", vm.CarDimensions);
        }

        [Fact]
        public void UpdateCarSpec_NullSpec_UsesFallbackName()
        {
            // Arrange
            var vm = new DashboardViewModel();

            // Act
            vm.UpdateCarSpec(null, "McLaren 720S");

            // Assert
            Assert.Equal("McLaren 720S", vm.CarName);
            Assert.Equal("--", vm.CarClass);
            Assert.Equal("--", vm.CarPower);
            Assert.Equal("--", vm.CarWeight);
            Assert.Equal("--", vm.CarDimensions);
        }

        [Fact]
        public void UpdateCarSpec_NullSpec_NullFallback_UsesDefault()
        {
            // Arrange
            var vm = new DashboardViewModel();

            // Act
            vm.UpdateCarSpec(null, null);

            // Assert
            Assert.Equal("CAR", vm.CarName);
            Assert.Equal("--", vm.CarClass);
        }

        [Fact]
        public void UpdateCarSpec_EmptyName_UsesFallback()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var spec = new CarSpec
            {
                Name = "",
                Category = "LMP2"
            };

            // Act
            vm.UpdateCarSpec(spec, "Oreca 07");

            // Assert
            Assert.Equal("Oreca 07", vm.CarName);
            Assert.Equal("LMP2", vm.CarClass);
        }

        [Fact]
        public void UpdateCarSpec_MissingWeight_ShowsDash()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var spec = new CarSpec
            {
                Name = "Test Car",
                WeightKg = null
            };

            // Act
            vm.UpdateCarSpec(spec, null);

            // Assert
            Assert.Equal("--", vm.CarWeight);
        }

        [Fact]
        public void UpdateCarSpec_PartialDimensions_ShowsDash()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var spec = new CarSpec
            {
                Name = "Test Car",
                LengthMm = 4500,
                WidthMm = null,
                HeightMm = 1200
            };

            // Act
            vm.UpdateCarSpec(spec, null);

            // Assert
            Assert.Equal("--", vm.CarDimensions);
        }

        [Fact]
        public void UpdateCarSpec_EmptyCategory_ShowsDash()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var spec = new CarSpec
            {
                Name = "Test Car",
                Category = ""
            };

            // Act
            vm.UpdateCarSpec(spec, string.Empty);

            // Assert
            Assert.Equal("--", vm.CarClass);
        }

        [Fact]
        public void UpdateCarSpec_EmptyPower_ShowsDash()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var spec = new CarSpec
            {
                Name = "Test Car",
                Power = string.Empty
            };

            // Act
            vm.UpdateCarSpec(spec, string.Empty);

            // Assert
            Assert.Equal("--", vm.CarPower);
        }

        #endregion

        #region Collection Tests

        [Fact]
        public void Alerts_CanAddItems()
        {
            // Arrange
            var vm = new DashboardViewModel();

            // Act
            vm.Alerts.Add("Warning: Low fuel");
            vm.Alerts.Add("Info: Optimal pit window");

            // Assert
            Assert.Equal(2, vm.Alerts.Count);
            Assert.Contains("Warning: Low fuel", vm.Alerts);
            Assert.Contains("Info: Optimal pit window", vm.Alerts);
        }

        [Fact]
        public void RelativePositions_CanAddItems()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var entry = new RelativePositionEntry
            {
                Relation = "AHEAD",
                Position = 3,
                CarNumber = "44",
                Driver = "Test Driver",
                Gap = "+2.5s",
                Class = "GT3"
            };

            // Act
            vm.RelativePositions.Add(entry);

            // Assert
            Assert.Single(vm.RelativePositions);
            Assert.Equal("AHEAD", vm.RelativePositions[0].Relation);
            Assert.Equal(3, vm.RelativePositions[0].Position);
        }

        [Fact]
        public void Standings_CanAddItems()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var entry = new StandingsEntry
            {
                Position = 1,
                Class = "LMP2",
                CarNumber = "38",
                Driver = "Leader",
                Gap = "Leader",
                Laps = 25
            };

            // Act
            vm.Standings.Add(entry);

            // Assert
            Assert.Single(vm.Standings);
            Assert.Equal(1, vm.Standings[0].Position);
            Assert.Equal("Leader", vm.Standings[0].Gap);
        }

        [Fact]
        public void Collections_RemainIndependent()
        {
            // Arrange
            var vm = new DashboardViewModel();

            // Act
            vm.Alerts.Add("Alert 1");
            vm.RelativePositions.Add(new RelativePositionEntry { Position = 1 });
            vm.Standings.Add(new StandingsEntry { Position = 1 });

            // Assert
            Assert.Single(vm.Alerts);
            Assert.Single(vm.RelativePositions);
            Assert.Single(vm.Standings);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void UpdateTelemetry_MultipleUpdates_LastValuesPreserved()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var telemetry1 = new TelemetrySampleDto { FuelLiters = 50, SpeedKph = 200 };
            var telemetry2 = new TelemetrySampleDto { FuelLiters = 45, SpeedKph = 210 };

            // Act
            vm.UpdateTelemetry(telemetry1);
            vm.UpdateTelemetry(telemetry2);

            // Assert
            Assert.Equal("45.0 L", vm.FuelLiters);
            Assert.Equal("210.0 KPH", vm.SpeedDisplay);
        }

        [Fact]
        public void UpdateTrackContext_MultipleUpdates_LastValuesPreserved()
        {
            // Arrange
            var vm = new DashboardViewModel();
            var status1 = new TrackSegmentStatus { TrackName = "Track1", SectorName = "S1" };
            var status2 = new TrackSegmentStatus { TrackName = "Track2", SectorName = "S2" };

            // Act
            vm.UpdateTrackContext(status1);
            vm.UpdateTrackContext(status2);

            // Assert
            Assert.Equal("Track2", vm.TrackName);
            Assert.Equal("S2", vm.CurrentSector);
        }

        [Fact]
        public void Properties_CanBeSetDirectly()
        {
            // Arrange
            var vm = new DashboardViewModel();

            // Act
            vm.CurrentLap = 15;
            vm.TotalLaps = 30;
            vm.Position = 3;
            vm.LastLapTime = "1:32.456";
            vm.BestLapTime = "1:31.234";

            // Assert
            Assert.Equal(15, vm.CurrentLap);
            Assert.Equal(30, vm.TotalLaps);
            Assert.Equal(3, vm.Position);
            Assert.Equal("1:32.456", vm.LastLapTime);
            Assert.Equal("1:31.234", vm.BestLapTime);
        }

        #endregion
    }
}
