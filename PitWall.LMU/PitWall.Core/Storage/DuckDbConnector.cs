using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        private readonly ILogger<DuckDbConnector> _logger;
        private const string TableGpsSpeed = "GPS Speed";
        private const string TableGpsTime = "GPS Time";
        private const string TableThrottle = "Throttle Pos";
        private const string TableBrake = "Brake Pos";
        private const string TableSteering = "Steering Pos";
        private const string TableFuel = "Fuel Level";
        private const string TableTyreTemps = "TyresTempCentre";

        public DuckDbConnector(string databasePath, ILogger<DuckDbConnector>? logger = null)
        {
            _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
            _logger = logger ?? NullLogger<DuckDbConnector>.Instance;
        }

        public string DatabasePath => _databasePath;

        public void EnsureSchema()
        {
            _logger.LogDebug("Ensuring DuckDB schema at {DatabasePath}.", _databasePath);
            using (var connection = new DuckDBConnection($"Data Source={_databasePath}"))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
CREATE TABLE IF NOT EXISTS ""GPS Speed"" (value FLOAT);
CREATE TABLE IF NOT EXISTS ""GPS Time"" (value DOUBLE);
CREATE TABLE IF NOT EXISTS ""Throttle Pos"" (value FLOAT);
CREATE TABLE IF NOT EXISTS ""Brake Pos"" (value FLOAT);
CREATE TABLE IF NOT EXISTS ""Steering Pos"" (value FLOAT);
CREATE TABLE IF NOT EXISTS ""Fuel Level"" (value FLOAT);
CREATE TABLE IF NOT EXISTS ""TyresTempCentre"" (value1 FLOAT, value2 FLOAT, value3 FLOAT, value4 FLOAT);
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

            _logger.LogDebug("Inserting {SampleCount} samples for session {SessionId}.", sampleList.Count, sessionId);

            using (var connection = new DuckDBConnection($"Data Source={_databasePath}"))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    using var speedCommand = CreateInsertCommand(connection, transaction, TableGpsSpeed, "value");
                    using var timeCommand = CreateInsertCommand(connection, transaction, TableGpsTime, "value");
                    using var throttleCommand = CreateInsertCommand(connection, transaction, TableThrottle, "value");
                    using var brakeCommand = CreateInsertCommand(connection, transaction, TableBrake, "value");
                    using var steeringCommand = CreateInsertCommand(connection, transaction, TableSteering, "value");
                    using var fuelCommand = CreateInsertCommand(connection, transaction, TableFuel, "value");
                    using var tempsCommand = CreateInsertCommand(connection, transaction, TableTyreTemps, "value1", "value2", "value3", "value4");

                    foreach (var sample in sampleList)
                    {
                        var speedMps = sample.SpeedKph / 3.6;
                        var gpsTime = (sample.Timestamp - DateTime.UnixEpoch).TotalSeconds;
                        var tyreTemps = sample.TyreTempsC ?? Array.Empty<double>();

                        speedCommand.Parameters[0].Value = speedMps;
                        timeCommand.Parameters[0].Value = gpsTime;
                        throttleCommand.Parameters[0].Value = sample.Throttle;
                        brakeCommand.Parameters[0].Value = sample.Brake;
                        steeringCommand.Parameters[0].Value = sample.Steering;
                        fuelCommand.Parameters[0].Value = sample.FuelLiters;

                        tempsCommand.Parameters[0].Value = tyreTemps.Length > 0 ? tyreTemps[0] : 0.0;
                        tempsCommand.Parameters[1].Value = tyreTemps.Length > 1 ? tyreTemps[1] : 0.0;
                        tempsCommand.Parameters[2].Value = tyreTemps.Length > 2 ? tyreTemps[2] : 0.0;
                        tempsCommand.Parameters[3].Value = tyreTemps.Length > 3 ? tyreTemps[3] : 0.0;

                        speedCommand.ExecuteNonQuery();
                        timeCommand.ExecuteNonQuery();
                        throttleCommand.ExecuteNonQuery();
                        brakeCommand.ExecuteNonQuery();
                        steeringCommand.ExecuteNonQuery();
                        fuelCommand.ExecuteNonQuery();
                        tempsCommand.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }

            _logger.LogDebug("Insert completed for session {SessionId}.", sessionId);
        }

        private static DuckDBCommand CreateInsertCommand(
            DuckDBConnection connection,
            DuckDBTransaction transaction,
            string tableName,
            params string[] columns)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;

            var quotedColumns = string.Join(", ", columns.Select(col => $"\"{col}\""));
            var placeholders = string.Join(", ", Enumerable.Repeat("?", columns.Length));
            command.CommandText = $"INSERT INTO \"{tableName}\" ({quotedColumns}) VALUES ({placeholders})";

            foreach (var column in columns)
            {
                var parameter = command.CreateParameter();
                command.Parameters.Add(parameter);
            }

            command.Prepare();
            return command;
        }
    }
}
