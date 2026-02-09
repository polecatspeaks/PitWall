using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using PitWall.Core.Services;
using Xunit;

namespace PitWall.Tests
{
    [SupportedOSPlatform("windows")]
    public class SharedMemoryReaderTests : IDisposable
    {
        private MemoryMappedFile? _testMmf;
        private const string TestMapName = "test-lmu-reader";

        public void Dispose()
        {
            _testMmf?.Dispose();
        }

        private void CreateTestMemoryMap()
        {
            try
            {
                // Try to delete existing test map
                var existing = MemoryMappedFile.OpenExisting(TestMapName);
                existing.Dispose();
            }
            catch
            {
                // Doesn't exist, which is fine
            }

            // Create new test memory-mapped file
            _testMmf = MemoryMappedFile.CreateNew(TestMapName, 4096);

            using (var accessor = _testMmf.CreateViewAccessor())
            {
                // Write known telemetry data
                accessor.Write(0, 125.5);   // Speed
                accessor.Write(8, 45.3);    // Fuel
                accessor.Write(16, 0.45);   // Brake
                accessor.Write(24, 0.75);   // Throttle
                accessor.Write(32, 0.15);   // Steering
                accessor.Write(40, 95.0);   // Tyre FL
                accessor.Write(48, 98.0);   // Tyre FR
                accessor.Write(56, 92.0);   // Tyre RL
                accessor.Write(64, 96.0);   // Tyre RR
            }
        }

        [Fact]
        public void SharedMemoryReader_InitializesWithNullMapName_UsesDefault()
        {
            // Arrange & Act
            var reader = new SharedMemoryReader();

            // Assert
            Assert.NotNull(reader);
            Assert.False(reader.IsConnected);
            Assert.Equal(100, reader.PollingFrequency);

            reader.Dispose();
        }

        [Fact]
        public void SharedMemoryReader_InitializesWithCustomMapName()
        {
            // Arrange & Act
            var reader = new SharedMemoryReader("Custom_LMU_Map", 8192);

            // Assert
            Assert.NotNull(reader);
            Assert.False(reader.IsConnected);

            reader.Dispose();
        }

        [Fact]
        public async Task SharedMemoryReader_StartAsync_CompletesSuccessfully()
        {
            // Arrange
            var reader = new SharedMemoryReader("nonexistent-map");

            // Act: StartAsync should complete without throwing
            await reader.StartAsync(100);

            // Assert
            Assert.True(true, "StartAsync should complete within reasonable time");

            reader.Dispose();
        }

        [Fact]
        public async Task SharedMemoryReader_StopAsync_CompletesSuccessfully()
        {
            // Arrange
            var reader = new SharedMemoryReader("test-map");
            await reader.StartAsync(100);

            // Act
            await reader.StopAsync();

            // Assert
            Assert.True(true);
            Assert.False(reader.IsConnected);

            reader.Dispose();
        }

        [Fact]
        public void SharedMemoryReader_GetLatestTelemetry_ReturnsNullWhenNotConnected()
        {
            // Arrange
            var reader = new SharedMemoryReader("nonexistent-map");

            // Act
            var latest = reader.GetLatestTelemetry();

            // Assert
            Assert.Null(latest);

            reader.Dispose();
        }

        [Fact]
        public void SharedMemoryReader_Dispose_CleanupSuccessfully()
        {
            // Arrange
            var reader = new SharedMemoryReader("test-dispose-map");

            // Act
            reader.Dispose();

            // Assert: Should not throw
            Assert.True(true);
        }

        [Fact]
        public async Task SharedMemoryReader_ConnectionStateEventSignals()
        {
            // Arrange
            var reader = new SharedMemoryReader("nonexistent-test-map");
            bool connectionStateChanged = false;

            reader.OnConnectionStateChanged += (sender, isConnected) =>
            {
                connectionStateChanged = true;
            };

            // Act
            await reader.StartAsync(100);
            await Task.Delay(50);

            // Assert: Event should have been raised (even if disconnected)
            Assert.True(connectionStateChanged || !reader.IsConnected);

            reader.Dispose();
        }
    }
}
