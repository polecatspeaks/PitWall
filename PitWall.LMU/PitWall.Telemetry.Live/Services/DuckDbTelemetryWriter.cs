using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using PitWall.Telemetry.Live.Models;

namespace PitWall.Telemetry.Live.Services
{
    /// <summary>
    /// Writes telemetry data to DuckDB with batch buffering for high-throughput ingestion.
    /// Batches sample writes and flushes when batch size is reached or on explicit flush/dispose.
    /// Thread-safety: Not thread-safe. Designed for single-writer pipeline usage.
    /// </summary>
    public class DuckDbTelemetryWriter : ITelemetryWriter
    {
        private readonly DuckDBConnection _connection;
        private readonly int _batchSize;
        private readonly List<TelemetrySnapshot> _pendingBatch = new();
        private bool _disposed;

        /// <summary>
        /// Creates a new DuckDB telemetry writer.
        /// </summary>
        /// <param name="connection">Open DuckDB connection (caller owns lifetime)</param>
        /// <param name="batchSize">Number of snapshots to buffer before auto-flush (default: 500)</param>
        /// <exception cref="ArgumentNullException">If connection is null</exception>
        public DuckDbTelemetryWriter(DuckDBConnection connection, int batchSize = 500)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _batchSize = batchSize;
        }

        /// <inheritdoc />
        public int PendingCount => _pendingBatch.Count;

        /// <inheritdoc />
        public async Task WriteSessionAsync(TelemetrySnapshot snapshot)
        {
            var session = snapshot.Session;
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR IGNORE INTO live_sessions 
                (session_id, start_time, track_name, session_type, num_vehicles, track_length)
                VALUES (?, ?, ?, ?, ?, ?)";

            AddParameter(cmd, snapshot.SessionId);
            AddParameter(cmd, session?.StartTimeUtc ?? DateTime.UtcNow);
            AddParameter(cmd, session?.TrackName ?? "Unknown");
            AddParameter(cmd, session?.SessionType ?? "Unknown");
            AddParameter(cmd, session?.NumVehicles ?? 0);
            AddParameter(cmd, session?.TrackLength ?? 0.0);

            await Task.Run(() => cmd.ExecuteNonQuery());
        }

        /// <inheritdoc />
        public async Task WriteSampleAsync(TelemetrySnapshot snapshot)
        {
            _pendingBatch.Add(snapshot);
            if (_pendingBatch.Count >= _batchSize)
            {
                await FlushAsync();
            }
        }

        /// <inheritdoc />
        public async Task WriteEventAsync(string sessionId, int vehicleId, string eventType, string eventDataJson)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO live_events 
                (session_id, vehicle_id, timestamp, event_type, event_data)
                VALUES (?, ?, ?, ?, ?)";

            AddParameter(cmd, sessionId);
            AddParameter(cmd, vehicleId);
            AddParameter(cmd, DateTime.UtcNow);
            AddParameter(cmd, eventType);
            AddParameter(cmd, eventDataJson);

