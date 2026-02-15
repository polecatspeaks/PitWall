using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Core.Models;
using PitWall.Core.Utilities;

namespace PitWall.Core.Services
{
    public class LmuTelemetryReader : ILmuTelemetryReader
    {
        private const int DefaultReadLimit = 1000;
        private readonly string _databasePath;
        private readonly int _fallbackSessionCount;
        private readonly ILogger<LmuTelemetryReader> _logger;

        /// <summary>
        /// Cached interpolated session data to avoid recomputing on each batch request.
        /// </summary>
        private InterpolatedSessionCache? _sessionCache;
        private readonly object _cacheLock = new();

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

        /// <summary>
        /// Pre-computed interpolated data for a session, cached to avoid recomputing per-batch.
        /// </summary>
        private sealed class InterpolatedSessionCache
        {
            public int SessionId { get; init; }
            public double[] TimeGrid { get; init; } = Array.Empty<double>();
            public double[] Speed { get; init; } = Array.Empty<double>();
            public double[] Throttle { get; init; } = Array.Empty<double>();
            public double[] Brake { get; init; } = Array.Empty<double>();
            public double[] Steering { get; init; } = Array.Empty<double>();
            public double[] Fuel { get; init; } = Array.Empty<double>();
            public double[][] TyreTemps { get; init; } = Array.Empty<double[]>();
            public double[] Latitude { get; init; } = Array.Empty<double>();
            public double[] Longitude { get; init; } = Array.Empty<double>();
            public double[] LateralG { get; init; } = Array.Empty<double>();
            public List<(double Timestamp, int LapNumber)> LapBoundaries { get; init; } = new();
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

            _logger.LogDebug("Reading samples from session {SessionId}, rows {StartRow} to {EndRow}.", sessionId, startRow, endRow);

            // Compute interpolation on a background thread to avoid blocking the caller
            var cache = await Task.Run(() => GetOrComputeSessionCache(sessionId, cancellationToken), cancellationToken);

            if (cache.TimeGrid.Length == 0)
            {
                _logger.LogWarning("No interpolated data for session {SessionId}.", sessionId);
                yield break;
            }

            // Apply row range filtering and yield aligned samples
            int actualStart = Math.Max(0, startRow);
            int actualEnd = endRow >= 0 ? Math.Min(endRow, cache.TimeGrid.Length - 1) : cache.TimeGrid.Length - 1;

            for (int i = actualStart; i <= actualEnd; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var gpsTime = cache.TimeGrid[i];
                var lapNumber = AssignLapNumber(gpsTime, cache.LapBoundaries);

                var timestamp = gpsTime <= 0
                    ? DateTime.UtcNow
                    : DateTime.UnixEpoch.AddSeconds(gpsTime);

                yield return new TelemetrySample(
                    timestamp,
                    cache.Speed[i],
                    new[] { cache.TyreTemps[0][i], cache.TyreTemps[1][i], cache.TyreTemps[2][i], cache.TyreTemps[3][i] },
                    cache.Fuel[i],
                    cache.Brake[i],
                    cache.Throttle[i],
                    cache.Steering[i])
                {
                    LapNumber = lapNumber,
                    Latitude = cache.Latitude[i],
                    Longitude = cache.Longitude[i],
                    LateralG = cache.LateralG[i]
                };
            }

            _logger.LogDebug("Finished streaming {Count} interpolated telemetry samples.", actualEnd - actualStart + 1);
        }

        /// <summary>
        /// Gets cached interpolated data for a session, computing it if necessary.
        /// Thread-safe: computation is serialized under the lock to prevent duplicate work.
        /// Holding the lock during I/O-bound compute is acceptable here because callers
        /// are on background threads and serializing avoids redundant DuckDB reads.
        /// </summary>
        private InterpolatedSessionCache GetOrComputeSessionCache(int sessionId, CancellationToken cancellationToken)
        {
            lock (_cacheLock)
            {
                if (_sessionCache != null && _sessionCache.SessionId == sessionId)
                {
                    _logger.LogDebug("Using cached interpolated data for session {SessionId} ({GridSize} points).",
                        sessionId, _sessionCache.TimeGrid.Length);
                    return _sessionCache;
                }

                _logger.LogInformation("Computing interpolated data for session {SessionId}...", sessionId);

                // Clear old cache before computing new one to release memory sooner
                _sessionCache = null;

                var computed = ComputeInterpolatedSession(sessionId, cancellationToken);
                _sessionCache = computed;

                _logger.LogInformation("Cached interpolated data for session {SessionId}: {GridSize} points.",
                    sessionId, computed.TimeGrid.Length);
                return computed;
            }
        }

