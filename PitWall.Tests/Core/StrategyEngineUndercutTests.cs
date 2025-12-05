using System;
using System.Collections.Generic;
using Xunit;
using PitWall.Core;
using PitWall.Models;

namespace PitWall.Tests.Core
{
    public class StrategyEngineUndercutTests
    {
        [Fact]
        public void GetRecommendation_UndercutOpportunityExists_RecommendsUndercut()
        {
            // Arrange
            var fuelStrategy = new FuelStrategy();
            var tyreDegradation = new TyreDegradation();
            var trafficAnalyzer = new TrafficAnalyzer();
            var engine = new StrategyEngine(fuelStrategy, tyreDegradation, trafficAnalyzer, null);

            // Record laps with consistent 3L/lap usage
            for (int i = 1; i <= 10; i++)
            {
                double startFuel = 50.0 - ((i - 1) * 3.0);
                double endFuel = 50.0 - (i * 3.0);
                fuelStrategy.RecordLap(i, startFuel, endFuel);
            }

            var telemetry = new Telemetry
            {
                CurrentLap = 10,
                FuelRemaining = 20.0, // 20L / 3L per lap = 6.67 laps (>= 5, good for undercut)
                FuelCapacity = 50.0,
                IsLapValid = false,
                PlayerPosition = 3,
                Opponents = new List<OpponentData>
                {
                    new OpponentData { Position = 1, GapSeconds = -10.0 },
                    new OpponentData { Position = 2, GapSeconds = -4.5, TyreAge = 10 }, // Car ahead, close gap
                    new OpponentData { Position = 4, GapSeconds = 3.0 }
                }
            };

            // Act
            var recommendation = engine.GetRecommendation(telemetry);

            // Assert
            Assert.Equal(RecommendationType.Undercut, recommendation.Type);
            Assert.True(recommendation.ShouldPit);
            Assert.Contains("undercut", recommendation.Message.ToLower());
        }

        [Fact]
        public void GetRecommendation_OvercutDefensePossible_RecommendsStayOut()
        {
            // Arrange
            var fuelStrategy = new FuelStrategy();
            var tyreDegradation = new TyreDegradation();
            var trafficAnalyzer = new TrafficAnalyzer();
            var engine = new StrategyEngine(fuelStrategy, tyreDegradation, trafficAnalyzer, null);

            // Record laps with consistent 3L/lap usage
            for (int i = 1; i <= 10; i++)
            {
                double startFuel = 50.0 - ((i - 1) * 3.0);
                double endFuel = 50.0 - (i * 3.0);
                fuelStrategy.RecordLap(i, startFuel, endFuel);
            }

            var telemetry = new Telemetry
            {
                CurrentLap = 10,
                FuelRemaining = 20.0, // 20/3 = 6.67 laps (>= 5 for undercut check)
                FuelCapacity = 50.0,
                IsLapValid = false,
                PlayerPosition = 2,
                Opponents = new List<OpponentData>
                {
                    new OpponentData { Position = 1, GapSeconds = -15.0 },
                    new OpponentData { Position = 3, GapSeconds = 9.0, TyreAge = 18 } // Car behind, large gap, old tyres
                }
            };

            // Act
            var recommendation = engine.GetRecommendation(telemetry);

            // Assert
            Assert.Equal(RecommendationType.Overcut, recommendation.Type);
            Assert.False(recommendation.ShouldPit);
            Assert.Contains("stay out", recommendation.Message.ToLower());
        }

        [Fact]
        public void GetRecommendation_FuelCriticalOverridesUndercut_RecommendsFuel()
        {
            // Arrange
            var fuelStrategy = new FuelStrategy();
            var tyreDegradation = new TyreDegradation();
            var trafficAnalyzer = new TrafficAnalyzer();
            var engine = new StrategyEngine(fuelStrategy, tyreDegradation, trafficAnalyzer, null);

            // Record laps
            for (int i = 1; i <= 20; i++)
            {
                fuelStrategy.RecordLap(i, 50.0, 50.0 - (i * 2.5));
            }

            var telemetry = new Telemetry
            {
                CurrentLap = 20,
                FuelRemaining = 4.0, // Critical fuel - only 1.6 laps left
                FuelCapacity = 50.0,
                IsLapValid = false,
                PlayerPosition = 3,
                Opponents = new List<OpponentData>
                {
                    new OpponentData { Position = 2, GapSeconds = -5.0, TyreAge = 20 }
                }
            };

            // Act
            var recommendation = engine.GetRecommendation(telemetry);

            // Assert - Fuel should override undercut
            Assert.Equal(RecommendationType.Fuel, recommendation.Type);
            Assert.True(recommendation.ShouldPit);
            Assert.Equal(Priority.Critical, recommendation.Priority);
        }

        [Fact]
        public void GetRecommendation_NoCloseOpponents_NoUndercutRecommendation()
        {
            // Arrange
            var fuelStrategy = new FuelStrategy();
            var tyreDegradation = new TyreDegradation();
            var trafficAnalyzer = new TrafficAnalyzer();
            var engine = new StrategyEngine(fuelStrategy, tyreDegradation, trafficAnalyzer, null);

            for (int i = 1; i <= 10; i++)
            {
                fuelStrategy.RecordLap(i, 50.0, 50.0 - (i * 2.5));
            }

            var telemetry = new Telemetry
            {
                CurrentLap = 10,
                FuelRemaining = 30.0,
                FuelCapacity = 50.0,
                IsLapValid = false,
                PlayerPosition = 5,
                Opponents = new List<OpponentData>
                {
                    new OpponentData { Position = 3, GapSeconds = -35.0 }, // Too far ahead
                    new OpponentData { Position = 7, GapSeconds = 40.0 }   // Too far behind
                }
            };

            // Act
            var recommendation = engine.GetRecommendation(telemetry);

            // Assert - No undercut/overcut recommendation
            Assert.NotEqual(RecommendationType.Undercut, recommendation.Type);
            Assert.NotEqual(RecommendationType.Overcut, recommendation.Type);
        }
    }
}
