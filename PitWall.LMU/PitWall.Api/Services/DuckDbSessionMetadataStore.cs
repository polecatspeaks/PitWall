using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Api.Models;
using PitWall.Core.Storage;

namespace PitWall.Api.Services
{
    public class DuckDbSessionMetadataStore : ISessionMetadataStore
    {
        private readonly string _databasePath;
        private readonly ILogger<DuckDbSessionMetadataStore> _logger;

        public DuckDbSessionMetadataStore(IDuckDbConnector connector, ILogger<DuckDbSessionMetadataStore>? logger = null)
        {
            if (connector == null)
                throw new ArgumentNullException(nameof(connector));

            _databasePath = connector.DatabasePath;
            _logger = logger ?? NullLogger<DuckDbSessionMetadataStore>.Instance;
        }

        public Task<IReadOnlyDictionary<int, SessionMetadata>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<int, SessionMetadata>();

            try
            {
                using var connection = new DuckDBConnection($"Data Source={_databasePath}");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT sessions.session_id,
             sessions.track_name,
             sessions.car_name,
             track_id.value AS track_id
FROM sessions
LEFT JOIN session_metadata AS track_id
    ON track_id.session_id = sessions.session_id
 AND track_id.""key"" = 'TrackId'
ORDER BY sessions.session_id;";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var sessionId = Convert.ToInt32(reader.GetValue(0));
                    var track = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var car = reader.IsDBNull(2) ? null : reader.GetString(2);
                    var trackId = reader.IsDBNull(3) ? null : reader.GetString(3);

                    result[sessionId] = new SessionMetadata
                    {
                        Track = string.IsNullOrWhiteSpace(track) ? "Unknown" : track,
                        TrackId = string.IsNullOrWhiteSpace(trackId) ? null : trackId,
                        Car = string.IsNullOrWhiteSpace(car) ? "Unknown" : car
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load session metadata from DuckDB at {DatabasePath}.", _databasePath);
            }

            return Task.FromResult((IReadOnlyDictionary<int, SessionMetadata>)result);
        }

        public Task<SessionMetadata?> GetAsync(int sessionId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new DuckDBConnection($"Data Source={_databasePath}");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT sessions.track_name,
             sessions.car_name,
             track_id.value AS track_id
FROM sessions
LEFT JOIN session_metadata AS track_id
    ON track_id.session_id = sessions.session_id
 AND track_id.""key"" = 'TrackId'
WHERE sessions.session_id = ?;";

                var idParam = command.CreateParameter();
                idParam.Value = sessionId;
                command.Parameters.Add(idParam);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    var track = reader.IsDBNull(0) ? null : reader.GetString(0);
                    var car = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var trackId = reader.IsDBNull(2) ? null : reader.GetString(2);

                    return Task.FromResult<SessionMetadata?>(new SessionMetadata
                    {
                        Track = string.IsNullOrWhiteSpace(track) ? "Unknown" : track,
                        TrackId = string.IsNullOrWhiteSpace(trackId) ? null : trackId,
                        Car = string.IsNullOrWhiteSpace(car) ? "Unknown" : car
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load session metadata for {SessionId}.", sessionId);
            }

            return Task.FromResult<SessionMetadata?>(null);
        }

        public Task SetAsync(int sessionId, SessionMetadata metadata, CancellationToken cancellationToken = default)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            try
            {
                using var connection = new DuckDBConnection($"Data Source={_databasePath}");
                connection.Open();

                using var transaction = connection.BeginTransaction();

                using (var updateCommand = connection.CreateCommand())
                {
                    updateCommand.Transaction = transaction;
                    updateCommand.CommandText = @"
UPDATE sessions
SET track_name = ?, car_name = ?
WHERE session_id = ?;";

                    var trackParam = updateCommand.CreateParameter();
                    trackParam.Value = metadata.Track;
                    updateCommand.Parameters.Add(trackParam);

                    var carParam = updateCommand.CreateParameter();
                    carParam.Value = metadata.Car;
                    updateCommand.Parameters.Add(carParam);

                    var idParam = updateCommand.CreateParameter();
                    idParam.Value = sessionId;
                    updateCommand.Parameters.Add(idParam);

                    updateCommand.ExecuteNonQuery();
                }

                using (var deleteCommand = connection.CreateCommand())
                {
                    deleteCommand.Transaction = transaction;
                    deleteCommand.CommandText = @"
DELETE FROM session_metadata
WHERE session_id = ? AND ""key"" IN ('TrackName', 'TrackId', 'CarName');";

                    var idParam = deleteCommand.CreateParameter();
                    idParam.Value = sessionId;
                    deleteCommand.Parameters.Add(idParam);

                    deleteCommand.ExecuteNonQuery();
                }

                using (var insertCommand = connection.CreateCommand())
                {
                    insertCommand.Transaction = transaction;
                    insertCommand.CommandText = @"
INSERT INTO session_metadata (session_id, ""key"", ""value"") VALUES (?, ?, ?);";

                    var idParam = insertCommand.CreateParameter();
                    var keyParam = insertCommand.CreateParameter();
                    var valueParam = insertCommand.CreateParameter();

                    insertCommand.Parameters.Add(idParam);
                    insertCommand.Parameters.Add(keyParam);
                    insertCommand.Parameters.Add(valueParam);

                    idParam.Value = sessionId;

                    keyParam.Value = "TrackName";
                    valueParam.Value = metadata.Track;
                    insertCommand.ExecuteNonQuery();

                    if (!string.IsNullOrWhiteSpace(metadata.TrackId))
                    {
                        keyParam.Value = "TrackId";
                        valueParam.Value = metadata.TrackId;
                        insertCommand.ExecuteNonQuery();
                    }

                    keyParam.Value = "CarName";
                    valueParam.Value = metadata.Car;
                    insertCommand.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist session metadata for {SessionId}.", sessionId);
            }

            return Task.CompletedTask;
        }
    }
}
