using System;
using System.Collections.Generic;
using System.Numerics;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Core.Models;

namespace PitWall.Core.Storage
{
    public class DuckDbTelemetryWriter : ITelemetryWriter
    {
        private readonly IDuckDbConnector _connector;
        private readonly ILogger<DuckDbTelemetryWriter> _logger;
        private readonly object _sessionRangeLock = new();
        private readonly Dictionary<int, (long StartRow, long EndRow)> _sessionRanges = new();
        private bool _sessionRangesLoaded;

        public DuckDbTelemetryWriter(IDuckDbConnector connector, ILogger<DuckDbTelemetryWriter>? logger = null)
        {
            _connector = connector ?? throw new ArgumentNullException(nameof(connector));
            _logger = logger ?? NullLogger<DuckDbTelemetryWriter>.Instance;
            _connector.EnsureSchema();
        }

        public List<TelemetrySample> GetSamples(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("Session ID is required.", nameof(sessionId));

            if (!int.TryParse(sessionId, out var sessionNumber))
            {
                _logger.LogWarning("Session ID '{SessionId}' is not numeric. Returning no samples.", sessionId);
                return new List<TelemetrySample>();
            }

            var samples = new List<TelemetrySample>();
            var databasePath = _connector.DatabasePath;

            _logger.LogDebug("Loading telemetry samples from {DatabasePath} for session {SessionId}.", databasePath, sessionId);

            using var connection = new DuckDBConnection($"Data Source={databasePath}");
            connection.Open();

            EnsureSessionRangesLoaded(connection);

            if (!_sessionRanges.TryGetValue(sessionNumber, out var range))
            {
                _logger.LogDebug("No session range found for session {SessionId}.", sessionId);
                return samples;
            }

            const int sampleLimit = 50;
            var rowEnd = range.EndRow;
            var rowStart = Math.Max(range.StartRow, range.EndRow - sampleLimit + 1);

            using var command = connection.CreateCommand();
            command.CommandText = BuildSampleQuery();

            var startParam = command.CreateParameter();
            startParam.Value = rowStart;
            command.Parameters.Add(startParam);

            var endParam = command.CreateParameter();
            endParam.Value = rowEnd;
            command.Parameters.Add(endParam);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var gpsTime = GetDouble(reader, 1);
                var speedMps = GetDouble(reader, 2);
                var throttle = GetDouble(reader, 3);
                var brake = GetDouble(reader, 4);
                var steering = GetDouble(reader, 5);
                var fuel = GetDouble(reader, 6);

                var tyreTemps = new double[4];
                tyreTemps[0] = GetDouble(reader, 7);
                tyreTemps[1] = GetDouble(reader, 8);
                tyreTemps[2] = GetDouble(reader, 9);
                tyreTemps[3] = GetDouble(reader, 10);

                var timestamp = gpsTime <= 0
                    ? DateTime.UtcNow
                    : DateTime.UnixEpoch.AddSeconds(gpsTime);

                samples.Add(new TelemetrySample(
                    timestamp,
                    speedMps * 3.6,
                    tyreTemps,
                    fuel,
                    brake,
                    throttle,
                    steering));
            }

            _logger.LogDebug("Retrieved {SampleCount} telemetry samples for session {SessionId}.", samples.Count, sessionId);
            return samples;
        }

        public void WriteSamples(string sessionId, List<TelemetrySample> samples)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            _logger.LogDebug("Writing {SampleCount} samples for session {SessionId}.", samples.Count, sessionId);
            _connector.InsertSamples(sessionId, samples);
        }

        private void EnsureSessionRangesLoaded(DuckDBConnection connection)
        {
            if (_sessionRangesLoaded)
                return;

            lock (_sessionRangeLock)
            {
                if (_sessionRangesLoaded)
                    return;

                using var command = connection.CreateCommand();
                command.CommandText = @"
WITH lap AS (
    SELECT row_number() OVER () AS rn, value AS lap
    FROM ""Lap""
),
marks AS (
    SELECT rn,
           lap,
           CASE WHEN lap < lag(lap) OVER (ORDER BY rn) THEN 1 ELSE 0 END AS reset_flag
    FROM lap
),
sessions AS (
    SELECT rn,
           1 + sum(reset_flag) OVER (ORDER BY rn) AS session_id
    FROM marks
)
SELECT session_id, MIN(rn) AS start_rn, MAX(rn) AS end_rn
FROM sessions
GROUP BY session_id
ORDER BY session_id;";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var sessionId = (int)ToLong(reader.GetValue(0));
                    var startRow = ToLong(reader.GetValue(1));
                    var endRow = ToLong(reader.GetValue(2));
                    _sessionRanges[sessionId] = (startRow, endRow);
                }

                _sessionRangesLoaded = true;
                _logger.LogDebug("Loaded {SessionCount} session ranges from telemetry.", _sessionRanges.Count);
            }
        }

        private static string BuildSampleQuery()
        {
            return @"
WITH speed AS (
    SELECT row_number() OVER () AS rn, value AS speed
    FROM ""GPS Speed""
),
clock AS (
    SELECT row_number() OVER () AS rn, value AS gps_time
    FROM ""GPS Time""
),
throttle AS (
    SELECT row_number() OVER () AS rn, value AS throttle
    FROM ""Throttle Pos""
),
brake AS (
    SELECT row_number() OVER () AS rn, value AS brake
    FROM ""Brake Pos""
),
steer AS (
    SELECT row_number() OVER () AS rn, value AS steering
    FROM ""Steering Pos""
),
fuel AS (
    SELECT row_number() OVER () AS rn, value AS fuel
    FROM ""Fuel Level""
),
temps AS (
    SELECT row_number() OVER () AS rn, value1, value2, value3, value4
    FROM ""TyresTempCentre""
)
SELECT speed.rn,
       clock.gps_time,
       speed.speed,
       throttle.throttle,
       brake.brake,
       steer.steering,
       fuel.fuel,
       temps.value1,
       temps.value2,
       temps.value3,
       temps.value4
FROM speed
LEFT JOIN clock ON clock.rn = speed.rn
LEFT JOIN throttle ON throttle.rn = speed.rn
LEFT JOIN brake ON brake.rn = speed.rn
LEFT JOIN steer ON steer.rn = speed.rn
LEFT JOIN fuel ON fuel.rn = speed.rn
LEFT JOIN temps ON temps.rn = speed.rn
WHERE speed.rn BETWEEN ? AND ?
ORDER BY speed.rn;";
        }

        private static double GetDouble(DuckDBDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
                return 0.0;

            var value = reader.GetValue(ordinal);
            return value switch
            {
                double d => d,
                float f => f,
                decimal m => (double)m,
                int i => i,
                long l => l,
                _ => Convert.ToDouble(value)
            };
        }

        private static long ToLong(object? value)
        {
            if (value is null)
                return 0;

            return value switch
            {
                long l => l,
                int i => i,
                short s => s,
                byte b => b,
                BigInteger big => (long)big,
                _ => Convert.ToInt64(value)
            };
        }
    }
}
