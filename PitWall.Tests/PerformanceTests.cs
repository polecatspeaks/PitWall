using System.Diagnostics;
using GameReaderCommon;
using Xunit;
using PitWall.Tests.Mocks;

namespace PitWall.Tests
{
    /// <summary>
    /// Performance benchmark tests to ensure plugin meets CPU budget
    /// </summary>
    public class PerformanceTests
    {
        [Fact]
        public void Plugin_DataUpdate_CompletesWithin10ms()
        {
            // This ensures <5% CPU at 100Hz update rate
            // Arrange
            var mockPluginManager = new MockPluginManager();
            mockPluginManager.GameName = "IRacing";
            mockPluginManager.SetPropertyValue("DataCorePlugin.GameRunning", true);
            mockPluginManager.SetPropertyValue("DataCorePlugin.GameData.Fuel", 50.0);

            var plugin = new PitWallPlugin();
            plugin.Init(mockPluginManager);

            var gameData = new GameData();

            // Act
            var stopwatch = Stopwatch.StartNew();
            plugin.DataUpdate(mockPluginManager, ref gameData);
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.ElapsedMilliseconds < 10,
                $"DataUpdate took {stopwatch.ElapsedMilliseconds}ms, must be <10ms");
        }

        [Fact]
        public void Plugin_DataUpdate_AveragePerformance_Under5ms()
        {
            // Run multiple iterations to get average performance
            // Arrange
            var mockPluginManager = new MockPluginManager();
            mockPluginManager.GameName = "IRacing";
            var plugin = new PitWallPlugin();
            plugin.Init(mockPluginManager);

            var gameData = new GameData();
            const int iterations = 100;
            var totalMs = 0.0;

            // Act - warm up
            for (int i = 0; i < 10; i++)
            {
                plugin.DataUpdate(mockPluginManager, ref gameData);
            }

            // Act - measure
            for (int i = 0; i < iterations; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                plugin.DataUpdate(mockPluginManager, ref gameData);
                stopwatch.Stop();
                totalMs += stopwatch.Elapsed.TotalMilliseconds;
            }

            var averageMs = totalMs / iterations;

            // Assert
            Assert.True(averageMs < 5.0,
                $"Average DataUpdate time was {averageMs:F2}ms, should be <5ms");
        }

        [Fact]
        public void Plugin_Init_CompletesQuickly()
        {
            // Arrange
            var mockPluginManager = new MockPluginManager();
            var plugin = new PitWallPlugin();

            // Act
            var stopwatch = Stopwatch.StartNew();
            plugin.Init(mockPluginManager);
            stopwatch.Stop();

            // Assert
            Assert.True(stopwatch.ElapsedMilliseconds < 1000,
                $"Init took {stopwatch.ElapsedMilliseconds}ms, should be <1000ms");
        }
    }
}
