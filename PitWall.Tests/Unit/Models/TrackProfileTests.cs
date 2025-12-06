using System;
using Xunit;
using PitWall.Models.Profiles;

namespace PitWall.Tests.Unit.Models
{
    /// <summary>
    /// Unit tests for TrackProfile - leaf level of hierarchical profiles
    /// Contains aggregated telemetry data for a driver+car+track combination
    /// </summary>
    public class TrackProfileTests
    {
        [Fact]
        public void Constructor_InitializesProperties()
        {
            // Arrange
            var trackId = "redbullring";
            var trackName = "Red Bull Ring";

            // Act
            var profile = new TrackProfile
            {
                TrackId = trackId,
                TrackName = trackName
            };

            // Assert
            Assert.Equal(trackId, profile.TrackId);
            Assert.Equal(trackName, profile.TrackName);
        }

        [Fact]
        public void TrackProfile_CanStoreAggregateData()
        {
            // Arrange & Act
            var profile = new TrackProfile
            {
                TrackId = "redbullring",
                TrackName = "Red Bull Ring",
                AvgFuelPerLap = 1.87f,
                AvgLapTime = TimeSpan.FromSeconds(130.456),
                LapTimeStdDev = 0.5f,
                TypicalTyreDegradation = 0.08f,
                SessionsCompleted = 12,
                LapCount = 487,
                Confidence = 0.91f,
                LastUpdated = DateTime.UtcNow
            };

            // Assert
            Assert.Equal(1.87f, profile.AvgFuelPerLap);
            Assert.Equal(TimeSpan.FromSeconds(130.456), profile.AvgLapTime);
            Assert.Equal(0.5f, profile.LapTimeStdDev);
            Assert.Equal(0.08f, profile.TypicalTyreDegradation);
            Assert.Equal(12, profile.SessionsCompleted);
            Assert.Equal(487, profile.LapCount);
            Assert.Equal(0.91f, profile.Confidence);
        }

        [Fact]
        public void TrackProfile_CanTrackLastSessionDate()
        {
            // Arrange
            var lastSession = new DateTime(2025, 12, 5, 14, 30, 0, DateTimeKind.Utc);

            // Act
            var profile = new TrackProfile
            {
                TrackId = "silverstone",
                TrackName = "Silverstone",
                LastSessionDate = lastSession
            };

            // Assert
            Assert.Equal(lastSession, profile.LastSessionDate);
        }

        [Fact]
        public void TrackProfile_CanBeMarkedAsStale()
        {
            // Arrange & Act
            var profile = new TrackProfile
            {
                TrackId = "monza",
                IsStale = true
            };

            // Assert
            Assert.True(profile.IsStale);
        }
    }
}
