using PitWall.UI.Models;
using PitWall.UI.ViewModels;
using Xunit;

namespace PitWall.UI.Tests
{
    public class MainWindowViewModelTests
    {
        [Fact]
        public void UpdateRecommendation_UpdatesStrategyFields()
        {
            var vm = new MainWindowViewModel();
            var recommendation = new RecommendationDto
            {
                Recommendation = "Box this lap",
                Confidence = 0.88
            };

            vm.UpdateRecommendation(recommendation);

            Assert.Equal("Box this lap", vm.StrategyMessage);
            Assert.Equal("CONF 0.88", vm.StrategyConfidence);
        }

        [Fact]
        public void UpdateTelemetry_UpdatesFuelAndTires()
        {
            var vm = new MainWindowViewModel();
            var telemetry = new TelemetrySampleDto
            {
                FuelLiters = 42.5,
                SpeedKph = 220.0,
                TyreTempsC = new[] { 90.0, 92.0, 88.0, 91.0 },
                CurrentLap = 12,
                TotalLaps = 30,
                LastLapTime = 222.123,
                BestLapTime = 220.891
            };

            vm.UpdateTelemetry(telemetry);

            Assert.Equal("42.5 L", vm.FuelLiters);
            Assert.Equal("220.0 KPH", vm.SpeedDisplay);
            Assert.Contains("90", vm.TiresLine1);
            Assert.Equal("12/30", vm.LapDisplay);
            Assert.Equal("LAST 3:42.123", vm.TimingLastLap);
            Assert.Equal("BEST 3:40.891", vm.TimingBestLap);
            Assert.Equal("DELTA +1.232", vm.TimingDelta);
        }

        [Fact]
        public void UpdateTelemetry_BuildsAlerts()
        {
            var vm = new MainWindowViewModel();
            var telemetry = new TelemetrySampleDto
            {
                FuelLiters = 1.5,
                SpeedKph = 150.0,
                Brake = 0.95,
                Throttle = 0.0,
                Steering = 0.7,
                TyreTempsC = new[] { 112.0, 90.0, 88.0, 91.0 }
            };

            vm.UpdateTelemetry(telemetry);

            Assert.Contains("FUEL CRITICAL", vm.AlertsDisplay);
            Assert.Contains("TIRE OVERHEAT", vm.AlertsDisplay);
            Assert.Contains("WHEEL LOCK RISK", vm.AlertsDisplay);
            Assert.Contains("HEAVY BRAKE + STEER", vm.AlertsDisplay);
        }
    }
}
