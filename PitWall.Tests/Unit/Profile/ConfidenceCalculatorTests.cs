using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using PitWall.Profile;
using PitWall.Models.Telemetry;

namespace PitWall.Tests.Unit.Profile
{
    /// <summary>
    /// Tests for multi-factor confidence calculation
    /// Combines: recency, sample size, consistency, session count
    /// </summary>
    public class ConfidenceCalculatorTests
    {
        private readonly ConfidenceCalculator _calculator = new();

        [Fact]
        public void CalculateConfidence_NoData_ReturnsZero()
        {
            // Arrange
            var sessions = new List<SessionMetadata>();
            var laps = new List<LapMetadata>();

            // Act
            float confidence = _calculator.CalculateConfidence(sessions, laps, 0.5f);

            // Assert
            Assert.Equal(0.0f, confidence);
        }

        [Fact]
        public void CalculateConfidence_RecentAndConsistent_IsHigh()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var sessions = new List<SessionMetadata>
            {
                new SessionMetadata { SessionDate = now.AddDays(-1), LapCount = 50 },
                new SessionMetadata { SessionDate = now.AddDays(-2), LapCount = 48 }
            };

            var laps = new List<LapMetadata>();
            for (int i = 0; i < 100; i++)
            {
                laps.Add(new LapMetadata
                {
                    LapNumber = i + 1,
                    LapTime = TimeSpan.FromSeconds(120.0 + (i % 5) * 0.1)
                });
            }

            // Act
            float confidence = _calculator.CalculateConfidence(sessions, laps, 0.3f);

            // Assert
            Assert.True(confidence > 0.2f); // Recent, consistent, but only 2 sessions
        }

        [Fact]
        public void CalculateConfidence_OldData_IsLow()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var sessions = new List<SessionMetadata>
            {
                new SessionMetadata { SessionDate = now.AddDays(-180), LapCount = 10 }
            };

            var laps = new List<LapMetadata>();
            for (int i = 0; i < 10; i++)
            {
                laps.Add(new LapMetadata
                {
                    LapNumber = i + 1,
                    LapTime = TimeSpan.FromSeconds(120.0)
                });
            }

            // Act
            float confidence = _calculator.CalculateConfidence(sessions, laps, 0.5f);

            // Assert
            Assert.True(confidence < 0.4f);
        }

        [Fact]
        public void CalculateConfidence_HighVariability_IsLower()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var sessions = new List<SessionMetadata>
            {
                new SessionMetadata { SessionDate = now.AddDays(-1), LapCount = 50 }
            };

            var laps = new List<LapMetadata>();
            for (int i = 0; i < 100; i++)
            {
                laps.Add(new LapMetadata
                {
                    LapNumber = i + 1,
                    LapTime = TimeSpan.FromSeconds(115.0 + (i % 20))
                });
            }

            // Act
            float confidenceHigh = _calculator.CalculateConfidence(sessions, laps, 0.3f);
            float confidenceLow = _calculator.CalculateConfidence(sessions, laps, 3.0f);

            // Assert
            Assert.True(confidenceHigh > confidenceLow);
        }

        [Fact]
        public void CalculateConfidence_FewSamples_IsLower()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var sessionsFew = new List<SessionMetadata>
            {
                new SessionMetadata { SessionDate = now.AddDays(-1), LapCount = 2 }
            };

            var lapsFew = new List<LapMetadata>();
            for (int i = 0; i < 2; i++)
            {
                lapsFew.Add(new LapMetadata
                {
                    LapNumber = i + 1,
                    LapTime = TimeSpan.FromSeconds(120.0)
                });
            }

            var sessionsMany = new List<SessionMetadata>
            {
                new SessionMetadata { SessionDate = now.AddDays(-1), LapCount = 100 }
            };

            var lapsMany = Enumerable.Range(0, 100)
                .Select(i => new LapMetadata
                {
                    LapNumber = i + 1,
                    LapTime = TimeSpan.FromSeconds(120.0)
                })
                .ToList();

            // Act
            float confidenceFew = _calculator.CalculateConfidence(sessionsFew, lapsFew, 0.3f);
            float confidenceMany = _calculator.CalculateConfidence(sessionsMany, lapsMany, 0.3f);

            // Assert
            Assert.True(confidenceFew < confidenceMany);
        }

        [Fact]
        public void CalculateDriverConfidence_AveragesTrackConfidences()
        {
            // Arrange
            var trackConfidences = new List<float> { 0.8f, 0.7f, 0.6f };

            // Act
            float driverConfidence = _calculator.CalculateDriverConfidence(trackConfidences);

            // Assert
            Assert.True(driverConfidence > 0.7f);
            Assert.True(driverConfidence < 0.8f);
        }

        [Fact]
        public void CalculateDriverConfidence_EmptyList_ReturnsZero()
        {
            // Arrange
            var trackConfidences = new List<float>();

            // Act
            float driverConfidence = _calculator.CalculateDriverConfidence(trackConfidences);

            // Assert
            Assert.Equal(0.0f, driverConfidence);
        }
    }
}
