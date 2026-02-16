using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using PitWall.Telemetry.Live.Models;
using PitWall.Telemetry.Live.Services;
using PitWall.Telemetry.Live.Storage;
using Xunit;

namespace PitWall.Telemetry.Live.Tests
{
    /// <summary>
    /// TDD tests for DuckDbTelemetryWriter — batch database writer for telemetry.
    /// Written FIRST per TDD RED phase.
    /// </summary>
    public class DuckDbTelemetryWriterTests : IAsyncLifetime
    {
        private DuckDBConnection _connection = null!;
        private TelemetryDatabaseSchema _schema = null!;

        public Task InitializeAsync()
        {
            _connection = new DuckDBConnection("DataSource=:memory:");
            _connection.Open();
            _schema = new TelemetryDatabaseSchema();
            _schema.CreateTables(_connection);
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            _connection?.Dispose();
            await Task.CompletedTask;
        }

        #region Constructor

        [Fact]
        public void Constructor_WithNullConnection_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new DuckDbTelemetryWriter(null!));
        }

        [Fact]
        public void Constructor_ImplementsITelemetryWriter()
        {
            var writer = new DuckDbTelemetryWriter(_connection);
            Assert.IsAssignableFrom<ITelemetryWriter>(writer);
        }

        [Fact]
        public void Constructor_DefaultBatchSize_Is500()
        {
            var writer = new DuckDbTelemetryWriter(_connection);
            Assert.Equal(0, writer.PendingCount);
        }

        #endregion

        #region WriteSessionAsync

        [Fact]
        public async Task WriteSessionAsync_InsertsIntoSessionsTable()
        {
            // Arrange
            await using var writer = new DuckDbTelemetryWriter(_connection);
            var snapshot = CreateSnapshot(sessionId: "test-session-1");
            snapshot.Session = new SessionInfo
            {
                StartTimeUtc = new DateTime(2026, 2, 16, 12, 0, 0, DateTimeKind.Utc),
                TrackName = "Monza Curva Grande Circuit",
                SessionType = "Race",
                NumVehicles = 25,
                TrackLength = 5744.0
            };

            // Act
            await writer.WriteSessionAsync(snapshot);

            // Assert
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT session_id, track_name, num_vehicles, track_length FROM live_sessions";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("test-session-1", reader.GetString(0));
            Assert.Equal("Monza Curva Grande Circuit", reader.GetString(1));
            Assert.Equal(25, reader.GetInt32(2));
            Assert.Equal(5744.0, reader.GetDouble(3));
            Assert.False(reader.Read()); // only one row
        }

        [Fact]
        public async Task WriteSessionAsync_WithNullSession_UsesDefaults()
        {
            // Arrange
            await using var writer = new DuckDbTelemetryWriter(_connection);
            var snapshot = CreateSnapshot(sessionId: "test-session-2");
            snapshot.Session = null;

            // Act
            await writer.WriteSessionAsync(snapshot);

            // Assert
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT session_id FROM live_sessions WHERE session_id = 'test-session-2'";
            var result = cmd.ExecuteScalar();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task WriteSessionAsync_DuplicateSession_DoesNotThrow()
        {
            // Arrange
            await using var writer = new DuckDbTelemetryWriter(_connection);
            var snapshot = CreateSnapshot(sessionId: "dup-session");

            // Act — write twice
            await writer.WriteSessionAsync(snapshot);
            var ex = await Record.ExceptionAsync(() => writer.WriteSessionAsync(snapshot));

            // Assert — should handle gracefully (upsert or ignore)
            Assert.Null(ex);
        }

        #endregion

        #region WriteSampleAsync — immediate write

        [Fact]
        public async Task WriteSampleAsync_SingleSample_IncrementsPendingCount()
        {
            // Arrange
            await using var writer = new DuckDbTelemetryWriter(_connection, batchSize: 100);
            var snapshot = CreateSnapshot();

            // Act
            await writer.WriteSampleAsync(snapshot);

            // Assert
            Assert.Equal(1, writer.PendingCount);
        }

        [Fact]
        public async Task WriteSampleAsync_AtBatchSize_FlushesBatch()
        {
            // Arrange
            int batchSize = 5;
            await using var writer = new DuckDbTelemetryWriter(_connection, batchSize: batchSize);

            // Act — write exactly batchSize samples
            for (int i = 0; i < batchSize; i++)
            {
                var snapshot = CreateSnapshot(
                    sessionId: "batch-test",
                    timestamp: DateTime.UtcNow.AddMilliseconds(i * 10));
                await writer.WriteSampleAsync(snapshot);
            }

            // Assert — batch should have been flushed, pending back to 0
            Assert.Equal(0, writer.PendingCount);

            // Verify samples in DB
            var count = GetRowCount("live_telemetry_samples");
            Assert.Equal(batchSize, count);
        }

        [Fact]
        public async Task WriteSampleAsync_BelowBatchSize_DoesNotWriteToDB()
        {
            // Arrange
            int batchSize = 10;
            await using var writer = new DuckDbTelemetryWriter(_connection, batchSize: batchSize);

            // Act — write fewer than batchSize samples
            for (int i = 0; i < batchSize - 1; i++)
            {
                var snapshot = CreateSnapshot(
                    sessionId: "pending-test",
                    timestamp: DateTime.UtcNow.AddMilliseconds(i * 10));
                await writer.WriteSampleAsync(snapshot);
            }

            // Assert — nothing in DB yet
            var count = GetRowCount("live_telemetry_samples");
            Assert.Equal(0, count);
            Assert.Equal(batchSize - 1, writer.PendingCount);
        }

        [Fact]
        public async Task WriteSampleAsync_MapsVehicleFieldsCorrectly()
        {
            // Arrange
            await using var writer = new DuckDbTelemetryWriter(_connection, batchSize: 1); // flush immediately
            var timestamp = new DateTime(2026, 2, 16, 14, 30, 0, DateTimeKind.Utc);
            var snapshot = CreateSnapshot(sessionId: "fields-test", timestamp: timestamp);
            snapshot.PlayerVehicle = new VehicleTelemetry
            {
                VehicleId = 0,
                IsPlayer = true,
                Speed = 250.5,
                Rpm = 8500.0,
                Gear = 4,
                Throttle = 0.95,
                Brake = 0.0,
                Steering = -0.05,
                Fuel = 42.0,
                PosX = 100.0,
                PosY = 2.5,
                PosZ = -200.0,
                Wheels = new[]
                {
                    new WheelData { TempInner = 90, TempMid = 95, TempOuter = 92, Wear = 85, Pressure = 1.8, BrakeTemp = 400, SuspDeflection = 0.05 },
                    new WheelData { TempInner = 91, TempMid = 96, TempOuter = 93, Wear = 86, Pressure = 1.9, BrakeTemp = 410, SuspDeflection = 0.06 },
                    new WheelData { TempInner = 89, TempMid = 94, TempOuter = 91, Wear = 84, Pressure = 1.7, BrakeTemp = 380, SuspDeflection = 0.04 },
                    new WheelData { TempInner = 90, TempMid = 95, TempOuter = 92, Wear = 85, Pressure = 1.8, BrakeTemp = 390, SuspDeflection = 0.05 }
                }
            };
            snapshot.AllVehicles = new List<VehicleTelemetry> { snapshot.PlayerVehicle };

            // Act
            await writer.WriteSampleAsync(snapshot);

            // Assert
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"SELECT speed, rpm, gear, throttle, brake, steering, fuel,
                                       pos_x, pos_y, pos_z,
                                       fl_temp_mid, fr_temp_mid, rl_temp_mid, rr_temp_mid,
                                       fl_wear, fr_wear, rl_wear, rr_wear,
                                       fl_brake_temp, fr_brake_temp, rl_brake_temp, rr_brake_temp
                                FROM live_telemetry_samples WHERE session_id = 'fields-test'";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(250.5, reader.GetDouble(0));   // speed
            Assert.Equal(8500.0, reader.GetDouble(1));  // rpm
            Assert.Equal(4, reader.GetInt32(2));         // gear
            Assert.Equal(0.95, reader.GetDouble(3), 2); // throttle
            Assert.Equal(0.0, reader.GetDouble(4));      // brake
            Assert.Equal(-0.05, reader.GetDouble(5), 2); // steering
            Assert.Equal(42.0, reader.GetDouble(6));     // fuel
            Assert.Equal(100.0, reader.GetDouble(7));    // pos_x
            Assert.Equal(2.5, reader.GetDouble(8));      // pos_y
            Assert.Equal(-200.0, reader.GetDouble(9));   // pos_z
            Assert.Equal(95.0, reader.GetDouble(10));    // fl_temp_mid
            Assert.Equal(96.0, reader.GetDouble(11));    // fr_temp_mid
            Assert.Equal(94.0, reader.GetDouble(12));    // rl_temp_mid
            Assert.Equal(95.0, reader.GetDouble(13));    // rr_temp_mid
            Assert.Equal(85.0, reader.GetDouble(14));    // fl_wear
            Assert.Equal(86.0, reader.GetDouble(15));    // fr_wear
            Assert.Equal(84.0, reader.GetDouble(16));    // rl_wear
            Assert.Equal(85.0, reader.GetDouble(17));    // rr_wear
            Assert.Equal(400.0, reader.GetDouble(18));   // fl_brake_temp
            Assert.Equal(410.0, reader.GetDouble(19));   // fr_brake_temp
            Assert.Equal(380.0, reader.GetDouble(20));   // rl_brake_temp
            Assert.Equal(390.0, reader.GetDouble(21));   // rr_brake_temp
        }

        [Fact]
        public async Task WriteSampleAsync_WritesAllVehicles()
        {
            // Arrange
            await using var writer = new DuckDbTelemetryWriter(_connection, batchSize: 1);
            var snapshot = CreateSnapshot(sessionId: "multi-vehicle");
            snapshot.AllVehicles = new List<VehicleTelemetry>
            {
                new VehicleTelemetry { VehicleId = 0, IsPlayer = true, Speed = 200 },
                new VehicleTelemetry { VehicleId = 1, IsPlayer = false, Speed = 195 },
                new VehicleTelemetry { VehicleId = 2, IsPlayer = false, Speed = 198 }
            };

            // Act
            await writer.WriteSampleAsync(snapshot);

            // Assert — should write one row per vehicle
            var count = GetRowCount("live_telemetry_samples", "session_id = 'multi-vehicle'");
            Assert.Equal(3, count);
        }

        #endregion

        #region WriteEventAsync

        [Fact]
        public async Task WriteEventAsync_InsertsIntoEventsTable()
        {
            // Arrange
            await using var writer = new DuckDbTelemetryWriter(_connection);
            var eventJson = """{"magnitude": 15000.0, "zones": [0, 5, 10, 0, 0, 0, 0, 0]}""";

            // Act
            await writer.WriteEventAsync("event-session", 0, "damage", eventJson);

            // Assert
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT session_id, vehicle_id, event_type FROM live_events";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("event-session", reader.GetString(0));
            Assert.Equal(0, reader.GetInt32(1));
            Assert.Equal("damage", reader.GetString(2));
        }

        [Fact]
        public async Task WriteEventAsync_MultipleEventTypes()
        {
            // Arrange
            await using var writer = new DuckDbTelemetryWriter(_connection);

            // Act
            await writer.WriteEventAsync("event-session2", 0, "damage", """{"mag": 15000}""");
            await writer.WriteEventAsync("event-session2", 0, "flag_change", """{"sector": 2, "new": "yellow"}""");
            await writer.WriteEventAsync("event-session2", 1, "pit_entry", """{"lap": 5}""");

            // Assert
            var count = GetRowCount("live_events", "session_id = 'event-session2'");
            Assert.Equal(3, count);
        }

        #endregion

        #region FlushAsync

        [Fact]
        public async Task FlushAsync_WritesAllPendingSamples()
        {
            // Arrange
            int batchSize = 100;
            await using var writer = new DuckDbTelemetryWriter(_connection, batchSize: batchSize);

            // Write fewer than batchSize
            for (int i = 0; i < 7; i++)
            {
                var snapshot = CreateSnapshot(
                    sessionId: "flush-test",
                    timestamp: DateTime.UtcNow.AddMilliseconds(i * 10));
                await writer.WriteSampleAsync(snapshot);
            }
            Assert.Equal(7, writer.PendingCount);

            // Act
            await writer.FlushAsync();

            // Assert
            Assert.Equal(0, writer.PendingCount);
            var count = GetRowCount("live_telemetry_samples", "session_id = 'flush-test'");
            Assert.Equal(7, count);
        }

        [Fact]
        public async Task FlushAsync_WhenNoPending_DoesNotThrow()
        {
            // Arrange
            await using var writer = new DuckDbTelemetryWriter(_connection);

            // Act & Assert
            var ex = await Record.ExceptionAsync(() => writer.FlushAsync());
            Assert.Null(ex);
        }

        #endregion

        #region DisposeAsync

        [Fact]
        public async Task DisposeAsync_FlushesRemainingBatch()
        {
            // Arrange
            int batchSize = 100;
            var writer = new DuckDbTelemetryWriter(_connection, batchSize: batchSize);

            for (int i = 0; i < 3; i++)
            {
                var snapshot = CreateSnapshot(
                    sessionId: "dispose-test",
                    timestamp: DateTime.UtcNow.AddMilliseconds(i * 10));
                await writer.WriteSampleAsync(snapshot);
            }
            Assert.Equal(3, writer.PendingCount);

            // Act
            await writer.DisposeAsync();

            // Assert
            var count = GetRowCount("live_telemetry_samples", "session_id = 'dispose-test'");
            Assert.Equal(3, count);
        }

        #endregion

        #region Error recovery

        [Fact]
        public async Task WriteSampleAsync_WithNullPlayerVehicle_DoesNotThrow()
        {
            // Arrange
            await using var writer = new DuckDbTelemetryWriter(_connection, batchSize: 1);
            var snapshot = CreateSnapshot();
            snapshot.PlayerVehicle = null;
            snapshot.AllVehicles = new List<VehicleTelemetry>();

            // Act & Assert — should handle empty vehicle list
            var ex = await Record.ExceptionAsync(() => writer.WriteSampleAsync(snapshot));
            Assert.Null(ex);
        }

        #endregion

        #region Helpers

        private TelemetrySnapshot CreateSnapshot(
            string sessionId = "test-session",
            DateTime? timestamp = null)
        {
            var ts = timestamp ?? DateTime.UtcNow;
            var player = new VehicleTelemetry
            {
                VehicleId = 0,
                IsPlayer = true,
                Speed = 200.0,
                Rpm = 7000,
                Gear = 3,
                Throttle = 0.8,
                Brake = 0.1,
                Steering = 0.02,
                Fuel = 55.0
            };

            return new TelemetrySnapshot
            {
                Timestamp = ts,
                SessionId = sessionId,
                Session = new SessionInfo
                {
                    StartTimeUtc = ts,
                    TrackName = "TestTrack",
                    SessionType = "Practice",
                    NumVehicles = 1,
                    TrackLength = 4000.0
                },
                PlayerVehicle = player,
                AllVehicles = new List<VehicleTelemetry> { player },
                Scoring = new ScoringInfo { NumVehicles = 1, SectorFlags = new int[3] }
            };
        }

        private int GetRowCount(string tableName, string? where = null)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = where != null
                ? $"SELECT COUNT(*) FROM {tableName} WHERE {where}"
                : $"SELECT COUNT(*) FROM {tableName}";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        #endregion
    }
}