        /// <summary>
        /// Loads all channel data from DuckDB and interpolates onto a uniform 50Hz time grid.
        /// </summary>
        private InterpolatedSessionCache ComputeInterpolatedSession(int sessionId, CancellationToken cancellationToken)
        {
            using var connection = new DuckDBConnection($"Data Source={_databasePath}");
            connection.Open();

            var existingTables = GetTableNamesAsync(connection, cancellationToken).GetAwaiter().GetResult();
            var missing = RequiredTables.Where(table => !existingTables.Contains(table)).ToList();
            if (missing.Count > 0)
            {
                _logger.LogError("Missing required telemetry tables: {MissingTables}", string.Join(", ", missing));
                throw new InvalidOperationException($"Missing required telemetry tables: {string.Join(", ", missing)}");
            }

            // Step 1: Load GPS Time as master timeline
            var gpsTimeValues = LoadChannelValues(connection, "GPS Time", sessionId, cancellationToken);
            if (gpsTimeValues.Length == 0)
            {
                _logger.LogWarning("No GPS Time data found for session {SessionId}.", sessionId);
                return new InterpolatedSessionCache { SessionId = sessionId };
            }

            double masterStart = gpsTimeValues[0];
            double masterEnd = gpsTimeValues[^1];
            _logger.LogDebug("Master timeline: {Start:F3}s to {End:F3}s, {Count} samples.",
                masterStart, masterEnd, gpsTimeValues.Length);

            // Step 2: Create uniform 50Hz time grid
            const int targetFrequencyHz = 50;
            var timeGrid = ChannelInterpolator.CreateTimeGrid(masterStart, masterEnd, targetFrequencyHz);
            if (timeGrid.Length == 0)
            {
                _logger.LogWarning("Empty time grid for session {SessionId}.", sessionId);
                return new InterpolatedSessionCache { SessionId = sessionId };
            }

            _logger.LogDebug("Created {GridSize} point time grid at {Freq}Hz.", timeGrid.Length, targetFrequencyHz);

            // Step 3: Load and interpolate each channel onto the time grid
            // Speed: stored in m/s, convert to km/h (* 3.6)
            var speedValues = LoadChannelValues(connection, "GPS Speed", sessionId, cancellationToken);
            var speedTimestamps = ChannelInterpolator.EstimateChannelTimestamps(speedValues.Length, masterStart, masterEnd);
            var speedInterp = ChannelInterpolator.Interpolate(speedTimestamps, speedValues, timeGrid, scaleFactor: 3.6);

            // Throttle: stored 0-100, convert to 0-1 (/ 100)
            var throttleValues = LoadChannelValues(connection, "Throttle Pos", sessionId, cancellationToken);
            var throttleTimestamps = ChannelInterpolator.EstimateChannelTimestamps(throttleValues.Length, masterStart, masterEnd);
            var throttleInterp = ChannelInterpolator.Interpolate(throttleTimestamps, throttleValues, timeGrid, scaleFactor: 0.01);

            // Brake: stored 0-100, convert to 0-1 (/ 100)
            var brakeValues = LoadChannelValues(connection, "Brake Pos", sessionId, cancellationToken);
            var brakeTimestamps = ChannelInterpolator.EstimateChannelTimestamps(brakeValues.Length, masterStart, masterEnd);
            var brakeInterp = ChannelInterpolator.Interpolate(brakeTimestamps, brakeValues, timeGrid, scaleFactor: 0.01);

            // Steering
            var steerValues = LoadChannelValues(connection, "Steering Pos", sessionId, cancellationToken);
            var steerTimestamps = ChannelInterpolator.EstimateChannelTimestamps(steerValues.Length, masterStart, masterEnd);
            var steerInterp = ChannelInterpolator.Interpolate(steerTimestamps, steerValues, timeGrid);

            // Fuel
            var fuelValues = LoadChannelValues(connection, "Fuel Level", sessionId, cancellationToken);
            var fuelTimestamps = ChannelInterpolator.EstimateChannelTimestamps(fuelValues.Length, masterStart, masterEnd);
            var fuelInterp = ChannelInterpolator.Interpolate(fuelTimestamps, fuelValues, timeGrid);

            // Tire temperatures (4 columns: value1-value4)
            var tempsData = LoadMultiColumnValues(connection, "TyresTempCentre", sessionId, 4, cancellationToken);
            var tempsTimestamps = ChannelInterpolator.EstimateChannelTimestamps(
                tempsData.Length > 0 ? tempsData[0].Length : 0, masterStart, masterEnd);
            var tempsInterp = tempsData.Length == 4
                ? ChannelInterpolator.InterpolateMultiColumn(tempsTimestamps, tempsData, timeGrid)
                : new[] { new double[timeGrid.Length], new double[timeGrid.Length], new double[timeGrid.Length], new double[timeGrid.Length] };

            // Optional channels
            double[] latInterp, lonInterp, latGInterp;

            if (existingTables.Contains("GPS Latitude"))
            {
                var latValues = LoadChannelValues(connection, "GPS Latitude", sessionId, cancellationToken);
                var latTimestamps = ChannelInterpolator.EstimateChannelTimestamps(latValues.Length, masterStart, masterEnd);
                latInterp = ChannelInterpolator.Interpolate(latTimestamps, latValues, timeGrid);
            }
            else
            {
                latInterp = new double[timeGrid.Length];
            }

            if (existingTables.Contains("GPS Longitude"))
            {
                var lonValues = LoadChannelValues(connection, "GPS Longitude", sessionId, cancellationToken);
                var lonTimestamps = ChannelInterpolator.EstimateChannelTimestamps(lonValues.Length, masterStart, masterEnd);
                lonInterp = ChannelInterpolator.Interpolate(lonTimestamps, lonValues, timeGrid);
            }
            else
            {
                lonInterp = new double[timeGrid.Length];
            }

            if (existingTables.Contains("G Force Lat"))
            {
                var latGValues = LoadChannelValues(connection, "G Force Lat", sessionId, cancellationToken);
                var latGTimestamps = ChannelInterpolator.EstimateChannelTimestamps(latGValues.Length, masterStart, masterEnd);
                latGInterp = ChannelInterpolator.Interpolate(latGTimestamps, latGValues, timeGrid);
            }
            else
            {
                latGInterp = new double[timeGrid.Length];
            }

            // Step 4: Load lap boundaries
            var lapBoundaries = LoadLapBoundaries(connection, sessionId, cancellationToken);

            return new InterpolatedSessionCache
            {
                SessionId = sessionId,
                TimeGrid = timeGrid,
                Speed = speedInterp,
                Throttle = throttleInterp,
                Brake = brakeInterp,
                Steering = steerInterp,
                Fuel = fuelInterp,
                TyreTemps = tempsInterp,
                Latitude = latInterp,
                Longitude = lonInterp,
                LateralG = latGInterp,
                LapBoundaries = lapBoundaries
            };
        }

