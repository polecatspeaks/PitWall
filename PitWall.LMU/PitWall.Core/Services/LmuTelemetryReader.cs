using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Core.Models;

namespace PitWall.Core.Services
{
    public class LmuTelemetryReader : ILmuTelemetryReader
    {
        private const int DefaultReadLimit = 1000;
        private readonly string _databasePath;
        private readonly int _fallbackSessionCount;
        private readonly ILogger<LmuTelemetryReader> _logger;

        private static readonly string[] RequiredTables =
        {
            "GPS Speed",
            "GPS Time",
            "Throttle Pos",
            "Brake Pos",
            "Steering Pos",
            "Fuel Level",
            "TyresTempCentre",
            "Lap"
        };


        public LmuTelemetryReader(string databasePath, int fallbackSessionCount = 0, ILogger<LmuTelemetryReader>? logger = null)
        {
            _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
            _fallbackSessionCount = fallbackSessionCount;
            _logger = logger ?? NullLogger<LmuTelemetryReader>.Instance;
        }

        public Task<int> GetSessionCountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new DuckDBConnection($"Data Source={_databasePath}");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sessions;";

                var result = command.ExecuteScalar();
                var count = result is null ? 0 : Convert.ToInt32(result);
                _logger.LogDebug("Resolved session count from DuckDB: {SessionCount}.", count);
                return Task.FromResult(count);
            }
            catch (Exception ex)
            {
                if (_fallbackSessionCount > 0)
                {
                    _logger.LogWarning(ex, "Failed to read session count from DuckDB. Using fallback {SessionCount}.", _fallbackSessionCount);
                    return Task.FromResult(_fallbackSessionCount);
                }

                _logger.LogWarning(ex, "Failed to read session count from DuckDB. Returning 0.");
                return Task.FromResult(0);
            }
        }

        public Task<List<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Loading channel list from {DatabasePath}.", _databasePath);
            using var connection = new DuckDBConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = 'main'
