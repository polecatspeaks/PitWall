using PitWall.Core;
using PitWall.Models;
using Xunit;

namespace PitWall.Tests.Core
{
    public class StrategyEngineTests
    {
        [Fact]
        public void GetRecommendation_WhenFuelBelowTwoLaps_ReturnsPitCall()
        {
            // Arrange
            var fuelStrategy = new FuelStrategy();
            fuelStrategy.RecordLap(1, 50.0, 45.0); // 5.0 per lap
            fuelStrategy.RecordLap(2, 45.0, 40.0); // 5.0 per lap
            var engine = new StrategyEngine(fuelStrategy);
            var telemetry = new Telemetry { FuelRemaining = 9.0 };

            // Act
            Recommendation rec = engine.GetRecommendation(telemetry);

            // Assert
            Assert.True(rec.ShouldPit);
            Assert.Equal(RecommendationType.Fuel, rec.Type);
            Assert.Equal(Priority.Critical, rec.Priority);
            Assert.Contains("Box this lap", rec.Message);
        }

        [Fact]
        public void GetRecommendation_WhenFuelSufficient_ReturnsNoAction()
        {
            // Arrange
            var fuelStrategy = new FuelStrategy();
            fuelStrategy.RecordLap(1, 50.0, 45.0); // 5.0 per lap
            var engine = new StrategyEngine(fuelStrategy);
            var telemetry = new Telemetry
            {
                FuelRemaining = 20.0,
                TyreWearFrontLeft = 90,
                TyreWearFrontRight = 90,
                TyreWearRearLeft = 90,
                TyreWearRearRight = 90
            };

            // Act
            Recommendation rec = engine.GetRecommendation(telemetry);

            // Assert
            Assert.False(rec.ShouldPit);
            Assert.Equal(RecommendationType.None, rec.Type);
            Assert.Equal(Priority.Info, rec.Priority);
        }

        [Fact]
        public void RecordLap_ForwardsToFuelStrategy()
        {
            // Arrange
            var fuelStrategy = new FuelStrategy();
            var engine = new StrategyEngine(fuelStrategy);
            var telemetry = new Telemetry { CurrentLap = 3, FuelRemaining = 40.0 };

            // Act
            engine.RecordLap(telemetry);

            // Assert
            Assert.Equal(1, fuelStrategy.GetLapCount());
        }
    }
}
