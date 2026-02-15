using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DuckDB.NET.Data;
using PitWall.Telemetry.Live.Storage;
using Xunit;

namespace PitWall.Telemetry.Live.Tests
{
    /// <summary>
    /// Tests for database schema creation following TDD methodology.
    /// These tests are written FIRST, then implementation follows.
    /// </summary>
    public class DatabaseSchemaTests : IDisposable
    {
        private DuckDBConnection? _connection;

        public void Dispose()
        {
            _connection?.Dispose();
        }

        private DuckDBConnection CreateInMemoryDatabase()
        {
            _connection = new DuckDBConnection("DataSource=:memory:");
            _connection.Open();
            return _connection;
        }

        private List<string> GetTableColumns(DuckDBConnection connection, string tableName)
        {
            var columns = new List<string>();
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info('{tableName}')";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.GetString(1)); // Column name is at index 1
            }
            return columns;
        }

        private bool TableExists(DuckDBConnection connection, string tableName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) 
                FROM information_schema.tables 
                WHERE table_name = $1";
            var param = command.CreateParameter();
            param.Value = tableName;
            command.Parameters.Add(param);
            var result = command.ExecuteScalar();
            return Convert.ToInt32(result) > 0;
        }

        private List<string> GetPrimaryKeyColumns(DuckDBConnection connection, string tableName)
        {
            var pkColumns = new List<string>();
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info('{tableName}')";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var isPk = reader.GetBoolean(5); // pk flag is at index 5
                if (isPk)
                {
                    pkColumns.Add(reader.GetString(1)); // Column name is at index 1
                }
            }
            return pkColumns;
        }

        [Fact]
        public void CreateSchema_CreatesSessionsTable_WithCorrectColumns()
        {
            // Arrange
            using var db = CreateInMemoryDatabase();
            var schema = new TelemetryDatabaseSchema();

            // Act
            schema.CreateTables(db);

            // Assert
            Assert.True(TableExists(db, "sessions"));
            var columns = GetTableColumns(db, "sessions");
            
            // Essential session metadata
            Assert.Contains("session_id", columns);
            Assert.Contains("start_time", columns);
            Assert.Contains("track_name", columns);
            Assert.Contains("session_type", columns);
            Assert.Contains("num_vehicles", columns);
            Assert.Contains("track_length", columns);
        }

        [Fact]
        public void CreateSchema_SessionsTable_HasSessionIdPrimaryKey()
        {
            // Arrange
            using var db = CreateInMemoryDatabase();
            var schema = new TelemetryDatabaseSchema();

            // Act
            schema.CreateTables(db);

            // Assert
            var pkColumns = GetPrimaryKeyColumns(db, "sessions");
            Assert.Single(pkColumns);
            Assert.Equal("session_id", pkColumns[0]);
        }

        [Fact]
        public void CreateSchema_CreatesLapsTable_WithCorrectColumns()
        {
            // Arrange
            using var db = CreateInMemoryDatabase();
            var schema = new TelemetryDatabaseSchema();

            // Act
            schema.CreateTables(db);

            // Assert
            Assert.True(TableExists(db, "laps"));
            var columns = GetTableColumns(db, "laps");
            
            // Primary key components
            Assert.Contains("session_id", columns);
            Assert.Contains("vehicle_id", columns);
            Assert.Contains("lap_number", columns);
            
            // Lap timing data
            Assert.Contains("lap_time", columns);
            Assert.Contains("sector1_time", columns);
            Assert.Contains("sector2_time", columns);
            Assert.Contains("sector3_time", columns);
            
            // Additional metrics
            Assert.Contains("fuel_at_start", columns);
            Assert.Contains("fuel_at_end", columns);
            Assert.Contains("avg_speed", columns);
        }

        [Fact]
        public void CreateSchema_LapsTable_HasCompositePrimaryKey()
        {
            // Arrange
            using var db = CreateInMemoryDatabase();
            var schema = new TelemetryDatabaseSchema();

            // Act
            schema.CreateTables(db);

            // Assert
            var pkColumns = GetPrimaryKeyColumns(db, "laps");
            Assert.Equal(3, pkColumns.Count);
            Assert.Contains("session_id", pkColumns);
            Assert.Contains("vehicle_id", pkColumns);
            Assert.Contains("lap_number", pkColumns);
        }

        [Fact]
        public void CreateSchema_CreatesTelemetrySamplesTable_WithCorrectColumns()
        {
            // Arrange
            using var db = CreateInMemoryDatabase();
            var schema = new TelemetryDatabaseSchema();

            // Act
            schema.CreateTables(db);

            // Assert
            Assert.True(TableExists(db, "telemetry_samples"));
            var columns = GetTableColumns(db, "telemetry_samples");
            
            // Primary key components
            Assert.Contains("session_id", columns);
            Assert.Contains("vehicle_id", columns);
            Assert.Contains("timestamp", columns);
            
            // Physics data
            Assert.Contains("elapsed_time", columns);
            Assert.Contains("speed", columns);
            Assert.Contains("rpm", columns);
            Assert.Contains("gear", columns);
            
            // Driver inputs
            Assert.Contains("throttle", columns);
            Assert.Contains("brake", columns);
            Assert.Contains("steering", columns);
            
            // Fuel
            Assert.Contains("fuel", columns);
            
            // Position data
            Assert.Contains("pos_x", columns);
            Assert.Contains("pos_y", columns);
            Assert.Contains("pos_z", columns);
        }

        [Fact]
        public void CreateSchema_TelemetrySamplesTable_HasTimestampKey()
        {
            // Arrange
            using var db = CreateInMemoryDatabase();
            var schema = new TelemetryDatabaseSchema();

            // Act
            schema.CreateTables(db);

            // Assert
            var pkColumns = GetPrimaryKeyColumns(db, "telemetry_samples");
            Assert.Equal(3, pkColumns.Count);
            Assert.Contains("session_id", pkColumns);
            Assert.Contains("vehicle_id", pkColumns);
            Assert.Contains("timestamp", pkColumns);
        }

        [Fact]
        public void CreateSchema_TelemetrySamplesTable_IncludesTyreData()
        {
            // Arrange
            using var db = CreateInMemoryDatabase();
            var schema = new TelemetryDatabaseSchema();

            // Act
            schema.CreateTables(db);

            // Assert
            var columns = GetTableColumns(db, "telemetry_samples");
            
            // Tyre temperatures (3 zones per tyre)
            Assert.Contains("fl_temp_inner", columns);
            Assert.Contains("fl_temp_mid", columns);
            Assert.Contains("fl_temp_outer", columns);
            Assert.Contains("rr_temp_outer", columns);
            
            // Tyre wear
            Assert.Contains("fl_wear", columns);
            Assert.Contains("fr_wear", columns);
            
            // Tyre pressure
            Assert.Contains("fl_pressure", columns);
            Assert.Contains("rr_pressure", columns);
        }

        [Fact]
        public void CreateSchema_TelemetrySamplesTable_IncludesBrakeData()
        {
            // Arrange
            using var db = CreateInMemoryDatabase();
            var schema = new TelemetryDatabaseSchema();

            // Act
            schema.CreateTables(db);

            // Assert
            var columns = GetTableColumns(db, "telemetry_samples");
            
            Assert.Contains("fl_brake_temp", columns);
            Assert.Contains("fr_brake_temp", columns);
            Assert.Contains("rl_brake_temp", columns);
            Assert.Contains("rr_brake_temp", columns);
        }

        [Fact]
        public void CreateSchema_TelemetrySamplesTable_IncludesSuspensionData()
        {
            // Arrange
            using var db = CreateInMemoryDatabase();
            var schema = new TelemetryDatabaseSchema();

            // Act
            schema.CreateTables(db);

            // Assert
            var columns = GetTableColumns(db, "telemetry_samples");
            
            Assert.Contains("fl_susp_deflection", columns);
            Assert.Contains("fr_susp_deflection", columns);
            Assert.Contains("rl_susp_deflection", columns);
            Assert.Contains("rr_susp_deflection", columns);
        }

        [Fact]
        public void CreateSchema_CreatesEventsTable_WithCorrectColumns()
        {
            // Arrange
            using var db = CreateInMemoryDatabase();
            var schema = new TelemetryDatabaseSchema();

            // Act
            schema.CreateTables(db);

            // Assert
            Assert.True(TableExists(db, "events"));
            var columns = GetTableColumns(db, "events");
            
            Assert.Contains("session_id", columns);
            Assert.Contains("vehicle_id", columns);
            Assert.Contains("timestamp", columns);
            Assert.Contains("event_type", columns);
            Assert.Contains("event_data", columns);
        }

        [Fact]
        public void CreateSchema_EventsTable_HasEventTypeInKey()
        {
            // Arrange
            using var db = CreateInMemoryDatabase();
            var schema = new TelemetryDatabaseSchema();

            // Act
            schema.CreateTables(db);

            // Assert
            var pkColumns = GetPrimaryKeyColumns(db, "events");
            Assert.Equal(4, pkColumns.Count);
            Assert.Contains("session_id", pkColumns);
            Assert.Contains("vehicle_id", pkColumns);
            Assert.Contains("timestamp", pkColumns);
            Assert.Contains("event_type", pkColumns);
        }

        [Fact]
        public void CreateSchema_CanBeCalledMultipleTimes_WithoutError()
        {
            // Arrange
            using var db = CreateInMemoryDatabase();
            var schema = new TelemetryDatabaseSchema();

            // Act - create tables twice
            schema.CreateTables(db);
            schema.CreateTables(db); // Should not throw

            // Assert
            Assert.True(TableExists(db, "sessions"));
            Assert.True(TableExists(db, "laps"));
            Assert.True(TableExists(db, "telemetry_samples"));
            Assert.True(TableExists(db, "events"));
        }

        [Fact]
        public void CreateSchema_WithNullConnection_ThrowsArgumentNullException()
        {
            // Arrange
            var schema = new TelemetryDatabaseSchema();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => schema.CreateTables(null!));
        }
    }
}
