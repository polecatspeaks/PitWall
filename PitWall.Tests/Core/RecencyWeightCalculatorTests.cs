using System;
using System.Collections.Generic;
using System.Linq;
using PitWall.Core;
using Xunit;

namespace PitWall.Tests.Core
{
    public class RecencyWeightCalculatorTests
    {
        private readonly RecencyWeightCalculator _calculator = new RecencyWeightCalculator();

        [Fact]
        public void CalculateWeight_CurrentSession_Returns100Percent()
        {
            var now = DateTime.Now;
            var weight = _calculator.CalculateWeight(now, now);

            Assert.Equal(1.0, weight, 2);
        }

        [Fact]
        public void CalculateWeight_30DaysOld_Returns81Percent()
        {
            var now = DateTime.Now;
            var sessionDate = now.AddDays(-30);

            var weight = _calculator.CalculateWeight(sessionDate, now);

            // Should be ~0.81 (81%)
            Assert.InRange(weight, 0.79, 0.83);
        }

        [Fact]
        public void CalculateWeight_90DaysOld_Returns50Percent()
        {
            var now = DateTime.Now;
            var sessionDate = now.AddDays(-90);

            var weight = _calculator.CalculateWeight(sessionDate, now);

            // Half-life: 90 days = 50% weight
            Assert.InRange(weight, 0.48, 0.52);
        }

        [Fact]
        public void CalculateWeight_180DaysOld_Returns25Percent()
        {
            var now = DateTime.Now;
            var sessionDate = now.AddDays(-180);

            var weight = _calculator.CalculateWeight(sessionDate, now);

            // 2 half-lives: 180 days = 25% weight
            Assert.InRange(weight, 0.23, 0.27);
        }

        [Fact]
        public void CalculateWeight_365DaysOld_ReturnsLowWeight()
        {
            var now = DateTime.Now;
            var sessionDate = now.AddDays(-365);

            var weight = _calculator.CalculateWeight(sessionDate, now);

            // Very old data should have very low weight
            Assert.True(weight < 0.10);
            Assert.True(weight > 0);
        }

        [Fact]
        public void CalculateWeight_FutureDate_Returns100Percent()
        {
            var now = DateTime.Now;
            var futureDate = now.AddDays(10);

            var weight = _calculator.CalculateWeight(futureDate, now);

            // Future dates shouldn't happen, but handle gracefully
            Assert.Equal(1.0, weight);
        }

        [Fact]
        public void CalculateWeightedAverageFuel_FavorsRecentData()
        {
            var now = DateTime.Now;
            var sessions = new List<(DateTime, double)>
            {
                (now.AddDays(-200), 3.5), // Old session: high fuel usage
                (now.AddDays(-7), 2.6)     // Recent session: lower fuel usage
            };

            var weighted = _calculator.CalculateWeightedAverageFuel(sessions, now);

            // Should be much closer to 2.6 than 3.5
            Assert.InRange(weighted, 2.6, 2.8);

            // Should NOT be simple average (3.05)
            Assert.True(Math.Abs(weighted - 3.05) > 0.2);
        }

        [Fact]
        public void CalculateWeightedAverageFuel_AllRecentSessions_ReturnsAccurateAverage()
        {
            var now = DateTime.Now;
            var sessions = new List<(DateTime, double)>
            {
                (now.AddDays(-2), 2.8),
                (now.AddDays(-5), 2.7),
                (now.AddDays(-7), 2.9)
            };

            var weighted = _calculator.CalculateWeightedAverageFuel(sessions, now);

            // All recent, weights similar, should be close to simple average
            var simpleAverage = sessions.Average(s => s.Item2);
            Assert.InRange(weighted, simpleAverage - 0.1, simpleAverage + 0.1);
        }

        [Fact]
        public void CalculateWeightedAverageFuel_EmptySessions_ReturnsZero()
        {
            var now = DateTime.Now;
            var sessions = new List<(DateTime, double)>();

            var weighted = _calculator.CalculateWeightedAverageFuel(sessions, now);

            Assert.Equal(0.0, weighted);
        }

        [Fact]
        public void CalculateWeightedAverageTyres_UsesExponentialDecay()
        {
            var now = DateTime.Now;
            var sessions = new List<(DateTime, double)>
            {
                (now.AddDays(-180), 0.20), // Old: high degradation
                (now.AddDays(-10), 0.12)   // Recent: lower degradation
            };

            var weighted = _calculator.CalculateWeightedAverageTyres(sessions, now);

            // Should strongly favor recent 0.12 over old 0.20
            Assert.InRange(weighted, 0.12, 0.14);
        }

        [Fact]
        public void CalculateWeight_OlderSessionWeighsLessThanRecent()
        {
            var now = DateTime.Now;
            var recent = now.AddDays(-7);
            var old = now.AddDays(-180);

            var recentWeight = _calculator.CalculateWeight(recent, now);
            var oldWeight = _calculator.CalculateWeight(old, now);

            // Recent should be at least 3x more important than old
            Assert.True(recentWeight > oldWeight * 3);
            Assert.True(recentWeight > 0.9);
            Assert.True(oldWeight < 0.3);
        }
    }
}
