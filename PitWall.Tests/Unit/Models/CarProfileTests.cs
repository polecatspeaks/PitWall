using System;
using System.Collections.Generic;
using Xunit;
using PitWall.Models.Profiles;

namespace PitWall.Tests.Unit.Models
{
    /// <summary>
    /// Unit tests for CarProfile - second level of hierarchical profiles
    /// Contains multiple TrackProfiles for tracks where this car was driven
    /// </summary>
    public class CarProfileTests
    {
        [Fact]
        public void Constructor_InitializesProperties()
        {
            // Arrange
            var carId = "mclaren_720s_gt3";
            var carName = "McLaren 720S GT3";

            // Act
            var profile = new CarProfile
            {
                CarId = carId,
                CarName = carName
            };

            // Assert
            Assert.Equal(carId, profile.CarId);
            Assert.Equal(carName, profile.CarName);
            Assert.NotNull(profile.TrackProfiles);
            Assert.Empty(profile.TrackProfiles);
        }

        [Fact]
        public void CarProfile_CanHaveMultipleTrackProfiles()
        {
            // Arrange
            var car = new CarProfile
            {
                CarId = "mclaren",
                CarName = "McLaren 720S GT3"
            };

            var redBull = new TrackProfile { TrackId = "redbullring", TrackName = "Red Bull Ring" };
            var silverstone = new TrackProfile { TrackId = "silverstone", TrackName = "Silverstone" };

            // Act
            car.TrackProfiles.Add(redBull);
            car.TrackProfiles.Add(silverstone);

            // Assert
            Assert.Equal(2, car.TrackProfiles.Count);
            Assert.Contains(redBull, car.TrackProfiles);
            Assert.Contains(silverstone, car.TrackProfiles);
        }

        [Fact]
        public void CarProfile_TrackingProperties()
        {
            // Arrange & Act
            var profile = new CarProfile
            {
                CarId = "mclaren",
                SessionsCompleted = 15,
                AvgFuelPerLap = 1.95f,
                LastUpdated = DateTime.UtcNow,
                Confidence = 0.82f
            };

            // Assert
            Assert.Equal(15, profile.SessionsCompleted);
            Assert.Equal(1.95f, profile.AvgFuelPerLap);
            Assert.Equal(0.82f, profile.Confidence);
        }
    }
}