        /// <summary>
        /// Loads all values from a single-value channel table, ordered by rowid.
        /// </summary>
        private static double[] LoadChannelValues(
            DuckDBConnection connection,
            string tableName,
            int sessionId,
            CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT value FROM \"{tableName}\" WHERE session_id = ? ORDER BY rowid";
            var param = command.CreateParameter();
            param.Value = sessionId;
            command.Parameters.Add(param);

            var values = new List<double>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                values.Add(GetDouble(reader, 0));
            }

            return values.ToArray();
        }

        /// <summary>
        /// Loads all values from a multi-column channel table (e.g. TyresTempCentre with value1-value4).
        /// Returns an array of columns, where each column is an array of values.
        /// </summary>
        private static double[][] LoadMultiColumnValues(
            DuckDBConnection connection,
            string tableName,
            int sessionId,
            int columnCount,
            CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            var columnNames = string.Join(", ", Enumerable.Range(1, columnCount).Select(i => $"value{i}"));
            command.CommandText = $"SELECT {columnNames} FROM \"{tableName}\" WHERE session_id = ? ORDER BY rowid";
            var param = command.CreateParameter();
            param.Value = sessionId;
            command.Parameters.Add(param);

            var columns = new List<double>[columnCount];
            for (int c = 0; c < columnCount; c++)
                columns[c] = new List<double>();

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int c = 0; c < columnCount; c++)
                {
                    columns[c].Add(GetDouble(reader, c));
                }
            }

            return columns.Select(c => c.ToArray()).ToArray();
        }

        /// <summary>
        /// Loads lap boundary timestamps from the Lap table.
        /// Returns a sorted list of (timestamp, lapNumber) tuples.
        /// </summary>
        private static List<(double Timestamp, int LapNumber)> LoadLapBoundaries(
            DuckDBConnection connection,
            int sessionId,
            CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT ts, value FROM \"Lap\" WHERE session_id = ? ORDER BY ts";
            var param = command.CreateParameter();
            param.Value = sessionId;
            command.Parameters.Add(param);

            var boundaries = new List<(double Timestamp, int LapNumber)>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var ts = GetDouble(reader, 0);
                var lapRaw = GetDouble(reader, 1);
                var lapNumber = lapRaw <= 0 ? 0 : (int)Math.Round(lapRaw);
                boundaries.Add((ts, lapNumber));
            }

            return boundaries;
        }

        /// <summary>
        /// Assigns a lap number to a given GPS time based on lap boundary timestamps.
        /// Uses the most recent lap boundary that is at or before the given time.
        /// </summary>
        private static int AssignLapNumber(double gpsTime, List<(double Timestamp, int LapNumber)> lapBoundaries)
        {
            if (lapBoundaries.Count == 0)
                return 0;

            // Binary search for the last boundary <= gpsTime
            int lo = 0;
            int hi = lapBoundaries.Count - 1;
            int bestIdx = -1;

            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (lapBoundaries[mid].Timestamp <= gpsTime)
                {
                    bestIdx = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return bestIdx >= 0 ? lapBoundaries[bestIdx].LapNumber : 0;
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
