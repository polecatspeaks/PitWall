using System;
using DuckDB.NET.Data;

namespace PitWall.Telemetry.Live.Storage
{
    /// <summary>
    /// Creates and manages the database schema for live telemetry storage.
    /// Based on the LMU telemetry schema with 231 fields discovered.
    /// Implements a normalized structure: sessions, laps, telemetry_samples, and events.
    /// </summary>
    public class TelemetryDatabaseSchema
    {
        /// <summary>
        /// Creates all required tables for telemetry storage.
        /// Can be called multiple times safely (uses IF NOT EXISTS).
        /// </summary>
        /// <param name="connection">Open DuckDB connection</param>
        /// <exception cref="ArgumentNullException">If connection is null</exception>
        public void CreateTables(DuckDBConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            CreateSessionsTable(connection);
            CreateLapsTable(connection);
            CreateTelemetrySamplesTable(connection);
            CreateEventsTable(connection);
        }

        private void CreateSessionsTable(DuckDBConnection conn)
        {
            using var command = conn.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS live_sessions (
                    session_id TEXT PRIMARY KEY,
                    start_time TIMESTAMP,
                    track_name TEXT,
                    session_type TEXT,
                    num_vehicles INTEGER,
                    track_length DOUBLE
                )";
            command.ExecuteNonQuery();
        }

        private void CreateLapsTable(DuckDBConnection conn)
        {
            using var command = conn.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS live_laps (
                    session_id TEXT,
                    vehicle_id INTEGER,
                    lap_number INTEGER,
                    lap_time DOUBLE,
                    sector1_time DOUBLE,
                    sector2_time DOUBLE,
                    sector3_time DOUBLE,
                    best_lap_time DOUBLE,
                    fuel_at_start DOUBLE,
                    fuel_at_end DOUBLE,
                    tire_compound_front TEXT,
                    tire_compound_rear TEXT,
                    avg_speed DOUBLE,
                    PRIMARY KEY (session_id, vehicle_id, lap_number)
                )";
            command.ExecuteNonQuery();
        }

        private void CreateTelemetrySamplesTable(DuckDBConnection conn)
        {
            using var command = conn.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS live_telemetry_samples (
                    session_id TEXT,
                    vehicle_id INTEGER,
                    timestamp TIMESTAMP,
                    elapsed_time DOUBLE,
                    -- Position
                    pos_x DOUBLE,
                    pos_y DOUBLE,
                    pos_z DOUBLE,
                    -- Motion
                    speed DOUBLE,
                    local_vel_x DOUBLE,
                    local_vel_y DOUBLE,
                    local_vel_z DOUBLE,
                    local_accel_x DOUBLE,
                    local_accel_y DOUBLE,
                    local_accel_z DOUBLE,
                    -- Engine
                    rpm DOUBLE,
                    gear INTEGER,
                    throttle DOUBLE,
                    brake DOUBLE,
                    steering DOUBLE,
                    fuel DOUBLE,
                    turbo_boost DOUBLE,
                    -- Tires (4 wheels × key metrics)
                    fl_temp_inner DOUBLE,
                    fl_temp_mid DOUBLE,
                    fl_temp_outer DOUBLE,
                    fr_temp_inner DOUBLE,
                    fr_temp_mid DOUBLE,
                    fr_temp_outer DOUBLE,
                    rl_temp_inner DOUBLE,
                    rl_temp_mid DOUBLE,
                    rl_temp_outer DOUBLE,
                    rr_temp_inner DOUBLE,
                    rr_temp_mid DOUBLE,
                    rr_temp_outer DOUBLE,
                    fl_wear DOUBLE,
                    fr_wear DOUBLE,
                    rl_wear DOUBLE,
                    rr_wear DOUBLE,
                    fl_pressure DOUBLE,
                    fr_pressure DOUBLE,
                    rl_pressure DOUBLE,
                    rr_pressure DOUBLE,
                    -- Brakes
                    fl_brake_temp DOUBLE,
                    fr_brake_temp DOUBLE,
                    rl_brake_temp DOUBLE,
                    rr_brake_temp DOUBLE,
                    -- Suspension
                    fl_susp_deflection DOUBLE,
                    fr_susp_deflection DOUBLE,
                    rl_susp_deflection DOUBLE,
                    rr_susp_deflection DOUBLE,
                    PRIMARY KEY (session_id, vehicle_id, timestamp)
                )";
            command.ExecuteNonQuery();
        }

        private void CreateEventsTable(DuckDBConnection conn)
        {
            using var command = conn.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS live_events (
                    session_id TEXT,
                    vehicle_id INTEGER,
                    timestamp TIMESTAMP,
                    event_type TEXT,
                    event_data JSON,
                    PRIMARY KEY (session_id, vehicle_id, timestamp, event_type)
                )";
            command.ExecuteNonQuery();
        }
    }
}
