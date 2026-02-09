using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using DuckDB.NET.Data;
using PitWall.Core.Models;

namespace PitWall.Core.Services
{
    public class LmuTelemetryReader : ILmuTelemetryReader
    {
        private const int DefaultReadLimit = 1000;
        private readonly string _databasePath;
        private readonly int _fallbackSessionCount;

        private static readonly string[] RequiredTables =
        {
            "GPS Speed",
            "GPS Time",
            "Throttle Pos",
            "Brake Pos",
            "Steering Pos",
            "Fuel Level",
            "TyresTempCentre"
        };

        public LmuTelemetryReader(string databasePath, int fallbackSessionCount = 0)
        {
            _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
            _fallbackSessionCount = fallbackSessionCount;
        }

        public Task<int> GetSessionCountAsync(CancellationToken cancellationToken = default)
        {
            if (_fallbackSessionCount > 0)
                return Task.FromResult(_fallbackSessionCount);

            return Task.FromResult(0);
        }

        public Task<List<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken = default)
        {
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

            return Task.FromResult(result);
        }

        public async IAsyncEnumerable<TelemetrySample> ReadSamplesAsync(
            int sessionId,
            int startRow,
            int endRow,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _ = sessionId;

            if (startRow < 0)
                throw new ArgumentOutOfRangeException(nameof(startRow), "startRow must be >= 0.");

            if (endRow >= 0 && endRow < startRow)
                throw new ArgumentOutOfRangeException(nameof(endRow), "endRow must be >= startRow.");

            var rowStart = startRow + 1;
            var rowEnd = endRow >= 0 ? endRow + 1 : rowStart + DefaultReadLimit - 1;

            using var connection = new DuckDBConnection($"Data Source={_databasePath}");
            connection.Open();

            var existingTables = await GetTableNamesAsync(connection, cancellationToken);
            var missing = RequiredTables.Where(table => !existingTables.Contains(table)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException($"Missing required telemetry tables: {string.Join(", ", missing)}");

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
                cancellationToken.ThrowIfCancellationRequested();
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

                yield return new TelemetrySample(
                    timestamp,
                    speedMps * 3.6,
                    tyreTemps,
                    fuel,
                    brake,
                    throttle,
                    steering);
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
