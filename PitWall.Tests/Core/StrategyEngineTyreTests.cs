using PitWall.Core;
using PitWall.Models;
using Xunit;

namespace PitWall.Tests.Core
{
    public class StrategyEngineTyreTests
    {
        [Fact]
        public void GetRecommendation_WhenTyreBelowThreshold_ReturnsTyrePit()
        {
            var fuelStrategy = new FuelStrategy();
            var tyreDeg = new TyreDegradation();
            var engine = new StrategyEngine(fuelStrategy, tyreDeg);

            var telemetry = new Telemetry
            {
                CurrentLap = 2,
                FuelCapacity = 100,
                FuelRemaining = 80,
                IsLapValid = true,
                TyreWearFrontLeft = 25, // below threshold 30
                TyreWearFrontRight = 40,
                TyreWearRearLeft = 50,
                TyreWearRearRight = 60
            };

            var rec = engine.GetRecommendation(telemetry);

            Assert.True(rec.ShouldPit);
            Assert.Equal(RecommendationType.Tyres, rec.Type);
            Assert.Equal(Priority.Warning, rec.Priority);
        }

        [Fact]
        public void GetRecommendation_WhenTyresNearThreshold_PredictsFewLaps()
        {
            var fuelStrategy = new FuelStrategy();
            var tyreDeg = new TyreDegradation();
            tyreDeg.RecordLap(1, 90, 90, 90, 90);
            tyreDeg.RecordLap(2, 80, 80, 80, 80); // 10 per lap
            var engine = new StrategyEngine(fuelStrategy, tyreDeg);

            var telemetry = new Telemetry
            {
                CurrentLap = 3,
                FuelCapacity = 100,
                FuelRemaining = 80,
                IsLapValid = true,
                TyreWearFrontLeft = 50,
                TyreWearFrontRight = 50,
                TyreWearRearLeft = 50,
                TyreWearRearRight = 50
            };

            var rec = engine.GetRecommendation(telemetry);

            Assert.True(rec.ShouldPit);
            Assert.Equal(RecommendationType.Tyres, rec.Type);
            Assert.Equal(Priority.Warning, rec.Priority);
        }

        [Fact]
        public void GetRecommendation_WhenFuelCritical_TakesFuelPriority()
        {
            var fuelStrategy = new FuelStrategy();
            fuelStrategy.RecordLap(1, 100, 90); // 10 per lap avg
            var tyreDeg = new TyreDegradation();
            tyreDeg.RecordLap(1, 90, 90, 90, 90);
            tyreDeg.RecordLap(2, 85, 85, 85, 85);
            var engine = new StrategyEngine(fuelStrategy, tyreDeg);

            var telemetry = new Telemetry
            {
                CurrentLap = 2,
                FuelCapacity = 100,
                FuelRemaining = 15, // <2 laps left
                IsLapValid = true,
                TyreWearFrontLeft = 80,
                TyreWearFrontRight = 80,
                TyreWearRearLeft = 80,
                TyreWearRearRight = 80
            };

            var rec = engine.GetRecommendation(telemetry);

            Assert.True(rec.ShouldPit);
            Assert.Equal(RecommendationType.Fuel, rec.Type);
            Assert.Equal(Priority.Critical, rec.Priority);
        }
    }
}
