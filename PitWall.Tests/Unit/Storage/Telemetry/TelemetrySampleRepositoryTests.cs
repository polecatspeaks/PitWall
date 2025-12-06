using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using PitWall.Models.Telemetry;
using PitWall.Storage.Telemetry;

namespace PitWall.Tests.Unit.Storage.Telemetry
{
    /// <summary>
    /// TDD RED Phase: Tests for TelemetrySampleRepository
    /// Tests should FAIL initially (repository not implemented)
    /// 
    /// Performance considerations:
    /// - IBT files contain 28K+ samples at 60Hz
    /// - Need efficient bulk insert
    /// - Query by LapNumber for analysis
    /// </summary>
    public class TelemetrySampleRepositoryTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ITelemetrySampleRepository _repository;

        public TelemetrySampleRepositoryTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"pitwall_test_{Guid.NewGuid()}.db");
            _repository = new SQLiteTelemetrySampleRepository(_testDbPath);
        }

        [Fact]
        public async Task SaveSamplesAsync_WhenLargeBatch_PersistsAllSamples()
        {
            // Arrange: Simulate 60Hz for 10 seconds (600 samples)
            var sessionId = Guid.NewGuid().ToString();
            var samples = Enumerable.Range(0, 600).Select(i => new TelemetrySample
            {
                LapNumber = i / 300 + 1,
                Speed = 100f + i * 0.1f,
                Throttle = 0.8f,
                FuelLevel = 0.5f
            }).ToList();

            // Act
            await _repository.SaveSamplesAsync(sessionId, samples);

            // Assert
            var count = await _repository.GetSampleCountAsync(sessionId);
            Assert.Equal(600, count);
        }

        [Fact]
        public async Task GetSamplesAsync_WhenNoLapFilter_ReturnsAllSamples()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var samples = CreateTestSamples(120);
            await _repository.SaveSamplesAsync(sessionId, samples);

            // Act
            var retrieved = await _repository.GetSamplesAsync(sessionId, null);

            // Assert
            Assert.Equal(120, retrieved.Count);
        }

        [Fact]
        public async Task GetSamplesAsync_WithLapFilter_ReturnsOnlyMatchingSamples()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var samples = CreateTestSamples(180); // 3 laps @ 60Hz
            await _repository.SaveSamplesAsync(sessionId, samples);

            // Act
            var lap2Samples = await _repository.GetSamplesAsync(sessionId, 2);

            // Assert
            Assert.All(lap2Samples, s => Assert.Equal(2, s.LapNumber));
            Assert.Equal(60, lap2Samples.Count);
        }

        [Fact]
        public async Task GetSampleCountAsync_ReturnsCorrectCount()
        {
            // Arrange
            var sessionId = Guid.NewGuid().ToString();
            var samples = CreateTestSamples(150);
            await _repository.SaveSamplesAsync(sessionId, samples);

            // Act
            var count = await _repository.GetSampleCountAsync(sessionId);

            // Assert
            Assert.Equal(150, count);
        }

        private List<TelemetrySample> CreateTestSamples(int count)
        {
            return Enumerable.Range(0, count).Select(i => new TelemetrySample
            {
                LapNumber = i / 60 + 1,
                Speed = 100f + i,
                Throttle = 0.8f,
                Brake = 0f,
                Gear = 3,
                EngineRpm = 6000,
                SteeringAngle = 0f,
                FuelLevel = 0.5f
            }).ToList();
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
