using System;
using System.Collections.Generic;
using Xunit;
using PitWall.Models.Telemetry;

namespace PitWall.Tests.Unit.Models
{
    /// <summary>
    /// Unit tests for SessionMetadata - represents a telemetry session from a single race/practice/qualifying
    /// </summary>
    public class SessionMetadataTests
    {
        [Fact]
        public void Constructor_InitializesAllProperties()
        {
            // Arrange
            var sessionId = "session_001_20251206_120000";
            var sessionDate = new DateTime(2025, 12, 6, 12, 0, 0, DateTimeKind.Utc);
            var sessionType = "Race";
            var driverId = "driver_001";
            var driverName = "Chris Mann";
            var carId = "mclaren_720s_gt3";
            var carName = "McLaren 720S GT3";
            var trackId = "redbullring";
            var trackName = "Red Bull Ring";

            // Act
            var metadata = new SessionMetadata
            {
                SessionId = sessionId,
                SessionDate = sessionDate,
                SessionType = sessionType,
                DriverId = driverId,
                DriverName = driverName,
                CarId = carId,
                CarName = carName,
                TrackId = trackId,
                TrackName = trackName
            };

            // Assert
            Assert.Equal(sessionId, metadata.SessionId);
            Assert.Equal(sessionDate, metadata.SessionDate);
            Assert.Equal(sessionType, metadata.SessionType);
            Assert.Equal(driverId, metadata.DriverId);
            Assert.Equal(driverName, metadata.DriverName);
            Assert.Equal(carId, metadata.CarId);
            Assert.Equal(carName, metadata.CarName);
            Assert.Equal(trackId, metadata.TrackId);
            Assert.Equal(trackName, metadata.TrackName);
        }

        [Theory]
        [InlineData("Practice")]
        [InlineData("Qualifying")]
        [InlineData("Race")]
        [InlineData("Warmup")]
        public void SessionType_CanBeVarious(string sessionType)
        {
            // Arrange & Act
            var metadata = new SessionMetadata { SessionType = sessionType };

            // Assert
            Assert.Equal(sessionType, metadata.SessionType);
        }

        [Fact]
        public void SessionMetadata_CanStoreAdditionalFields()
        {
            // Arrange & Act
            var metadata = new SessionMetadata
            {
                SessionId = "s001",
                SessionDate = DateTime.UtcNow,
                DriverId = "d001",
                LapCount = 42,
                Duration = TimeSpan.FromHours(2),
                TotalFuelUsed = 75.5f,
                AvgFuelPerLap = 1.8f
            };

            // Assert
            Assert.Equal(42, metadata.LapCount);
            Assert.Equal(TimeSpan.FromHours(2), metadata.Duration);
            Assert.Equal(75.5f, metadata.TotalFuelUsed);
            Assert.Equal(1.8f, metadata.AvgFuelPerLap);
        }
    }
}
