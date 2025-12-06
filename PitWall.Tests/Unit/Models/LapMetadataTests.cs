using System;
using Xunit;
using PitWall.Models.Telemetry;

namespace PitWall.Tests.Unit.Models
{
    /// <summary>
    /// Unit tests for LapMetadata - represents lap-level aggregates from raw 60Hz telemetry
    /// </summary>
    public class LapMetadataTests
    {
        [Fact]
        public void Constructor_InitializesAllProperties()
        {
            // Arrange
            var lapNumber = 10;
            var lapTime = TimeSpan.FromSeconds(125.456);
            var isValid = true;
            var isClear = false;
            var fuelUsed = 1.85f;
            var fuelRemaining = 23.15f;
            var avgSpeed = 248.5f;
            var maxSpeed = 315.2f;
            var avgThrottle = 0.72f;
            var avgBrake = 0.45f;
            var avgTyreWear = 0.15f;

            // Act
            var lapData = new LapMetadata
            {
                LapNumber = lapNumber,
                LapTime = lapTime,
                IsValid = isValid,
                IsClear = isClear,
                FuelUsed = fuelUsed,
                FuelRemaining = fuelRemaining,
                AvgSpeed = avgSpeed,
                MaxSpeed = maxSpeed,
                AvgThrottle = avgThrottle,
                AvgBrake = avgBrake,
                AvgTyreWear = avgTyreWear
            };

            // Assert
            Assert.Equal(lapNumber, lapData.LapNumber);
            Assert.Equal(lapTime, lapData.LapTime);
            Assert.True(lapData.IsValid);
            Assert.False(lapData.IsClear);
            Assert.Equal(fuelUsed, lapData.FuelUsed);
            Assert.Equal(fuelRemaining, lapData.FuelRemaining);
            Assert.Equal(avgSpeed, lapData.AvgSpeed);
            Assert.Equal(maxSpeed, lapData.MaxSpeed);
            Assert.Equal(avgThrottle, lapData.AvgThrottle);
            Assert.Equal(avgBrake, lapData.AvgBrake);
            Assert.Equal(avgTyreWear, lapData.AvgTyreWear);
        }

        [Fact]
        public void ValidLap_CanBeCreated()
        {
            // Arrange & Act
            var lap = new LapMetadata
            {
                LapNumber = 5,
                LapTime = TimeSpan.FromSeconds(120.0),
                IsValid = true,
                IsClear = true,
                FuelUsed = 1.8f
            };

            // Assert
            Assert.Equal(5, lap.LapNumber);
            Assert.True(lap.IsValid);
            Assert.True(lap.IsClear);
        }

        [Fact]
        public void InvalidLap_CanBeMarked()
        {
            // Arrange & Act
            var lap = new LapMetadata
            {
                LapNumber = 1,
                LapTime = TimeSpan.FromSeconds(150.0), // Pit lap
                IsValid = false,
                IsClear = false
            };

            // Assert
            Assert.False(lap.IsValid);
            Assert.False(lap.IsClear);
        }

        [Fact]
        public void LapTime_CanBeCompared()
        {
            // Arrange
            var fastLap = new LapMetadata { LapTime = TimeSpan.FromSeconds(120.0) };
            var slowLap = new LapMetadata { LapTime = TimeSpan.FromSeconds(130.0) };

            // Act
            var difference = slowLap.LapTime - fastLap.LapTime;

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(10.0), difference);
        }
    }
}