            await Task.Run(() => cmd.ExecuteNonQuery());
        }

        /// <inheritdoc />
        public async Task FlushAsync()
        {
            if (_pendingBatch.Count == 0) return;

            var batch = new List<TelemetrySnapshot>(_pendingBatch);
            _pendingBatch.Clear();

            foreach (var snapshot in batch)
            {
                var vehicles = snapshot.AllVehicles;
                if (vehicles == null || vehicles.Count == 0) continue;

                foreach (var vehicle in vehicles)
                {
                    await InsertSampleRowAsync(snapshot, vehicle);
                }
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await FlushAsync();
                _disposed = true;
            }
        }

        private async Task InsertSampleRowAsync(TelemetrySnapshot snapshot, VehicleTelemetry vehicle)
        {
            var wheels = vehicle.Wheels;
            var fl = wheels is { Length: > 0 } ? wheels[0] : new WheelData();
            var fr = wheels is { Length: > 1 } ? wheels[1] : new WheelData();
            var rl = wheels is { Length: > 2 } ? wheels[2] : new WheelData();
            var rr = wheels is { Length: > 3 } ? wheels[3] : new WheelData();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO live_telemetry_samples (
                    session_id, vehicle_id, timestamp, elapsed_time,
                    pos_x, pos_y, pos_z,
                    speed, local_vel_x, local_vel_y, local_vel_z,
                    local_accel_x, local_accel_y, local_accel_z,
                    rpm, gear, throttle, brake, steering, fuel, turbo_boost,
                    fl_temp_inner, fl_temp_mid, fl_temp_outer,
                    fr_temp_inner, fr_temp_mid, fr_temp_outer,
                    rl_temp_inner, rl_temp_mid, rl_temp_outer,
                    rr_temp_inner, rr_temp_mid, rr_temp_outer,
                    fl_wear, fr_wear, rl_wear, rr_wear,
                    fl_pressure, fr_pressure, rl_pressure, rr_pressure,
                    fl_brake_temp, fr_brake_temp, rl_brake_temp, rr_brake_temp,
                    fl_susp_deflection, fr_susp_deflection, rl_susp_deflection, rr_susp_deflection
                ) VALUES (
                    ?, ?, ?, ?,
                    ?, ?, ?,
                    ?, ?, ?, ?,
                    ?, ?, ?,
                    ?, ?, ?, ?, ?, ?, ?,
                    ?, ?, ?,
                    ?, ?, ?,
                    ?, ?, ?,
                    ?, ?, ?,
                    ?, ?, ?, ?,
                    ?, ?, ?, ?,
                    ?, ?, ?, ?,
                    ?, ?, ?, ?
                )";

            // Identity
            AddParameter(cmd, snapshot.SessionId);       // session_id
            AddParameter(cmd, vehicle.VehicleId);        // vehicle_id
            AddParameter(cmd, snapshot.Timestamp);        // timestamp
            AddParameter(cmd, vehicle.ElapsedTime);       // elapsed_time

            // Position
            AddParameter(cmd, vehicle.PosX);             // pos_x
            AddParameter(cmd, vehicle.PosY);             // pos_y
            AddParameter(cmd, vehicle.PosZ);             // pos_z

            // Motion
            AddParameter(cmd, vehicle.Speed);            // speed
            AddParameter(cmd, vehicle.LocalVelX);        // local_vel_x
            AddParameter(cmd, vehicle.LocalVelY);        // local_vel_y
            AddParameter(cmd, vehicle.LocalVelZ);        // local_vel_z
            AddParameter(cmd, 0.0);                      // local_accel_x (not yet in model)
            AddParameter(cmd, 0.0);                      // local_accel_y
            AddParameter(cmd, 0.0);                      // local_accel_z

            // Engine
            AddParameter(cmd, vehicle.Rpm);              // rpm
            AddParameter(cmd, vehicle.Gear);             // gear
            AddParameter(cmd, vehicle.Throttle);         // throttle
            AddParameter(cmd, vehicle.Brake);            // brake
            AddParameter(cmd, vehicle.Steering);         // steering
            AddParameter(cmd, vehicle.Fuel);             // fuel
            AddParameter(cmd, 0.0);                      // turbo_boost (not yet in model)

            // Tire temps — FL
            AddParameter(cmd, fl.TempInner);             // fl_temp_inner
            AddParameter(cmd, fl.TempMid);               // fl_temp_mid
            AddParameter(cmd, fl.TempOuter);             // fl_temp_outer
            // FR
            AddParameter(cmd, fr.TempInner);             // fr_temp_inner
            AddParameter(cmd, fr.TempMid);               // fr_temp_mid
            AddParameter(cmd, fr.TempOuter);             // fr_temp_outer
            // RL
            AddParameter(cmd, rl.TempInner);             // rl_temp_inner
            AddParameter(cmd, rl.TempMid);               // rl_temp_mid
            AddParameter(cmd, rl.TempOuter);             // rl_temp_outer
            // RR
            AddParameter(cmd, rr.TempInner);             // rr_temp_inner
            AddParameter(cmd, rr.TempMid);               // rr_temp_mid
            AddParameter(cmd, rr.TempOuter);             // rr_temp_outer

            // Wear
            AddParameter(cmd, fl.Wear);                  // fl_wear
            AddParameter(cmd, fr.Wear);                  // fr_wear
            AddParameter(cmd, rl.Wear);                  // rl_wear
            AddParameter(cmd, rr.Wear);                  // rr_wear

            // Pressure
            AddParameter(cmd, fl.Pressure);              // fl_pressure
            AddParameter(cmd, fr.Pressure);              // fr_pressure
            AddParameter(cmd, rl.Pressure);              // rl_pressure
            AddParameter(cmd, rr.Pressure);              // rr_pressure

            // Brake temps
            AddParameter(cmd, fl.BrakeTemp);             // fl_brake_temp
            AddParameter(cmd, fr.BrakeTemp);             // fr_brake_temp
            AddParameter(cmd, rl.BrakeTemp);             // rl_brake_temp
            AddParameter(cmd, rr.BrakeTemp);             // rr_brake_temp

            // Suspension
            AddParameter(cmd, fl.SuspDeflection);        // fl_susp_deflection
            AddParameter(cmd, fr.SuspDeflection);        // fr_susp_deflection
            AddParameter(cmd, rl.SuspDeflection);        // rl_susp_deflection
            AddParameter(cmd, rr.SuspDeflection);        // rr_susp_deflection

            await Task.Run(() => cmd.ExecuteNonQuery());
        }

        /// <summary>
        /// Helper to add a positional parameter to a command.
        /// </summary>
        private static void AddParameter(DuckDBCommand cmd, object value)
        {
            var param = cmd.CreateParameter();
            param.Value = value;
            cmd.Parameters.Add(param);
        }
    }
}
