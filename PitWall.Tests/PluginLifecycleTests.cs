using Xunit;
using PitWall.Tests.Mocks;

namespace PitWall.Tests
{
    /// <summary>
    /// Tests for plugin initialization and cleanup lifecycle
    /// </summary>
    public class PluginLifecycleTests
    {
        [Fact]
        public void Plugin_Initializes_WithoutCrashing()
        {
            // Arrange
            var mockPluginManager = new MockPluginManager();

            // Act
            var plugin = new PitWallPlugin();
            plugin.Init(mockPluginManager);

            // Assert
            Assert.NotNull(plugin);
            Assert.Equal("Pit Wall Race Engineer", plugin.Name);
            Assert.NotNull(plugin.PluginManager);
        }

        [Fact]
        public void Plugin_End_CleansUpResources()
        {
            // Arrange
            var mockPluginManager = new MockPluginManager();
            var plugin = new PitWallPlugin();
            plugin.Init(mockPluginManager);

            // Act & Assert (should not throw)
            plugin.End(mockPluginManager);
        }

        [Fact]
        public void Plugin_Name_IsCorrect()
        {
            // Arrange
            var plugin = new PitWallPlugin();

            // Act
            var name = plugin.Name;

            // Assert
            Assert.Equal("Pit Wall Race Engineer", name);
        }
    }
}
