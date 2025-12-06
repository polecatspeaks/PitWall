using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using PitWall.Models.Telemetry;
using PitWall.Storage.Telemetry;
using PitWall.Telemetry;

namespace PitWall.Tests.Unit.Storage.Telemetry
{
    /// <summary>
    /// TDD RED Phase: Tests for SessionRepository
    /// Tests should FAIL initially (repository not implemented)
    /// 
    /// Test strategy:
    /// 1. Save complete ImportedSession with hierarchy
    /// 2. Retrieve session with all data
    /// 3. Query recent sessions
    /// 4. Delete session cascade
    /// </summary>
    public class SessionRepositoryTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ISessionRepository _repository;

        public SessionRepositoryTests()
        {
            // Use temp database for tests
            _testDbPath = Path.Combine(Path.GetTempPath(), $"pitwall_test_{Guid.NewGuid()}.db");
            _repository = new SQLiteSessionRepository(_testDbPath);
        }

        [Fact]
        public async Task SaveSessionAsync_WhenValidSession_ReturnsSessionId()
        {
            // Arrange
            var session = CreateTestSession();

            // Act
            var sessionId = await _repository.SaveSessionAsync(session);

            // Assert
            Assert.NotNull(sessionId);
            Assert.NotEmpty(sessionId);
        }

        [Fact]
        public async Task GetSessionAsync_WhenSessionExists_ReturnsCompleteHierarchy()
        {
            // Arrange
            var session = CreateTestSession();
            var sessionId = await _repository.SaveSessionAsync(session);

            // Act
            var retrieved = await _repository.GetSessionAsync(sessionId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(sessionId, retrieved.SessionMetadata.SessionId);
            Assert.NotEmpty(retrieved.Laps);
            Assert.NotEmpty(retrieved.RawSamples);
            Assert.Equal(session.Laps.Count, retrieved.Laps.Count);
        }

        [Fact]
        public async Task GetRecentSessionsAsync_ReturnsSessionsOrderedByDate()
        {
            // Arrange
            var session1 = CreateTestSession();
            session1.SessionMetadata.SessionDate = DateTime.UtcNow.AddDays(-2);
            await _repository.SaveSessionAsync(session1);

            var session2 = CreateTestSession();
            session2.SessionMetadata.SessionDate = DateTime.UtcNow.AddDays(-1);
            await _repository.SaveSessionAsync(session2);

            // Act
            var recent = await _repository.GetRecentSessionsAsync(10);

            // Assert
            Assert.NotEmpty(recent);
            Assert.True(recent[0].SessionMetadata.SessionDate >= recent[1].SessionMetadata.SessionDate);
        }

        [Fact]
        public async Task DeleteSessionAsync_WhenSessionExists_RemovesAllRelatedData()
        {
            // Arrange
            var session = CreateTestSession();
            var sessionId = await _repository.SaveSessionAsync(session);

            // Act
            var deleted = await _repository.DeleteSessionAsync(sessionId);

            // Assert
            Assert.True(deleted);
            var retrieved = await _repository.GetSessionAsync(sessionId);
            Assert.Null(retrieved);
        }

        private ImportedSession CreateTestSession()
        {
            return new ImportedSession
            {
                SourceFilePath = "test.ibt",
                ImportedAt = DateTime.UtcNow,
                SessionMetadata = new SessionMetadata
                {
                    SessionId = Guid.NewGuid().ToString(),
                    SessionDate = DateTime.UtcNow,
                    DriverName = "Test Driver",
                    CarName = "Test Car",
                    TrackName = "Test Track",
                    SessionType = "Race"
                },
                Laps = new List<LapMetadata>
                {
                    new LapMetadata
                    {
                        LapNumber = 1,
                        LapTime = TimeSpan.FromSeconds(90),
                        FuelUsed = 0.5f,
                        AvgSpeed = 100
                    },
                    new LapMetadata
                    {
                        LapNumber = 2,
                        LapTime = TimeSpan.FromSeconds(88),
                        FuelUsed = 0.48f,
                        AvgSpeed = 102
                    }
                },
                RawSamples = Enumerable.Range(0, 120).Select(i => new TelemetrySample
                {
                    LapNumber = i / 60 + 1,
                    Speed = 100f + i,
                    Throttle = 0.8f,
                    FuelLevel = 0.5f
                }).ToList()
            };
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
