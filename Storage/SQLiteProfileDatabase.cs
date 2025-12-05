using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PitWall.Models;

namespace PitWall.Storage
{
    /// <summary>
    /// SQLite implementation of profile database for production
    /// </summary>
    public class SQLiteProfileDatabase : IProfileDatabase
    {
        private readonly string _connectionString;
        private const string DB_FILE = "pitwall.db";

        public SQLiteProfileDatabase(string dataDirectory = "")
        {
            // Default to per-user LocalAppData to avoid locks/permissions under Program Files
            var baseDir = string.IsNullOrEmpty(dataDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PitWall")
                : dataDirectory;

            Directory.CreateDirectory(baseDir);

            string dbPath = Path.Combine(baseDir, DB_FILE);
            _connectionString = $"Data Source={dbPath};Version=3;";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            string profilesTable = @"
                CREATE TABLE IF NOT EXISTS Profiles (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DriverName TEXT NOT NULL,
                    TrackName TEXT NOT NULL,
                    CarName TEXT NOT NULL,
                    AverageFuelPerLap REAL NOT NULL,
                    TypicalTyreDegradation REAL NOT NULL,
                    Style INTEGER NOT NULL,
                    SessionsCompleted INTEGER NOT NULL,
                    LastUpdated TEXT NOT NULL,
                    Confidence REAL DEFAULT 0.0,
                    IsStale INTEGER DEFAULT 0,
                    LastSessionDate TEXT,
                    UNIQUE(DriverName, TrackName, CarName)
                )";

            string sessionsTable = @"
                CREATE TABLE IF NOT EXISTS Sessions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DriverName TEXT NOT NULL,
                    TrackName TEXT NOT NULL,
                    CarName TEXT NOT NULL,
                    SessionType TEXT NOT NULL,
                    SessionDate TEXT NOT NULL,
                    TotalFuelUsed REAL NOT NULL,
                    SessionDuration TEXT NOT NULL
                )";

            string lapsTable = @"
                CREATE TABLE IF NOT EXISTS Laps (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SessionId INTEGER NOT NULL,
                    LapNumber INTEGER NOT NULL,
                    LapTime TEXT NOT NULL,
                    FuelUsed REAL NOT NULL,
                    FuelRemaining REAL NOT NULL,
                    IsValid INTEGER NOT NULL,
                    IsClear INTEGER NOT NULL,
                    TyreWearAverage REAL NOT NULL,
                    Timestamp TEXT NOT NULL,
                    FOREIGN KEY(SessionId) REFERENCES Sessions(Id)
                )";

            string timeSeriesTable = @"
                CREATE TABLE IF NOT EXISTS ProfileTimeSeries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DriverName TEXT NOT NULL,
                    TrackName TEXT NOT NULL,
                    CarName TEXT NOT NULL,
                    SessionDate TEXT NOT NULL,
                    SessionId TEXT,
                    SessionType TEXT,
                    LapCount INTEGER NOT NULL,
                    FuelPerLap REAL NOT NULL,
                    AvgLapTime REAL NOT NULL,
                    LapTimeStdDev REAL,
                    ProcessedDate TEXT NOT NULL,
                    ReplayFilePath TEXT
                )";

            string timeSeriesIndex = @"
                CREATE INDEX IF NOT EXISTS idx_timeseries_track_car 
                ON ProfileTimeSeries(DriverName, TrackName, CarName)";

            string timeSeriesDateIndex = @"
                CREATE INDEX IF NOT EXISTS idx_timeseries_date 
                ON ProfileTimeSeries(SessionDate)";

            using var command = new SQLiteCommand(profilesTable, connection);
            command.ExecuteNonQuery();

            command.CommandText = sessionsTable;
            command.ExecuteNonQuery();

            command.CommandText = lapsTable;
            command.ExecuteNonQuery();

            command.CommandText = timeSeriesTable;
            command.ExecuteNonQuery();

            command.CommandText = timeSeriesIndex;
            command.ExecuteNonQuery();

            command.CommandText = timeSeriesDateIndex;
            command.ExecuteNonQuery();
        }

