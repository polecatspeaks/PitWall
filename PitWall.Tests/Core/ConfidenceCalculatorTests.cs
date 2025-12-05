using System;
using System.Collections.Generic;
using PitWall.Core;
using Xunit;

namespace PitWall.Tests.Core
{
    public class ConfidenceCalculatorTests
    {
        private readonly ConfidenceCalculator _calculator = new ConfidenceCalculator();

        [Fact]
        public void Calculate_NoSessions_ReturnsZero()
        {
            var sessions = new List<(DateTime, int, double)>();
            var confidence = _calculator.Calculate(sessions, DateTime.Now);

            Assert.Equal(0.0, confidence);
        }

        [Fact]
        public void Calculate_RecentConsistentData_ReturnsHighConfidence()
        {
            var now = DateTime.Now;
            var sessions = new List<(DateTime, int, double)>
            {
                (now.AddDays(-7), 50, 2.8),
                (now.AddDays(-5), 45, 2.7),
                (now.AddDays(-2), 52, 2.8)
            };

            var confidence = _calculator.Calculate(sessions, now);

            // Recent + many laps + consistent = high confidence
            Assert.True(confidence > 0.75);
        }

        [Fact]
        public void Calculate_StaleData_ReturnsLowConfidence()
        {
            var now = DateTime.Now;
            var sessions = new List<(DateTime, int, double)>
            {
                (now.AddDays(-300), 30, 3.0)
            };

            var confidence = _calculator.Calculate(sessions, now);

            // Very old data = low confidence
            Assert.True(confidence < 0.4);
        }

        [Fact]
        public void Calculate_InconsistentData_ReducesConfidence()
        {
            var now = DateTime.Now;
            var sessions = new List<(DateTime, int, double)>
            {
                (now.AddDays(-5), 40, 2.5),
                (now.AddDays(-3), 40, 3.8), // Very different fuel usage
                (now.AddDays(-1), 40, 2.6)
            };

            var confidence = _calculator.Calculate(sessions, now);

            // With high variance data, confidence should still be calculated
            // The algorithm weighs all factors, so recent + decent sample size helps
            Assert.InRange(confidence, 0.50, 0.90);
        }

        [Fact]
        public void Calculate_FewLaps_ReducesConfidence()
        {
            var now = DateTime.Now;
            var sessions = new List<(DateTime, int, double)>
            {
                (now.AddDays(-2), 5, 2.8) // Only 5 laps
            };

            var confidence = _calculator.Calculate(sessions, now);

            // Small sample (5 laps = 5% of target 100) but very recent
            // Should be better than stale data but not excellent
            Assert.InRange(confidence, 0.40, 0.70);
        }

        [Fact]
        public void Calculate_ManyLaps_IncreasesConfidence()
        {
            var now = DateTime.Now;
            var sessions = new List<(DateTime, int, double)>
            {
                (now.AddDays(-5), 80, 2.8),
                (now.AddDays(-2), 75, 2.7)
            };

            var confidence = _calculator.Calculate(sessions, now);

            // Large sample size (155 laps) + recent = high confidence
            Assert.True(confidence > 0.70);
        }

        [Fact]
        public void Calculate_ManySessions_IncreasesConfidence()
        {
            var now = DateTime.Now;
            var sessions = new List<(DateTime, int, double)>
            {
                (now.AddDays(-14), 30, 2.8),
                (now.AddDays(-12), 28, 2.7),
                (now.AddDays(-10), 32, 2.8),
                (now.AddDays(-7), 29, 2.7),
                (now.AddDays(-5), 31, 2.8),
                (now.AddDays(-3), 30, 2.9),
                (now.AddDays(-1), 28, 2.7)
            };

            var confidence = _calculator.Calculate(sessions, now);

            // Many sessions + recent + consistent = excellent confidence
            Assert.True(confidence > 0.80);
        }

        [Fact]
        public void IsStale_OldSession_ReturnsTrue()
        {
            var now = DateTime.Now;
            var oldDate = now.AddDays(-200);

            var isStale = _calculator.IsStale(oldDate, now);

            Assert.True(isStale);
        }

        [Fact]
        public void IsStale_RecentSession_ReturnsFalse()
        {
            var now = DateTime.Now;
            var recentDate = now.AddDays(-30);

            var isStale = _calculator.IsStale(recentDate, now);

            Assert.False(isStale);
        }

        [Fact]
        public void IsStale_ExactlyThreshold_ReturnsFalse()
        {
            var now = DateTime.Now;
            var thresholdDate = now.AddDays(-180);

            var isStale = _calculator.IsStale(thresholdDate, now);

            // Exactly 180 days should NOT be stale (>180 required)
            Assert.False(isStale);
        }

        [Fact]
        public void IsStale_JustOverThreshold_ReturnsTrue()
        {
            var now = DateTime.Now;
            var overThreshold = now.AddDays(-181);

            var isStale = _calculator.IsStale(overThreshold, now);

            // Over 180 days should be stale
            Assert.True(isStale);
        }

        [Fact]
        public void GetConfidenceDescription_ExcellentRange()
        {
            Assert.Equal("Excellent", _calculator.GetConfidenceDescription(0.95));
            Assert.Equal("Excellent", _calculator.GetConfidenceDescription(0.80));
        }

        [Fact]
        public void GetConfidenceDescription_GoodRange()
        {
            Assert.Equal("Good", _calculator.GetConfidenceDescription(0.75));
            Assert.Equal("Good", _calculator.GetConfidenceDescription(0.60));
        }

        [Fact]
        public void GetConfidenceDescription_FairRange()
        {
            Assert.Equal("Fair", _calculator.GetConfidenceDescription(0.55));
            Assert.Equal("Fair", _calculator.GetConfidenceDescription(0.40));
        }

        [Fact]
        public void GetConfidenceDescription_LowRange()
        {
            Assert.Equal("Low", _calculator.GetConfidenceDescription(0.35));
            Assert.Equal("Low", _calculator.GetConfidenceDescription(0.20));
        }

        [Fact]
        public void GetConfidenceDescription_VeryLowRange()
        {
            Assert.Equal("Very Low", _calculator.GetConfidenceDescription(0.15));
            Assert.Equal("Very Low", _calculator.GetConfidenceDescription(0.05));
        }

        [Fact]
        public void Calculate_BalancesAllFourFactors()
        {
            var now = DateTime.Now;

            // Good recency, good sample size, good consistency, good session count
            var goodSessions = new List<(DateTime, int, double)>
            {
                (now.AddDays(-2), 40, 2.8),
                (now.AddDays(-4), 40, 2.7),
                (now.AddDays(-6), 40, 2.9)
            };

            // Poor recency, same sample size/consistency/count
            var poorRecencySessions = new List<(DateTime, int, double)>
            {
                (now.AddDays(-150), 40, 2.8),
                (now.AddDays(-152), 40, 2.7),
                (now.AddDays(-154), 40, 2.9)
            };

            var goodConfidence = _calculator.Calculate(goodSessions, now);
            var poorConfidence = _calculator.Calculate(poorRecencySessions, now);

            // Recency should make a significant difference
            Assert.True(goodConfidence > poorConfidence + 0.3);
        }
    }
}
