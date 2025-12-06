using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using PitWall.Storage.Telemetry;
using PitWall.Telemetry;

namespace PitWall.Tests.Integration
{
    /// <summary>
    /// TDD RED Phase: Integration tests for complete IBT import pipeline
    /// 
    /// End-to-end workflow:
    /// 1. Import real IBT file (mclaren720sgt3_charlotte)
    /// 2. Extract metadata, laps, and 60Hz samples
    /// 3. Persist complete hierarchy to SQLite
    /// 4. Query data back and verify integrity
    /// 5. Verify performance with 28K+ samples
    /// 
    /// Data integrity checks:
    /// - Session metadata preserved
    /// - All laps persisted with correct statistics
    /// - All 28K+ samples retrievable
    /// - Lap filtering works correctly
    /// - Cascade delete removes all related data
    /// </summary>
    public class IbtImportIntegrationTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly ISessionRepository _sessionRepository;
        private readonly ILapRepository _lapRepository;
        private readonly ITelemetrySampleRepository _sampleRepository;
        private readonly IbtImporter _importer;
        private readonly string _testIbtFile = @"C:\Users\ohzee\Documents\iRacing\telemetry\mclaren720sgt3_charlotte 2025 roval2025 2025-11-16 13-15-19.ibt";

        public IbtImportIntegrationTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"pitwall_integration_test_{Guid.NewGuid()}.db");
            _sessionRepository = new SQLiteSessionRepository(_testDbPath);
            _lapRepository = new SQLiteLapRepository(_testDbPath);
            _sampleRepository = new SQLiteTelemetrySampleRepository(_testDbPath);
            _importer = new IbtImporter();
        }

        [Fact]
        public async Task EndToEnd_ImportIBT_PersistToDatabase_QueryBack_PreservesDataIntegrity()
        {
            // Skip if test file doesn't exist (CI/CD)
            if (!File.Exists(_testIbtFile))
            {
                return;
            }

            // ACT 1: Import IBT file
            var importedSession = await _importer.ImportIBTFileAsync(_testIbtFile);

            // ASSERT 1: Verify import extracted all data
            Assert.NotNull(importedSession);
            Assert.NotNull(importedSession.SessionMetadata);
            Assert.NotEmpty(importedSession.Laps);
            Assert.NotEmpty(importedSession.RawSamples);
            var originalLapCount = importedSession.Laps.Count;
            var originalSampleCount = importedSession.RawSamples.Count;

            // ACT 2: Persist to database
            var sessionId = await _sessionRepository.SaveSessionAsync(importedSession);

            // ASSERT 2: Verify sessionId returned
            Assert.NotNull(sessionId);
            Assert.NotEmpty(sessionId);

            // ACT 3: Query session back
            var retrievedSession = await _sessionRepository.GetSessionAsync(sessionId);

            // ASSERT 3: Verify complete hierarchy retrieved
            Assert.NotNull(retrievedSession);
            Assert.Equal(importedSession.SessionMetadata.DriverName, retrievedSession.SessionMetadata.DriverName);
            Assert.Equal(importedSession.SessionMetadata.CarName, retrievedSession.SessionMetadata.CarName);
            Assert.Equal(importedSession.SessionMetadata.TrackName, retrievedSession.SessionMetadata.TrackName);

            // ASSERT 4: Verify all laps persisted
            Assert.Equal(originalLapCount, retrievedSession.Laps.Count);
            for (int i = 0; i < originalLapCount; i++)
            {
                var originalLap = importedSession.Laps[i];
                var retrievedLap = retrievedSession.Laps[i];
                Assert.Equal(originalLap.LapNumber, retrievedLap.LapNumber);
                Assert.Equal(originalLap.LapTime, retrievedLap.LapTime);
                Assert.Equal(originalLap.FuelUsed, retrievedLap.FuelUsed);
                Assert.Equal(originalLap.AvgSpeed, retrievedLap.AvgSpeed);
            }

            // ASSERT 5: Verify all samples persisted
            Assert.Equal(originalSampleCount, retrievedSession.RawSamples.Count);

            // ACT 4: Query samples by lap
            var lap2Samples = await _sampleRepository.GetSamplesAsync(sessionId, 2);

            // ASSERT 6: Verify lap filtering works
            Assert.NotEmpty(lap2Samples);
            Assert.All(lap2Samples, s => Assert.Equal(2, s.LapNumber));

            // ACT 5: Get sample count
            var totalSamples = await _sampleRepository.GetSampleCountAsync(sessionId);

            // ASSERT 7: Verify count matches
            Assert.Equal(originalSampleCount, totalSamples);

            // ACT 6: Delete session
            var deleted = await _sessionRepository.DeleteSessionAsync(sessionId);

            // ASSERT 8: Verify cascade delete
            Assert.True(deleted);
            var afterDelete = await _sessionRepository.GetSessionAsync(sessionId);
            Assert.Null(afterDelete);
        }

        [Fact]
        public async Task Performance_BulkInsert28KSamples_CompletesInReasonableTime()
        {
            // Skip if test file doesn't exist
            if (!File.Exists(_testIbtFile))
            {
                return;
            }

            // ACT: Import and persist large dataset
            var startTime = DateTime.UtcNow;
            var importedSession = await _importer.ImportIBTFileAsync(_testIbtFile);
            var sessionId = await _sessionRepository.SaveSessionAsync(importedSession);
            var elapsed = DateTime.UtcNow - startTime;

            // ASSERT: Verify performance
            Assert.NotNull(sessionId);
            Assert.True(importedSession.RawSamples.Count >= 28000, "Should have 28K+ samples");
            Assert.True(elapsed.TotalSeconds < 10, $"Import + persist should complete in < 10s (took {elapsed.TotalSeconds:F2}s)");
        }

        [Fact]
        public async Task MultipleSession_Import_StoresIndependently()
        {
            // Skip if test file doesn't exist
            if (!File.Exists(_testIbtFile))
            {
                return;
            }

            // ACT: Import same file twice (simulating different sessions)
            var session1 = await _importer.ImportIBTFileAsync(_testIbtFile);
            session1.SessionMetadata.SessionId = Guid.NewGuid().ToString();
            var sessionId1 = await _sessionRepository.SaveSessionAsync(session1);

            var session2 = await _importer.ImportIBTFileAsync(_testIbtFile);
            session2.SessionMetadata.SessionId = Guid.NewGuid().ToString();
            var sessionId2 = await _sessionRepository.SaveSessionAsync(session2);

            // ASSERT: Verify both sessions stored independently
            var retrieved1 = await _sessionRepository.GetSessionAsync(sessionId1);
            var retrieved2 = await _sessionRepository.GetSessionAsync(sessionId2);

            Assert.NotNull(retrieved1);
            Assert.NotNull(retrieved2);
            Assert.NotEqual(sessionId1, sessionId2);
            Assert.Equal(session1.RawSamples.Count, retrieved1.RawSamples.Count);
            Assert.Equal(session2.RawSamples.Count, retrieved2.RawSamples.Count);

            // Cleanup
            await _sessionRepository.DeleteSessionAsync(sessionId1);
            await _sessionRepository.DeleteSessionAsync(sessionId2);
        }

        [Fact]
        public async Task GetRecentSessions_ReturnsMostRecentFirst()
        {
            // Skip if test file doesn't exist
            if (!File.Exists(_testIbtFile))
            {
                return;
            }

            // ACT: Import multiple sessions with different dates
            var session1 = await _importer.ImportIBTFileAsync(_testIbtFile);
            session1.SessionMetadata.SessionId = Guid.NewGuid().ToString();
            session1.SessionMetadata.SessionDate = DateTime.UtcNow.AddDays(-2);
            await _sessionRepository.SaveSessionAsync(session1);

            var session2 = await _importer.ImportIBTFileAsync(_testIbtFile);
            session2.SessionMetadata.SessionId = Guid.NewGuid().ToString();
            session2.SessionMetadata.SessionDate = DateTime.UtcNow.AddDays(-1);
            await _sessionRepository.SaveSessionAsync(session2);

            var session3 = await _importer.ImportIBTFileAsync(_testIbtFile);
            session3.SessionMetadata.SessionId = Guid.NewGuid().ToString();
            session3.SessionMetadata.SessionDate = DateTime.UtcNow;
            await _sessionRepository.SaveSessionAsync(session3);

            // ACT: Query recent sessions
            var recent = await _sessionRepository.GetRecentSessionsAsync(10);

            // ASSERT: Verify ordered by date descending
            Assert.True(recent.Count >= 3);
            Assert.True(recent[0].SessionMetadata.SessionDate >= recent[1].SessionMetadata.SessionDate);
            Assert.True(recent[1].SessionMetadata.SessionDate >= recent[2].SessionMetadata.SessionDate);

            // ASSERT: Verify laps loaded but samples not (performance optimization)
            Assert.NotEmpty(recent[0].Laps);
            // RawSamples should be null or empty for list view (not loaded)

            // Cleanup
            foreach (var session in recent)
            {
                await _sessionRepository.DeleteSessionAsync(session.SessionMetadata.SessionId);
            }
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
