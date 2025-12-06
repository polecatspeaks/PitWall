using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using PitWall.Models.Telemetry;
using PitWall.Storage.Telemetry;

namespace PitWall.Tests.Unit.Storage.Telemetry
{
    /// <summary>
    /// TDD RED Phase: Tests for LapRepository
    /// Tests should FAIL initially (repository not implemented)
    /// </summary>
    public class LapRepositoryTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ILapRepository _lapRepository;

        public LapRepositoryTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"pitwall_test_{Guid.NewGuid()}.db");
            _lapRepository = new SQLiteLapRepository(_testDbPath);
        }

        [Fact]
        public async Task SaveLapsAsync_WhenValidLaps_PersistsAllLaps()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var laps = new List<LapMetadata>
            {
                new LapMetadata { LapNumber = 1, LapTime = TimeSpan.FromSeconds(90), FuelUsed = 0.5f, AvgSpeed = 100 },
                new LapMetadata { LapNumber = 2, LapTime = TimeSpan.FromSeconds(88), FuelUsed = 0.48f, AvgSpeed = 102 }
            };

            // Act
            await _lapRepository.SaveLapsAsync(sessionId, laps);

            // Assert
            var retrieved = await _lapRepository.GetSessionLapsAsync(sessionId);
            Assert.Equal(2, retrieved.Count);
        }

        [Fact]
        public async Task GetSessionLapsAsync_ReturnsLapsOrderedByLapNumber()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var laps = new List<LapMetadata>
            {
                new LapMetadata { LapNumber = 3, LapTime = TimeSpan.FromSeconds(87) },
                new LapMetadata { LapNumber = 1, LapTime = TimeSpan.FromSeconds(90) },
                new LapMetadata { LapNumber = 2, LapTime = TimeSpan.FromSeconds(88) }
            };
            await _lapRepository.SaveLapsAsync(sessionId, laps);

            // Act
            var retrieved = await _lapRepository.GetSessionLapsAsync(sessionId);

            // Assert
            Assert.Equal(1, retrieved[0].LapNumber);
            Assert.Equal(2, retrieved[1].LapNumber);
            Assert.Equal(3, retrieved[2].LapNumber);
        }

        [Fact]
        public async Task GetLapAsync_WhenLapExists_ReturnsSingleLap()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var laps = new List<LapMetadata>
            {
                new LapMetadata { LapNumber = 1, LapTime = TimeSpan.FromSeconds(90), AvgSpeed = 100 }
            };
            await _lapRepository.SaveLapsAsync(sessionId, laps);

            // Act
            var lap = await _lapRepository.GetLapAsync(sessionId, 1);

            // Assert
            Assert.NotNull(lap);
            Assert.Equal(1, lap.LapNumber);
            Assert.Equal(100, lap.AvgSpeed);
        }

        [Fact]
        public async Task GetLapAsync_WhenLapNotFound_ReturnsNull()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();

            // Act
            var lap = await _lapRepository.GetLapAsync(sessionId, 99);

            // Assert
            Assert.Null(lap);
        }

        public void Dispose()
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
    }
}
