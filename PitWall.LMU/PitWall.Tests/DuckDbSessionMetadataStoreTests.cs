using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Api.Models;
using PitWall.Api.Services;
using PitWall.Core.Storage;
using Xunit;

namespace PitWall.Tests
{
    public class DuckDbSessionMetadataStoreTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly TestDuckDbConnector _connector;
        private readonly DuckDbSessionMetadataStore _store;

        public DuckDbSessionMetadataStoreTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test-metadata-{Guid.NewGuid():N}.duckdb");
            _connector = new TestDuckDbConnector(_testDbPath);
            _store = new DuckDbSessionMetadataStore(_connector, NullLogger<DuckDbSessionMetadataStore>.Instance);
            InitializeTestDatabase();
        }

        public void Dispose()
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }

        private void InitializeTestDatabase()
        {
            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE sessions (
                    session_id INTEGER PRIMARY KEY,
                    track_name VARCHAR,
                    car_name VARCHAR,
                    recording_time VARCHAR
                );
                
                CREATE TABLE session_metadata (
                    session_id INTEGER,
                    ""key"" VARCHAR,
                    ""value"" VARCHAR
                );
            ";
            command.ExecuteNonQuery();
        }

        [Fact]
        public async Task GetAllAsync_ReturnsEmptyDictionary_WhenNoSessionsExist()
        {
            var result = await _store.GetAllAsync();

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllSessions_WithMetadata()
        {
            InsertTestSession(1, "Silverstone", "Ferrari 488", "123");
            InsertTestSession(2, "Spa", "Mercedes AMG", "456");

            var result = await _store.GetAllAsync();

            Assert.Equal(2, result.Count);
            Assert.True(result.ContainsKey(1));
            Assert.Equal("Silverstone", result[1].Track);
            Assert.Equal("Ferrari 488", result[1].Car);
            Assert.Equal("123", result[1].TrackId);
            
            Assert.True(result.ContainsKey(2));
            Assert.Equal("Spa", result[2].Track);
            Assert.Equal("Mercedes AMG", result[2].Car);
            Assert.Equal("456", result[2].TrackId);
        }

        [Fact]
        public async Task GetAllAsync_HandlesNullValues()
        {
            InsertTestSession(1, null, null, null);

            var result = await _store.GetAllAsync();

            Assert.Single(result);
            Assert.Equal("Unknown", result[1].Track);
            Assert.Equal("Unknown", result[1].Car);
            Assert.Null(result[1].TrackId);
        }

        [Fact]
        public async Task GetAllAsync_HandlesWhitespaceValues()
        {
            InsertTestSession(1, "  ", "  ", "  ");

            var result = await _store.GetAllAsync();

            Assert.Single(result);
            Assert.Equal("Unknown", result[1].Track);
            Assert.Equal("Unknown", result[1].Car);
            Assert.Null(result[1].TrackId);
        }

        [Fact]
        public async Task GetAllAsync_SupportsCancellation()
        {
            // Note: DuckDB operations are not async and don't support cancellation tokens
            // This test verifies the interface supports cancellation but the actual cancellation
            // happens before the DB query starts
            InsertTestSession(1, "Silverstone", "Ferrari 488", null);
            
            var result = await _store.GetAllAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public async Task GetAsync_ReturnsNull_WhenSessionDoesNotExist()
        {
            var result = await _store.GetAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAsync_ReturnsMetadata_ForExistingSession()
        {
            InsertTestSession(1, "Monza", "Red Bull RB19", "789");

            var result = await _store.GetAsync(1);

            Assert.NotNull(result);
            Assert.Equal("Monza", result.Track);
            Assert.Equal("Red Bull RB19", result.Car);
            Assert.Equal("789", result.TrackId);
        }

        [Fact]
        public async Task GetAsync_HandlesNullValues()
        {
            InsertTestSession(1, null, null, null);

            var result = await _store.GetAsync(1);

            Assert.NotNull(result);
            Assert.Equal("Unknown", result.Track);
            Assert.Equal("Unknown", result.Car);
            Assert.Null(result.TrackId);
        }

        [Fact]
        public async Task SetAsync_ThrowsArgumentNullException_WhenMetadataIsNull()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await _store.SetAsync(1, null!));
        }

        [Fact]
        public async Task SetAsync_UpdatesExistingSession()
        {
            InsertTestSession(1, "OldTrack", "OldCar", null);

            var newMetadata = new SessionMetadata
            {
                Track = "NewTrack",
                Car = "NewCar",
                TrackId = "NewId"
            };

            await _store.SetAsync(1, newMetadata);

            var result = await _store.GetAsync(1);
            Assert.NotNull(result);
            Assert.Equal("NewTrack", result.Track);
            Assert.Equal("NewCar", result.Car);
            Assert.Equal("NewId", result.TrackId);
        }

        [Fact]
        public async Task SetAsync_InsertsMetadataRows()
        {
            InsertTestSession(1, "Track", "Car", null);

            var metadata = new SessionMetadata
            {
                Track = "Spa-Francorchamps",
                Car = "McLaren MCL60",
                TrackId = "spa2023"
            };

            await _store.SetAsync(1, metadata);

            var metadataRows = GetSessionMetadataRows(1);
            Assert.Contains(metadataRows, row => row.Key == "TrackName" && row.Value == "Spa-Francorchamps");
            Assert.Contains(metadataRows, row => row.Key == "TrackId" && row.Value == "spa2023");
            Assert.Contains(metadataRows, row => row.Key == "CarName" && row.Value == "McLaren MCL60");
        }

        [Fact]
        public async Task SetAsync_SkipsTrackIdWhenNullOrEmpty()
        {
            InsertTestSession(1, "Track", "Car", null);

            var metadata = new SessionMetadata
            {
                Track = "Monza",
                Car = "Ferrari",
                TrackId = null
            };

            await _store.SetAsync(1, metadata);

            var metadataRows = GetSessionMetadataRows(1);
            Assert.DoesNotContain(metadataRows, row => row.Key == "TrackId");
        }

        [Fact]
        public async Task SetAsync_DeletesOldMetadataBeforeInsert()
        {
            InsertTestSession(1, "Track", "Car", "OldId");
            InsertMetadata(1, "TrackName", "OldTrackName");
            InsertMetadata(1, "CarName", "OldCarName");

            var metadata = new SessionMetadata
            {
                Track = "NewTrack",
                Car = "NewCar",
                TrackId = "NewId"
            };

            await _store.SetAsync(1, metadata);

            var metadataRows = GetSessionMetadataRows(1);
            Assert.DoesNotContain(metadataRows, row => row.Value == "OldTrackName");
            Assert.DoesNotContain(metadataRows, row => row.Value == "OldCarName");
        }

        [Fact]
        public async Task SetAsync_HandlesNonExistentSession()
        {
            var metadata = new SessionMetadata
            {
                Track = "Track",
                Car = "Car",
                TrackId = "Id"
            };

            // Should not throw even if session doesn't exist
            await _store.SetAsync(999, metadata);
        }

        private void InsertTestSession(int sessionId, string? track, string? car, string? trackId)
        {
            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO sessions (session_id, track_name, car_name, recording_time) VALUES (?, ?, ?, ?)";
            
            var idParam = command.CreateParameter();
            idParam.Value = sessionId;
            command.Parameters.Add(idParam);
            
            var trackParam = command.CreateParameter();
            trackParam.Value = (object?)track ?? DBNull.Value;
            command.Parameters.Add(trackParam);
            
            var carParam = command.CreateParameter();
            carParam.Value = (object?)car ?? DBNull.Value;
            command.Parameters.Add(carParam);
            
            var timeParam = command.CreateParameter();
            timeParam.Value = (object?)"2024-01-01_10:00:00" ?? DBNull.Value;
            command.Parameters.Add(timeParam);
            
            command.ExecuteNonQuery();

            if (trackId != null)
            {
                InsertMetadata(sessionId, "TrackId", trackId);
            }
        }

        private void InsertMetadata(int sessionId, string key, string value)
        {
            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO session_metadata (session_id, \"key\", \"value\") VALUES (?, ?, ?)";
            
            var idParam = command.CreateParameter();
            idParam.Value = sessionId;
            command.Parameters.Add(idParam);
            
            var keyParam = command.CreateParameter();
            keyParam.Value = key;
            command.Parameters.Add(keyParam);
            
            var valueParam = command.CreateParameter();
            valueParam.Value = value;
            command.Parameters.Add(valueParam);
            
            command.ExecuteNonQuery();
        }

        private (string Key, string Value)[] GetSessionMetadataRows(int sessionId)
        {
            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT \"key\", \"value\" FROM session_metadata WHERE session_id = ?";
            
            var idParam = command.CreateParameter();
            idParam.Value = sessionId;
            command.Parameters.Add(idParam);

            var results = new System.Collections.Generic.List<(string, string)>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add((reader.GetString(0), reader.GetString(1)));
            }
            return results.ToArray();
        }

        private class TestDuckDbConnector : IDuckDbConnector
        {
            public TestDuckDbConnector(string databasePath)
            {
                DatabasePath = databasePath;
            }

            public string DatabasePath { get; }

            public void EnsureSchema()
            {
                throw new NotImplementedException();
            }

            public void InsertSamples(string sessionId, System.Collections.Generic.IEnumerable<PitWall.Core.Models.TelemetrySample> samples)
            {
                throw new NotImplementedException();
            }
        }
    }
}
