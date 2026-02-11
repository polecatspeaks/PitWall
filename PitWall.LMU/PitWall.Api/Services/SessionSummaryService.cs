using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Api.Models;
using PitWall.Core.Storage;

namespace PitWall.Api.Services
{
    public class SessionSummaryService : ISessionSummaryService
    {
        private readonly string _databasePath;
        private readonly ISessionMetadataStore _metadataStore;
        private readonly ILogger<SessionSummaryService> _logger;

        public SessionSummaryService(
            IDuckDbConnector connector,
            ISessionMetadataStore metadataStore,
            ILogger<SessionSummaryService>? logger = null)
        {
            _databasePath = connector?.DatabasePath ?? throw new ArgumentNullException(nameof(connector));
            _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
            _logger = logger ?? NullLogger<SessionSummaryService>.Instance;
        }

        public async Task<IReadOnlyList<SessionSummary>> GetSessionSummariesAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(_databasePath))
            {
                _logger.LogWarning("Telemetry database not found at {DatabasePath}.", _databasePath);
                return Array.Empty<SessionSummary>();
            }

            var metadata = await _metadataStore.GetAllAsync(cancellationToken);
            var summaries = new List<SessionSummary>();

            try
            {
                using var connection = new DuckDBConnection($"Data Source={_databasePath}");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = BuildSessionSummaryQuery();

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var sessionId = (int)ToLong(reader.GetValue(0));
                    var recordingTimeRaw = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var sessionTimeRaw = reader.IsDBNull(2) ? null : reader.GetString(2);

                    var startTime = ParseRecordingTime(recordingTimeRaw);
                    var endTime = CalculateEndTime(startTime, sessionTimeRaw);

                    metadata.TryGetValue(sessionId, out var meta);

                    summaries.Add(new SessionSummary
                    {
                        SessionId = sessionId,
                        StartTimeUtc = startTime,
                        EndTimeUtc = endTime,
                        Track = meta?.Track ?? "Unknown",
                        TrackId = meta?.TrackId,
                        Car = meta?.Car ?? "Unknown"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load session summaries from DuckDB.");
                return Array.Empty<SessionSummary>();
            }

            return summaries;
        }

        public async Task<SessionSummary?> GetSessionSummaryAsync(int sessionId, CancellationToken cancellationToken = default)
        {
            var summaries = await GetSessionSummariesAsync(cancellationToken);
            return summaries.FirstOrDefault(summary => summary.SessionId == sessionId);
        }

        private static string BuildSessionSummaryQuery()
        {
            return @"
SELECT sessions.session_id,
       sessions.recording_time,
       session_time.value AS session_time
FROM sessions
LEFT JOIN session_metadata AS session_time
  ON session_time.session_id = sessions.session_id
 AND session_time.""key"" = 'SessionTime'
ORDER BY sessions.session_id;";
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

        private static DateTimeOffset? ParseRecordingTime(string? value)
        {
                if (string.IsNullOrWhiteSpace(value))
                return null;

                var normalized = value.Replace('_', ':');

            if (DateTimeOffset.TryParse(
                    normalized,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var timestamp))
            {
                return timestamp;
            }

            return null;
        }

        private static DateTimeOffset? CalculateEndTime(DateTimeOffset? startTime, string? sessionTime)
        {
            if (startTime == null)
                return null;

            if (!string.IsNullOrWhiteSpace(sessionTime)
                && TimeSpan.TryParse(sessionTime, CultureInfo.InvariantCulture, out var duration))
            {
                return startTime.Value.Add(duration);
            }

            return startTime;
        }
    }
}
