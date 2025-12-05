using PitWall.Core;
using PitWall.Models;
using PitWall.Tests.Mocks;
using Xunit;

namespace PitWall.Tests.Core
{
    public class SimHubTelemetryProviderTests
    {
        [Fact]
        public void GetCurrentTelemetry_WhenPropertiesMissing_ReturnsDefaults()
        {
            // Arrange
            var mockPluginManager = new MockPluginManager();
            var provider = new SimHubTelemetryProvider(mockPluginManager);

            // Act
            Telemetry telemetry = provider.GetCurrentTelemetry();

            // Assert
            Assert.Equal(0.0, telemetry.FuelRemaining);
            Assert.Equal(0.0, telemetry.FuelCapacity);
            Assert.Equal(0.0, telemetry.LastLapTime);
            Assert.Equal(0.0, telemetry.BestLapTime);
            Assert.Equal(0, telemetry.CurrentLap);
            Assert.False(telemetry.IsInPit);
            Assert.False(telemetry.IsLapValid);
            Assert.Equal(string.Empty, telemetry.TrackName);
            Assert.Equal(string.Empty, telemetry.CarName);
            Assert.False(provider.IsGameRunning);
        }

        [Fact]
        public void GetCurrentTelemetry_WhenPropertiesPresent_MapsValuesCorrectly()
        {
            // Arrange
            var mockPluginManager = new MockPluginManager();
            mockPluginManager.SetPropertyValue("DataCorePlugin.GameData.NewData.Fuel", 42.5);
            mockPluginManager.SetPropertyValue("DataCorePlugin.GameData.NewData.FuelMaxCapacity", 100.0);
            mockPluginManager.SetPropertyValue("DataCorePlugin.GameData.NewData.LastLapTime", 93.4);
            mockPluginManager.SetPropertyValue("DataCorePlugin.GameData.NewData.BestLapTime", 92.1);
            mockPluginManager.SetPropertyValue("DataCorePlugin.GameData.NewData.CompletedLaps", 12);
            mockPluginManager.SetPropertyValue("DataCorePlugin.GameData.NewData.IsInPit", true);
            mockPluginManager.SetPropertyValue("DataCorePlugin.GameData.NewData.CurrentLapIsValid", true);
            mockPluginManager.SetPropertyValue("DataCorePlugin.GameData.NewData.TrackName", "Monza");
            mockPluginManager.SetPropertyValue("DataCorePlugin.GameData.NewData.CarName", "Porsche 911 GT3 R");
            mockPluginManager.GameName = "ACC";
            var provider = new SimHubTelemetryProvider(mockPluginManager);

            // Act
            Telemetry telemetry = provider.GetCurrentTelemetry();

            // Assert
            Assert.Equal(42.5, telemetry.FuelRemaining);
            Assert.Equal(100.0, telemetry.FuelCapacity);
            Assert.Equal(93.4, telemetry.LastLapTime);
            Assert.Equal(92.1, telemetry.BestLapTime);
            Assert.Equal(12, telemetry.CurrentLap);
            Assert.True(telemetry.IsInPit);
            Assert.True(telemetry.IsLapValid);
            Assert.Equal("Monza", telemetry.TrackName);
            Assert.Equal("Porsche 911 GT3 R", telemetry.CarName);
            Assert.True(provider.IsGameRunning);
        }

        [Fact]
        public void IsGameRunning_WhenGameNameMissing_ReturnsFalse()
        {
            // Arrange
            var mockPluginManager = new MockPluginManager();
            var provider = new SimHubTelemetryProvider(mockPluginManager);

            // Act
            bool isRunning = provider.IsGameRunning;

            // Assert
            Assert.False(isRunning);
        }
    }
}