        public async Task<DriverProfile?> GetProfile(string driver, string track, string car)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT DriverName, TrackName, CarName, AverageFuelPerLap, 
                       TypicalTyreDegradation, Style, SessionsCompleted, LastUpdated,
                       Confidence, IsStale, LastSessionDate
                FROM Profiles
                WHERE DriverName = @driver AND TrackName = @track AND CarName = @car";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@driver", driver);
            command.Parameters.AddWithValue("@track", track);
            command.Parameters.AddWithValue("@car", car);

            using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return new DriverProfile
            {
                DriverName = reader.GetString(0),
                TrackName = reader.GetString(1),
                CarName = reader.GetString(2),
                AverageFuelPerLap = reader.GetDouble(3),
                TypicalTyreDegradation = reader.GetDouble(4),
                Style = (DrivingStyle)reader.GetInt32(5),
                SessionsCompleted = reader.GetInt32(6),
                LastUpdated = DateTime.Parse(reader.GetString(7)),
                Confidence = reader.IsDBNull(8) ? 0.0 : reader.GetDouble(8),
                IsStale = reader.IsDBNull(9) ? false : reader.GetInt32(9) == 1,
                LastSessionDate = reader.IsDBNull(10) ? null : DateTime.Parse(reader.GetString(10))
            };
        }

        public async Task SaveProfile(DriverProfile profile)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            string upsert = @"
                INSERT INTO Profiles (DriverName, TrackName, CarName, AverageFuelPerLap, 
                                      TypicalTyreDegradation, Style, SessionsCompleted, LastUpdated,
                                      Confidence, IsStale, LastSessionDate)
                VALUES (@driver, @track, @car, @fuel, @tyre, @style, @sessions, @updated,
                        @confidence, @stale, @lastSession)
                ON CONFLICT(DriverName, TrackName, CarName) DO UPDATE SET
                    AverageFuelPerLap = @fuel,
                    TypicalTyreDegradation = @tyre,
                    Style = @style,
                    SessionsCompleted = @sessions,
                    LastUpdated = @updated,
                    Confidence = @confidence,
                    IsStale = @stale,
                    LastSessionDate = @lastSession";

            using var command = new SQLiteCommand(upsert, connection);
            command.Parameters.AddWithValue("@driver", profile.DriverName);
            command.Parameters.AddWithValue("@track", profile.TrackName);
            command.Parameters.AddWithValue("@car", profile.CarName);
            command.Parameters.AddWithValue("@fuel", profile.AverageFuelPerLap);
            command.Parameters.AddWithValue("@tyre", profile.TypicalTyreDegradation);
            command.Parameters.AddWithValue("@style", (int)profile.Style);
            command.Parameters.AddWithValue("@sessions", profile.SessionsCompleted);
            command.Parameters.AddWithValue("@updated", profile.LastUpdated.ToString("o"));
            command.Parameters.AddWithValue("@confidence", profile.Confidence);
            command.Parameters.AddWithValue("@stale", profile.IsStale ? 1 : 0);
            command.Parameters.AddWithValue("@lastSession", profile.LastSessionDate?.ToString("o") ?? string.Empty);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<SessionData>> GetRecentSessions(int count)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT Id, DriverName, TrackName, CarName, SessionType, SessionDate, 
                       TotalFuelUsed, SessionDuration
                FROM Sessions
                ORDER BY SessionDate DESC
                LIMIT @count";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@count", count);

            var sessions = new List<SessionData>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                int sessionId = reader.GetInt32(0);
                var session = new SessionData
                {
                    DriverName = reader.GetString(1),
                    TrackName = reader.GetString(2),
                    CarName = reader.GetString(3),
                    SessionType = reader.GetString(4),
                    SessionDate = DateTime.Parse(reader.GetString(5)),
                    TotalFuelUsed = reader.GetDouble(6),
                    SessionDuration = TimeSpan.Parse(reader.GetString(7)),
                    Laps = await GetLapsForSession(connection, sessionId)
                };
                sessions.Add(session);
            }

            return sessions;
        }

        public async Task SaveSession(SessionData session)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            try
            {
                string insertSession = @"
                    INSERT INTO Sessions (DriverName, TrackName, CarName, SessionType, SessionDate, 
                                         TotalFuelUsed, SessionDuration)
                    VALUES (@driver, @track, @car, @type, @date, @fuel, @duration);
                    SELECT last_insert_rowid();";

                long sessionId;
                using (var command = new SQLiteCommand(insertSession, connection))
                {
                    command.Parameters.AddWithValue("@driver", session.DriverName);
                    command.Parameters.AddWithValue("@track", session.TrackName);
                    command.Parameters.AddWithValue("@car", session.CarName);
                    command.Parameters.AddWithValue("@type", session.SessionType);
                    command.Parameters.AddWithValue("@date", session.SessionDate.ToString("o"));
                    command.Parameters.AddWithValue("@fuel", session.TotalFuelUsed);
                    command.Parameters.AddWithValue("@duration", session.SessionDuration.ToString());
                    sessionId = (long)await command.ExecuteScalarAsync();
                }

                string insertLap = @"
                    INSERT INTO Laps (SessionId, LapNumber, LapTime, FuelUsed, FuelRemaining, 
                                     IsValid, IsClear, TyreWearAverage, Timestamp)
                    VALUES (@sessionId, @lapNum, @lapTime, @fuel, @remaining, @valid, @clear, @tyre, @timestamp)";

                foreach (var lap in session.Laps)
                {
                    using var command = new SQLiteCommand(insertLap, connection);
                    command.Parameters.AddWithValue("@sessionId", sessionId);
                    command.Parameters.AddWithValue("@lapNum", lap.LapNumber);
                    command.Parameters.AddWithValue("@lapTime", lap.LapTime.ToString());
                    command.Parameters.AddWithValue("@fuel", lap.FuelUsed);
                    command.Parameters.AddWithValue("@remaining", lap.FuelRemaining);
                    command.Parameters.AddWithValue("@valid", lap.IsValid ? 1 : 0);
                    command.Parameters.AddWithValue("@clear", lap.IsClear ? 1 : 0);
                    command.Parameters.AddWithValue("@tyre", lap.TyreWearAverage);
                    command.Parameters.AddWithValue("@timestamp", lap.Timestamp.ToString("o"));
                    await command.ExecuteNonQueryAsync();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private async Task<List<LapData>> GetLapsForSession(SQLiteConnection connection, int sessionId)
        {
            string query = @"
                SELECT LapNumber, LapTime, FuelUsed, FuelRemaining, IsValid, IsClear, 
                       TyreWearAverage, Timestamp
                FROM Laps
                WHERE SessionId = @sessionId
                ORDER BY LapNumber";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@sessionId", sessionId);

            var laps = new List<LapData>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                laps.Add(new LapData
                {
                    LapNumber = reader.GetInt32(0),
                    LapTime = TimeSpan.Parse(reader.GetString(1)),
                    FuelUsed = reader.GetDouble(2),
                    FuelRemaining = reader.GetDouble(3),
                    IsValid = reader.GetInt32(4) == 1,
                    IsClear = reader.GetInt32(5) == 1,
                    TyreWearAverage = reader.GetDouble(6),
                    Timestamp = DateTime.Parse(reader.GetString(7))
                });
            }

            return laps;
        }

        /// <summary>
        /// Store time-series session data for replay processing
        /// </summary>
        public async Task StoreTimeSeriesSession(
            string driver, 
            string track, 
            string car,
            DateTime sessionDate,
            string sessionId,
            string sessionType,
            int lapCount,
            double fuelPerLap,
            double avgLapTime,
            double lapTimeStdDev,
            string? replayFilePath = null)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            string insert = @"
                INSERT INTO ProfileTimeSeries 
                (DriverName, TrackName, CarName, SessionDate, SessionId, SessionType,
                 LapCount, FuelPerLap, AvgLapTime, LapTimeStdDev, ProcessedDate, ReplayFilePath)
                VALUES (@driver, @track, @car, @date, @sessionId, @type,
                        @lapCount, @fuel, @avgLap, @stdDev, @processed, @replay)";

            using var command = new SQLiteCommand(insert, connection);
            command.Parameters.AddWithValue("@driver", driver);
            command.Parameters.AddWithValue("@track", track);
            command.Parameters.AddWithValue("@car", car);
            command.Parameters.AddWithValue("@date", sessionDate.ToString("o"));
            command.Parameters.AddWithValue("@sessionId", sessionId ?? string.Empty);
            command.Parameters.AddWithValue("@type", sessionType);
            command.Parameters.AddWithValue("@lapCount", lapCount);
            command.Parameters.AddWithValue("@fuel", fuelPerLap);
            command.Parameters.AddWithValue("@avgLap", avgLapTime);
            command.Parameters.AddWithValue("@stdDev", lapTimeStdDev);
            command.Parameters.AddWithValue("@processed", DateTime.Now.ToString("o"));
            command.Parameters.AddWithValue("@replay", replayFilePath ?? string.Empty);

            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Get all time-series sessions for a driver/track/car combination
        /// Ordered chronologically (oldest to newest)
        /// </summary>
        public async Task<List<(DateTime Date, int LapCount, double FuelPerLap, double TyreDeg)>> GetTimeSeries(
            string driver, 
            string track, 
            string car)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT SessionDate, LapCount, FuelPerLap, AvgLapTime
                FROM ProfileTimeSeries
                WHERE DriverName = @driver AND TrackName = @track AND CarName = @car
                ORDER BY SessionDate ASC";

            using var command = new SQLiteCommand(query, connection);
            command.Parameters.AddWithValue("@driver", driver);
            command.Parameters.AddWithValue("@track", track);
            command.Parameters.AddWithValue("@car", car);

            var series = new List<(DateTime, int, double, double)>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var date = DateTime.Parse(reader.GetString(0));
                var lapCount = reader.GetInt32(1);
                var fuelPerLap = reader.GetDouble(2);
                var avgLapTime = reader.GetDouble(3);

                series.Add((date, lapCount, fuelPerLap, avgLapTime));
            }

            return series;
        }

        /// <summary>
        /// Update profile with confidence and staleness information
        /// </summary>
        public async Task UpdateProfileMetadata(
            string driver,
            string track,
            string car,
            double confidence,
            bool isStale,
            DateTime lastSessionDate)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            string update = @"
                UPDATE Profiles
                SET Confidence = @confidence,
                    IsStale = @stale,
                    LastSessionDate = @lastSession
                WHERE DriverName = @driver AND TrackName = @track AND CarName = @car";

            using var command = new SQLiteCommand(update, connection);
            command.Parameters.AddWithValue("@confidence", confidence);
            command.Parameters.AddWithValue("@stale", isStale ? 1 : 0);
            command.Parameters.AddWithValue("@lastSession", lastSessionDate.ToString("o"));
            command.Parameters.AddWithValue("@driver", driver);
            command.Parameters.AddWithValue("@track", track);
            command.Parameters.AddWithValue("@car", car);

            await command.ExecuteNonQueryAsync();
        }
    }
}