ORDER BY table_name, ordinal_position;";

            var channels = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tableName = reader.GetString(0);
                var columnName = reader.GetString(1);

                if (!channels.TryGetValue(tableName, out var columns))
                {
                    columns = new List<string>();
                    channels[tableName] = columns;
                }

                columns.Add(columnName);
            }

            var result = channels
                .Select(kvp => new ChannelInfo(kvp.Key, kvp.Value))
                .OrderBy(channel => channel.Name)
                .ToList();

            _logger.LogDebug("Loaded {ChannelCount} channel groups.", result.Count);

            return Task.FromResult(result);
        }

        public async IAsyncEnumerable<TelemetrySample> ReadSamplesAsync(
            int sessionId,
            int startRow,
            int endRow,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (startRow < 0)
                throw new ArgumentOutOfRangeException(nameof(startRow), "startRow must be >= 0.");

            if (endRow >= 0 && endRow < startRow)
                throw new ArgumentOutOfRangeException(nameof(endRow), "endRow must be >= startRow.");

            var rowStart = startRow + 1;
            var rowEnd = endRow >= 0 ? endRow + 1 : int.MaxValue;

            _logger.LogDebug("Reading samples from session {SessionId}, rows {RowStart} to {RowEnd}.", sessionId, rowStart, rowEnd);

            using var connection = new DuckDBConnection($"Data Source={_databasePath}");
            connection.Open();

            var existingTables = await GetTableNamesAsync(connection, cancellationToken);
            var missing = RequiredTables.Where(table => !existingTables.Contains(table)).ToList();
            if (missing.Count > 0)
            {
                _logger.LogError("Missing required telemetry tables: {MissingTables}", string.Join(", ", missing));
                throw new InvalidOperationException($"Missing required telemetry tables: {string.Join(", ", missing)}");
            }

            var hasGpsLat = existingTables.Contains("GPS Latitude");
            var hasGpsLon = existingTables.Contains("GPS Longitude");
            var hasLatG = existingTables.Contains("G Force Lat");

            using var command = connection.CreateCommand();
            command.CommandText = BuildSampleQuery(hasGpsLat, hasGpsLon, hasLatG);

            // Add session_id parameter for each CTE (8 mandatory + up to 3 optional)
            var paramCount = 8; // speed, clock, throttle, brake, steer, fuel, temps, lap
            if (hasGpsLat) paramCount++;
            if (hasGpsLon) paramCount++;
            if (hasLatG) paramCount++;

            for (int i = 0; i < paramCount; i++)
            {
                var sessionParam = command.CreateParameter();
                sessionParam.Value = sessionId;
                command.Parameters.Add(sessionParam);
            }

            var startParam = command.CreateParameter();
            startParam.Value = rowStart;
            command.Parameters.Add(startParam);

            var endParam = command.CreateParameter();
            endParam.Value = rowEnd;
            command.Parameters.Add(endParam);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var gpsTime = GetDouble(reader, 1);
                var speedMps = GetDouble(reader, 2);
                var throttle = GetDouble(reader, 3) / 100.0; // Scale from 0-100 to 0-1
                var brake = GetDouble(reader, 4) / 100.0;    // Scale from 0-100 to 0-1
                var steering = GetDouble(reader, 5);
                var fuel = GetDouble(reader, 6);

                var tyreTemps = new double[4];
                tyreTemps[0] = GetDouble(reader, 7);
                tyreTemps[1] = GetDouble(reader, 8);
                tyreTemps[2] = GetDouble(reader, 9);
                tyreTemps[3] = GetDouble(reader, 10);

                var lapRaw = GetDouble(reader, 11);
                var lapNumber = lapRaw <= 0 ? 0 : (int)Math.Round(lapRaw);

                var latitude = GetDouble(reader, 12);
                var longitude = GetDouble(reader, 13);
                var lateralG = GetDouble(reader, 14);

                var timestamp = gpsTime <= 0
                    ? DateTime.UtcNow
                    : DateTime.UnixEpoch.AddSeconds(gpsTime);

                yield return new TelemetrySample(
                    timestamp,
                    speedMps * 3.6,
                    tyreTemps,
                    fuel,
                    brake,
                    throttle,
                    steering)
                {
                    LapNumber = lapNumber,
                    Latitude = latitude,
                    Longitude = longitude,
                    LateralG = lateralG
                };
            }

            _logger.LogDebug("Finished streaming telemetry samples.");
        }

        private static string BuildSampleQuery(bool hasGpsLat, bool hasGpsLon, bool hasLatG)
        {
            var gpsLatCte = hasGpsLat
                ? "SELECT row_number() OVER (ORDER BY rowid) AS rn, value AS lat FROM \"GPS Latitude\" WHERE session_id = ?"
                : "SELECT rn, NULL AS lat FROM throttle";

            var gpsLonCte = hasGpsLon
                ? "SELECT row_number() OVER (ORDER BY rowid) AS rn, value AS lon FROM \"GPS Longitude\" WHERE session_id = ?"
                : "SELECT rn, NULL AS lon FROM throttle";

            var latGCte = hasLatG
                ? "SELECT row_number() OVER (ORDER BY rowid) AS rn, value AS lat_g FROM \"G Force Lat\" WHERE session_id = ?"
                : "SELECT rn, NULL AS lat_g FROM throttle";

            var gpsLatCountCte = hasGpsLat
                ? "SELECT COUNT(*) AS cnt FROM gps_lat"
                : "SELECT COUNT(*) AS cnt FROM throttle";

            var gpsLonCountCte = hasGpsLon
                ? "SELECT COUNT(*) AS cnt FROM gps_lon"
                : "SELECT COUNT(*) AS cnt FROM throttle";

            var latGCountCte = hasLatG
                ? "SELECT COUNT(*) AS cnt FROM lat_g"
                : "SELECT COUNT(*) AS cnt FROM throttle";

            return @"
WITH speed AS (
    SELECT row_number() OVER (ORDER BY rowid) AS rn, value AS speed
    FROM ""GPS Speed""
    WHERE session_id = ?
),
clock AS (
    SELECT row_number() OVER (ORDER BY rowid) AS rn, value AS gps_time
    FROM ""GPS Time""
    WHERE session_id = ?
),
throttle AS (
    SELECT row_number() OVER (ORDER BY rowid) AS rn, value AS throttle
    FROM ""Throttle Pos""
    WHERE session_id = ?
),
brake AS (
    SELECT row_number() OVER (ORDER BY rowid) AS rn, value AS brake
    FROM ""Brake Pos""
    WHERE session_id = ?
),
steer AS (
    SELECT row_number() OVER (ORDER BY rowid) AS rn, value AS steering
    FROM ""Steering Pos""
    WHERE session_id = ?
),
fuel AS (
    SELECT row_number() OVER (ORDER BY rowid) AS rn, value AS fuel
    FROM ""Fuel Level""
    WHERE session_id = ?
),
temps AS (
    SELECT row_number() OVER (ORDER BY rowid) AS rn, value1, value2, value3, value4
    FROM ""TyresTempCentre""
    WHERE session_id = ?
),
lap AS (
    SELECT ts, value AS lap
    FROM main.""Lap""
    WHERE session_id = ?
),
gps_lat AS (
    " + gpsLatCte + @"
),
gps_lon AS (
    " + gpsLonCte + @"
),
lat_g AS (
    " + latGCte + @"
),
speed_count AS (SELECT COUNT(*) AS cnt FROM speed),
clock_count AS (SELECT COUNT(*) AS cnt FROM clock),
throttle_count AS (SELECT COUNT(*) AS cnt FROM throttle),
brake_count AS (SELECT COUNT(*) AS cnt FROM brake),
steer_count AS (SELECT COUNT(*) AS cnt FROM steer),
fuel_count AS (SELECT COUNT(*) AS cnt FROM fuel),
temps_count AS (SELECT COUNT(*) AS cnt FROM temps),
lap_count AS (SELECT COUNT(*) AS cnt FROM lap),
gps_lat_count AS (
    " + gpsLatCountCte + @"
),
gps_lon_count AS (
    " + gpsLonCountCte + @"
),
lat_g_count AS (
    " + latGCountCte + @"
),
speed_map AS (
    SELECT t.rn,
           s.speed
    FROM throttle t
    CROSS JOIN speed_count sc
    CROSS JOIN throttle_count tc
    LEFT JOIN speed s
        ON s.rn = CAST(FLOOR((t.rn - 1) * sc.cnt::DOUBLE / tc.cnt) + 1 AS BIGINT)
),
clock_map AS (
    SELECT t.rn,
           c.gps_time
    FROM throttle t
    CROSS JOIN clock_count cc
    CROSS JOIN throttle_count tc
    LEFT JOIN clock c
        ON c.rn = CAST(FLOOR((t.rn - 1) * cc.cnt::DOUBLE / tc.cnt) + 1 AS BIGINT)
),
brake_map AS (
    SELECT t.rn,
           b.brake
    FROM throttle t
    CROSS JOIN brake_count bc
    CROSS JOIN throttle_count tc
    LEFT JOIN brake b
        ON b.rn = CAST(FLOOR((t.rn - 1) * bc.cnt::DOUBLE / tc.cnt) + 1 AS BIGINT)
),
steer_map AS (
    SELECT t.rn,
           s.steering
    FROM throttle t
    CROSS JOIN steer_count sc
    CROSS JOIN throttle_count tc
    LEFT JOIN steer s
        ON s.rn = CAST(FLOOR((t.rn - 1) * sc.cnt::DOUBLE / tc.cnt) + 1 AS BIGINT)
),
fuel_map AS (
    SELECT t.rn,
           f.fuel
    FROM throttle t
    CROSS JOIN fuel_count fc
    CROSS JOIN throttle_count tc
    LEFT JOIN fuel f
        ON f.rn = CAST(FLOOR((t.rn - 1) * fc.cnt::DOUBLE / tc.cnt) + 1 AS BIGINT)
),
temps_map AS (
    SELECT t.rn,
           tm.value1,
           tm.value2,
           tm.value3,
           tm.value4
    FROM throttle t
    CROSS JOIN temps_count tc
    CROSS JOIN throttle_count ttc
    LEFT JOIN temps tm
        ON tm.rn = CAST(FLOOR((t.rn - 1) * tc.cnt::DOUBLE / ttc.cnt) + 1 AS BIGINT)
),
lap_map AS (
    SELECT t.rn,
           COALESCE(MAX(l.lap), 0) AS lap
    FROM throttle t
    LEFT JOIN clock_map c ON c.rn = t.rn
    LEFT JOIN lap l ON l.ts <= c.gps_time
    GROUP BY t.rn
),
gps_lat_map AS (
    SELECT t.rn,
           gl.lat
    FROM throttle t
    CROSS JOIN gps_lat_count glc
    CROSS JOIN throttle_count tc
    LEFT JOIN gps_lat gl
        ON gl.rn = CAST(FLOOR((t.rn - 1) * glc.cnt::DOUBLE / tc.cnt) + 1 AS BIGINT)
),
gps_lon_map AS (
    SELECT t.rn,
           gl.lon
    FROM throttle t
    CROSS JOIN gps_lon_count glc
    CROSS JOIN throttle_count tc
    LEFT JOIN gps_lon gl
        ON gl.rn = CAST(FLOOR((t.rn - 1) * glc.cnt::DOUBLE / tc.cnt) + 1 AS BIGINT)
),
lat_g_map AS (
    SELECT t.rn,
           lg.lat_g
    FROM throttle t
    CROSS JOIN lat_g_count lgc
    CROSS JOIN throttle_count tc
    LEFT JOIN lat_g lg
        ON lg.rn = CAST(FLOOR((t.rn - 1) * lgc.cnt::DOUBLE / tc.cnt) + 1 AS BIGINT)
)
SELECT throttle.rn,
       COALESCE(clock_map.gps_time, 0) AS gps_time,
       speed_map.speed,
       COALESCE(throttle.throttle, 0) AS throttle,
       COALESCE(brake_map.brake, 0) AS brake,
       COALESCE(steer_map.steering, 0) AS steering,
       COALESCE(fuel_map.fuel, 0) AS fuel,
       COALESCE(temps_map.value1, 0) AS value1,
       COALESCE(temps_map.value2, 0) AS value2,
       COALESCE(temps_map.value3, 0) AS value3,
       COALESCE(temps_map.value4, 0) AS value4,
       COALESCE(lap_map.lap, 0) AS lap,
       COALESCE(gps_lat_map.lat, 0) AS lat,
       COALESCE(gps_lon_map.lon, 0) AS lon,
       COALESCE(lat_g_map.lat_g, 0) AS lat_g
FROM throttle
LEFT JOIN speed_map ON speed_map.rn = throttle.rn
LEFT JOIN clock_map ON clock_map.rn = throttle.rn
LEFT JOIN brake_map ON brake_map.rn = throttle.rn
LEFT JOIN steer_map ON steer_map.rn = throttle.rn
LEFT JOIN fuel_map ON fuel_map.rn = throttle.rn
LEFT JOIN temps_map ON temps_map.rn = throttle.rn
LEFT JOIN lap_map ON lap_map.rn = throttle.rn
LEFT JOIN gps_lat_map ON gps_lat_map.rn = throttle.rn
LEFT JOIN gps_lon_map ON gps_lon_map.rn = throttle.rn
LEFT JOIN lat_g_map ON lat_g_map.rn = throttle.rn
WHERE throttle.rn BETWEEN ? AND ?
ORDER BY throttle.rn;";
        }

        private static Task<HashSet<string>> GetTableNamesAsync(
            DuckDBConnection connection,
            CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'main';";

            using var reader = command.ExecuteReader();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                names.Add(reader.GetString(0));
            }

            return Task.FromResult(names);
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
    }
}
