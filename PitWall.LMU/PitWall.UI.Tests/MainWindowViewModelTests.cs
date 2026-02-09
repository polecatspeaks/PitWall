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
                TyreTempsC = new[] { 90.0, 92.0, 88.0, 91.0 }
            };

            vm.UpdateTelemetry(telemetry);

            Assert.Equal("42.5 L", vm.FuelLiters);
            Assert.Equal("220.0 KPH", vm.SpeedDisplay);
            Assert.Contains("90", vm.TiresLine1);
        }
    }
}
