using System;
using System.Threading.Tasks;
using Xunit;
using PitWall.Core;
using PitWall.Models;
using PitWall.Storage;

namespace PitWall.Tests.Core
{
    public class StrategyEngineProfileTests
    {
        [Fact]
        public async Task GetRecommendation_UsesProfileDataWhenAvailable()
        {
            // Arrange
            var db = new InMemoryProfileDatabase();
            var profile = new DriverProfile
            {
                DriverName = "TestDriver",
                TrackName = "TestTrack",
                CarName = "TestCar",
                AverageFuelPerLap = 2.5,
                TypicalTyreDegradation = 0.5,
                Style = DrivingStyle.Smooth,
                SessionsCompleted = 5,
                LastUpdated = DateTime.Now
            };
            await db.SaveProfile(profile);

            var fuelStrategy = new FuelStrategy();
            var tyreDegradation = new TyreDegradation();
            var trafficAnalyzer = new TrafficAnalyzer();
            var engine = new StrategyEngine(fuelStrategy, tyreDegradation, trafficAnalyzer, db);

            await engine.LoadProfile("TestDriver", "TestTrack", "TestCar");

            var telemetry = new Telemetry
            {
                FuelRemaining = 4.0, // Should be ~1.6 laps with profile (4.0 / 2.5), trigger warning
                FuelCapacity = 50.0,
                CurrentLap = 0,
                IsLapValid = false
            };

            // Act
            var recommendation = engine.GetRecommendation(telemetry);

            // Assert
            Assert.True(recommendation.ShouldPit);
            Assert.Equal(RecommendationType.Fuel, recommendation.Type);
            Assert.Equal(Priority.Critical, recommendation.Priority);
        }

        [Fact]
        public void GetRecommendation_FallsBackToCurrentSessionWhenNoProfile()
        {
            // Arrange
            var db = new InMemoryProfileDatabase();
            var fuelStrategy = new FuelStrategy();
            var tyreDegradation = new TyreDegradation();
            var trafficAnalyzer = new TrafficAnalyzer();
            var engine = new StrategyEngine(fuelStrategy, tyreDegradation, trafficAnalyzer, db);

            // No profile loaded, engine should use FuelStrategy's live calculation
            fuelStrategy.RecordLap(1, 50.0, 47.0); // 3.0 per lap
            fuelStrategy.RecordLap(2, 47.0, 44.0); // 3.0 per lap

            var telemetry = new Telemetry
            {
                FuelRemaining = 5.0, // Should be ~1.6 laps (5.0 / 3.0), trigger warning
                FuelCapacity = 50.0,
                CurrentLap = 2,
                IsLapValid = false
            };

            // Act
            var recommendation = engine.GetRecommendation(telemetry);

            // Assert
            Assert.True(recommendation.ShouldPit);
            Assert.Equal(RecommendationType.Fuel, recommendation.Type);
        }

        [Fact]
        public async Task GetRecommendation_ProfileImprovesAccuracy()
        {
            // Arrange
            var db = new InMemoryProfileDatabase();
            var profile = new DriverProfile
            {
                DriverName = "TestDriver",
                TrackName = "TestTrack",
                CarName = "TestCar",
                AverageFuelPerLap = 2.0, // Historical average
                TypicalTyreDegradation = 0.4,
                Style = DrivingStyle.Smooth,
                SessionsCompleted = 10,
                LastUpdated = DateTime.Now
            };
            await db.SaveProfile(profile);

            var fuelStrategy = new FuelStrategy();
            var tyreDegradation = new TyreDegradation();
            var trafficAnalyzer = new TrafficAnalyzer();
            var engine = new StrategyEngine(fuelStrategy, tyreDegradation, trafficAnalyzer, db);

            await engine.LoadProfile("TestDriver", "TestTrack", "TestCar");

            // Current session has anomalous high usage (3.0/lap), but profile says 2.0/lap
            fuelStrategy.RecordLap(1, 50.0, 47.0); // 3.0 this lap (traffic)

            var telemetry = new Telemetry
            {
                FuelRemaining = 10.0, // Profile: 5 laps OK, Current: 3.3 laps (would warn incorrectly)
                FuelCapacity = 50.0,
                CurrentLap = 1,
                IsLapValid = false
            };

            // Act
            var recommendation = engine.GetRecommendation(telemetry);

            // Assert - should NOT pit with profile (5 laps), would pit without (3 laps)
            Assert.False(recommendation.ShouldPit);
            Assert.Equal(RecommendationType.None, recommendation.Type);
        }
    }
}
