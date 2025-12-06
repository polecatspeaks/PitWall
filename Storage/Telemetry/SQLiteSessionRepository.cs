using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using PitWall.Models.Telemetry;
using PitWall.Telemetry;

namespace PitWall.Storage.Telemetry
{
    /// <summary>
    /// SQLite implementation of ISessionRepository
    /// 
    /// Schema:
    /// - Sessions: SessionId (PK), SourceFilePath, ImportedAt, SessionDate, DriverName, CarName, TrackName, SessionType
    /// - Cascade delete to Laps and TelemetrySamples tables
    /// </summary>
    public class SQLiteSessionRepository : ISessionRepository
    {
        private readonly string _dbPath;
        private readonly ILapRepository _lapRepository;
        private readonly ITelemetrySampleRepository _sampleRepository;

        public SQLiteSessionRepository(string dbPath)
        {
            _dbPath = dbPath;
            _lapRepository = new SQLiteLapRepository(dbPath);
            _sampleRepository = new SQLiteTelemetrySampleRepository(dbPath);
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Sessions (
                            SessionId TEXT PRIMARY KEY,
                            SourceFilePath TEXT NOT NULL,
                            ImportedAt TEXT NOT NULL,
                            SessionDate TEXT NOT NULL,
                            DriverName TEXT NOT NULL,
                            CarName TEXT NOT NULL,
                            TrackName TEXT NOT NULL,
                            SessionType TEXT NOT NULL
                        );
                    ";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public async Task<string> SaveSessionAsync(ImportedSession session)
        {
            var sessionId = session.SessionMetadata.SessionId;

            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                await conn.OpenAsync();
                
                // Save session metadata
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO Sessions 
                        (SessionId, SourceFilePath, ImportedAt, SessionDate, DriverName, CarName, TrackName, SessionType)
                        VALUES (@sessionId, @sourcePath, @importedAt, @sessionDate, @driverName, @carName, @trackName, @sessionType)
                    ";
                    cmd.Parameters.AddWithValue("@sessionId", sessionId);
                    cmd.Parameters.AddWithValue("@sourcePath", session.SourceFilePath);
                    cmd.Parameters.AddWithValue("@importedAt", session.ImportedAt.ToString("o"));
                    cmd.Parameters.AddWithValue("@sessionDate", session.SessionMetadata.SessionDate.ToString("o"));
                    cmd.Parameters.AddWithValue("@driverName", session.SessionMetadata.DriverName);
                    cmd.Parameters.AddWithValue("@carName", session.SessionMetadata.CarName);
                    cmd.Parameters.AddWithValue("@trackName", session.SessionMetadata.TrackName);
                    cmd.Parameters.AddWithValue("@sessionType", session.SessionMetadata.SessionType);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Save hierarchical data
            if (session.Laps != null && session.Laps.Count > 0)
            {
                await _lapRepository.SaveLapsAsync(sessionId, session.Laps);
            }

            if (session.RawSamples != null && session.RawSamples.Count > 0)
            {
                await _sampleRepository.SaveSamplesAsync(sessionId, session.RawSamples);
            }

            return sessionId;
        }

        public async Task<ImportedSession?> GetSessionAsync(string sessionId)
        {
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT SessionId, SourceFilePath, ImportedAt, SessionDate, DriverName, CarName, TrackName, SessionType
                        FROM Sessions
                        WHERE SessionId = @sessionId
                    ";
                    cmd.Parameters.AddWithValue("@sessionId", sessionId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            return null;
                        }

                        var session = new ImportedSession
                        {
                            SourceFilePath = reader.GetString(1),
                            ImportedAt = DateTime.Parse(reader.GetString(2)),
                            SessionMetadata = new SessionMetadata
                            {
                                SessionId = reader.GetString(0),
                                SessionDate = DateTime.Parse(reader.GetString(3)),
                                DriverName = reader.GetString(4),
                                CarName = reader.GetString(5),
                                TrackName = reader.GetString(6),
                                SessionType = reader.GetString(7)
                            }
                        };

                        // Load hierarchical data
                        session.Laps = await _lapRepository.GetSessionLapsAsync(sessionId);
                        session.RawSamples = await _sampleRepository.GetSamplesAsync(sessionId, null);

                        return session;
                    }
                }
            }
        }

        public async Task<List<ImportedSession>> GetRecentSessionsAsync(int count)
        {
            var sessions = new List<ImportedSession>();

            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT SessionId, SourceFilePath, ImportedAt, SessionDate, DriverName, CarName, TrackName, SessionType
                        FROM Sessions
                        ORDER BY SessionDate DESC
                        LIMIT @count
                    ";
                    cmd.Parameters.AddWithValue("@count", count);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            sessions.Add(new ImportedSession
                            {
                                SourceFilePath = reader.GetString(1),
                                ImportedAt = DateTime.Parse(reader.GetString(2)),
                                SessionMetadata = new SessionMetadata
                                {
                                    SessionId = reader.GetString(0),
                                    SessionDate = DateTime.Parse(reader.GetString(3)),
                                    DriverName = reader.GetString(4),
                                    CarName = reader.GetString(5),
                                    TrackName = reader.GetString(6),
                                    SessionType = reader.GetString(7)
                                },
                                // Don't load samples for list view (performance)
                                Laps = await _lapRepository.GetSessionLapsAsync(reader.GetString(0))
                            });
                        }
                    }
                }
            }

            return sessions;
        }

        public async Task<bool> DeleteSessionAsync(string sessionId)
        {
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM Sessions WHERE SessionId = @sessionId";
                    cmd.Parameters.AddWithValue("@sessionId", sessionId);
                    var rows = await cmd.ExecuteNonQueryAsync();
                    return rows > 0;
                }
            }
        }
    }
}
