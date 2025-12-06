using System;
using System.Collections.Generic;
using Xunit;
using PitWall.Models.Profiles;

namespace PitWall.Tests.Unit.Models
{
    /// <summary>
    /// Unit tests for DriverProfile - root level of hierarchical profiles
    /// Contains multiple CarProfiles for cars driven by this driver
    /// </summary>
    public class DriverProfileTests
    {
        [Fact]
        public void Constructor_InitializesProperties()
        {
            // Arrange
            var driverId = "driver_001";
            var driverName = "Chris Mann";
            var style = 7; // iRacing style number

            // Act
            var profile = new DriverProfile
            {
                DriverId = driverId,
                DriverName = driverName,
                Style = style
            };

            // Assert
            Assert.Equal(driverId, profile.DriverId);
            Assert.Equal(driverName, profile.DriverName);
            Assert.Equal(style, profile.Style);
            Assert.NotNull(profile.CarProfiles);
            Assert.Empty(profile.CarProfiles);
        }

        [Fact]
        public void DriverProfile_CanHaveMultipleCarProfiles()
        {
            // Arrange
            var driver = new DriverProfile
            {
                DriverId = "driver_001",
                DriverName = "Chris Mann"
            };

            var mclaren = new CarProfile { CarId = "mclaren", CarName = "McLaren 720S GT3" };
            var porsche = new CarProfile { CarId = "porsche", CarName = "Porsche 911 GT3" };

            // Act
            driver.CarProfiles.Add(mclaren);
            driver.CarProfiles.Add(porsche);

            // Assert
            Assert.Equal(2, driver.CarProfiles.Count);
            Assert.Contains(mclaren, driver.CarProfiles);
            Assert.Contains(porsche, driver.CarProfiles);
        }

        [Fact]
        public void DriverProfile_TrackingProperties()
        {
            // Arrange & Act
            var profile = new DriverProfile
            {
                DriverId = "d001",
                SessionsCompleted = 25,
                LastUpdated = DateTime.UtcNow,
                Confidence = 0.87f
            };

            // Assert
            Assert.Equal(25, profile.SessionsCompleted);
            Assert.NotEqual(DateTime.MinValue, profile.LastUpdated);
            Assert.Equal(0.87f, profile.Confidence);
        }
    }
}
