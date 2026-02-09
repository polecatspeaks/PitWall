using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DuckDB.NET.Data;
using PitWall.Core.Models;

namespace PitWall.Core.Storage
{
    /// <summary>
    /// DuckDB connector that persists telemetry samples to a local DuckDB database.
    /// Schema is created on first EnsureSchema call; InsertSamples batches writes.
    /// </summary>
    public class DuckDbConnector : IDuckDbConnector
    {
        private readonly string _databasePath;
        private const string TelemetryTableName = "telemetry_samples";

        public DuckDbConnector(string databasePath)
        {
            _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        }

        public void EnsureSchema()
        {
            using (var connection = new DuckDBConnection($"Data Source={_databasePath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"
CREATE TABLE IF NOT EXISTS {TelemetryTableName} (
    timestamp TIMESTAMP,
    speed_kph DOUBLE,
    tyre_temps_c VARCHAR,
    fuel_liters DOUBLE,
    brake_input DOUBLE,
    throttle_input DOUBLE,
    steering_input DOUBLE,
    session_id VARCHAR
);
";
                    command.ExecuteNonQuery();
                }
            }
        }

        public void InsertSamples(string sessionId, IEnumerable<TelemetrySample> samples)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("Session ID is required.", nameof(sessionId));

            var sampleList = samples?.ToList() ?? new List<TelemetrySample>();
            if (sampleList.Count == 0)
                return;

            using (var connection = new DuckDBConnection($"Data Source={_databasePath}"))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = $@"
INSERT INTO {TelemetryTableName} 
(timestamp, speed_kph, tyre_temps_c, fuel_liters, brake_input, throttle_input, steering_input, session_id)
VALUES (?, ?, ?, ?, ?, ?, ?, ?)";

                        var timestampParam = command.CreateParameter();
                        timestampParam.ParameterName = "@timestamp";
                        command.Parameters.Add(timestampParam);

                        var speedParam = command.CreateParameter();
                        speedParam.ParameterName = "@speed_kph";
                        command.Parameters.Add(speedParam);

                        var tyreTempsParam = command.CreateParameter();
                        tyreTempsParam.ParameterName = "@tyre_temps_c";
                        command.Parameters.Add(tyreTempsParam);

                        var fuelParam = command.CreateParameter();
                        fuelParam.ParameterName = "@fuel_liters";
                        command.Parameters.Add(fuelParam);

                        var brakeParam = command.CreateParameter();
                        brakeParam.ParameterName = "@brake_input";
                        command.Parameters.Add(brakeParam);

                        var throttleParam = command.CreateParameter();
                        throttleParam.ParameterName = "@throttle_input";
                        command.Parameters.Add(throttleParam);

                        var steeringParam = command.CreateParameter();
                        steeringParam.ParameterName = "@steering_input";
                        command.Parameters.Add(steeringParam);

                        var sessionParam = command.CreateParameter();
                        sessionParam.ParameterName = "@session_id";
                        command.Parameters.Add(sessionParam);

                        command.Prepare();

                        foreach (var sample in sampleList)
                        {
                            timestampParam.Value = sample.Timestamp;
                            speedParam.Value = sample.SpeedKph;
                            tyreTempsParam.Value = string.Join(",", sample.TyreTempsC);
                            fuelParam.Value = sample.FuelLiters;
                            brakeParam.Value = sample.Brake;
                            throttleParam.Value = sample.Throttle;
                            steeringParam.Value = sample.Steering;
                            sessionParam.Value = sessionId;

                            command.ExecuteNonQuery();
                        }
                    }
                    transaction.Commit();
                }
            }
        }
    }
}
