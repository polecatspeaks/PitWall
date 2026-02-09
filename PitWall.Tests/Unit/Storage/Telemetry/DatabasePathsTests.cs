using System;
using System.IO;
using PitWall.Storage.Telemetry;
using Xunit;

namespace PitWall.Tests.Unit.Storage.Telemetry
{
    public class DatabasePathsTests
    {
        [Fact]
        public void GetDatabasePath_ReturnsLocalAppDataPitWallDb()
        {
            // Arrange
            var expectedBase = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PitWall");
            var expectedPath = Path.Combine(expectedBase, "pitwall.db");

            // Act
            var path = DatabasePaths.GetDatabasePath();

            // Assert
            Assert.Equal(expectedPath, path);
            Assert.True(Directory.Exists(expectedBase));
        }
    }
}
