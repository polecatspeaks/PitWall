using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;
using PitWall.Models.Telemetry;

namespace PitWall.Storage.Telemetry
{
    /// <summary>
    /// SQLite implementation of ILapRepository
    /// 
    /// Schema:
    /// - Laps: Id (PK), SessionId (FK), LapNumber, LapTime, FuelUsed, AvgSpeed, MaxSpeed, AvgThrottle, AvgBrake, etc.
    /// - Foreign key to Sessions table
    /// </summary>
    public class SQLiteLapRepository : ILapRepository
    {
        private readonly string _dbPath;

        public SQLiteLapRepository(string dbPath)
        {
            _dbPath = dbPath;
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
                        CREATE TABLE IF NOT EXISTS Laps (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            SessionId TEXT NOT NULL,
                            LapNumber INTEGER NOT NULL,
                            LapTimeTicks BIGINT NOT NULL,
                            FuelUsed REAL NOT NULL,
                            AvgSpeed REAL NOT NULL,
                            MaxSpeed REAL NOT NULL,
                            AvgThrottle REAL NOT NULL,
                            AvgBrake REAL NOT NULL,
                            AvgSteeringAngle REAL NOT NULL,
                            AvgEngineRpm REAL NOT NULL,
                            AvgEngineTemp REAL NOT NULL,
                            FOREIGN KEY (SessionId) REFERENCES Sessions(SessionId) ON DELETE CASCADE
                        );
                        CREATE INDEX IF NOT EXISTS idx_laps_session ON Laps(SessionId);
                        CREATE INDEX IF NOT EXISTS idx_laps_session_lapnum ON Laps(SessionId, LapNumber);
                    ";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public async Task SaveLapsAsync(string sessionId, List<LapMetadata> laps)
        {
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                await conn.OpenAsync();

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var lap in laps)
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = @"
                                    INSERT INTO Laps 
                                    (SessionId, LapNumber, LapTimeTicks, FuelUsed, AvgSpeed, MaxSpeed, 
                                     AvgThrottle, AvgBrake, AvgSteeringAngle, AvgEngineRpm, AvgEngineTemp)
                                    VALUES 
                                    (@sessionId, @lapNumber, @lapTimeTicks, @fuelUsed, @avgSpeed, @maxSpeed,
                                     @avgThrottle, @avgBrake, @avgSteeringAngle, @avgEngineRpm, @avgEngineTemp)
                                ";
                                cmd.Parameters.AddWithValue("@sessionId", sessionId);
                                cmd.Parameters.AddWithValue("@lapNumber", lap.LapNumber);
                                cmd.Parameters.AddWithValue("@lapTimeTicks", lap.LapTime.Ticks);
                                cmd.Parameters.AddWithValue("@fuelUsed", lap.FuelUsed);
                                cmd.Parameters.AddWithValue("@avgSpeed", lap.AvgSpeed);
                                cmd.Parameters.AddWithValue("@maxSpeed", lap.MaxSpeed);
                                cmd.Parameters.AddWithValue("@avgThrottle", lap.AvgThrottle);
                                cmd.Parameters.AddWithValue("@avgBrake", lap.AvgBrake);
                                cmd.Parameters.AddWithValue("@avgSteeringAngle", lap.AvgSteeringAngle);
                                cmd.Parameters.AddWithValue("@avgEngineRpm", lap.AvgEngineRpm);
                                cmd.Parameters.AddWithValue("@avgEngineTemp", lap.AvgEngineTemp);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<List<LapMetadata>> GetSessionLapsAsync(string sessionId)
        {
            var laps = new List<LapMetadata>();

            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT LapNumber, LapTimeTicks, FuelUsed, AvgSpeed, MaxSpeed, 
                               AvgThrottle, AvgBrake, AvgSteeringAngle, AvgEngineRpm, AvgEngineTemp
                        FROM Laps
                        WHERE SessionId = @sessionId
                        ORDER BY LapNumber
                    ";
                    cmd.Parameters.AddWithValue("@sessionId", sessionId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            laps.Add(new LapMetadata
                            {
                                LapNumber = reader.GetInt32(0),
                                LapTime = TimeSpan.FromTicks(reader.GetInt64(1)),
                                FuelUsed = (float)reader.GetDouble(2),
                                AvgSpeed = (float)reader.GetDouble(3),
                                MaxSpeed = (float)reader.GetDouble(4),
                                AvgThrottle = (float)reader.GetDouble(5),
                                AvgBrake = (float)reader.GetDouble(6),
                                AvgSteeringAngle = (float)reader.GetDouble(7),
                                AvgEngineRpm = (int)reader.GetDouble(8),
                                AvgEngineTemp = (float)reader.GetDouble(9)
                            });
                        }
                    }
                }
            }

            return laps;
        }

        public async Task<LapMetadata?> GetLapAsync(string sessionId, int lapNumber)
        {
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT LapNumber, LapTimeTicks, FuelUsed, AvgSpeed, MaxSpeed, 
                               AvgThrottle, AvgBrake, AvgSteeringAngle, AvgEngineRpm, AvgEngineTemp
                        FROM Laps
                        WHERE SessionId = @sessionId AND LapNumber = @lapNumber
                    ";
                    cmd.Parameters.AddWithValue("@sessionId", sessionId);
                    cmd.Parameters.AddWithValue("@lapNumber", lapNumber);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            return null;
                        }

                        return new LapMetadata
                        {
                            LapNumber = reader.GetInt32(0),
                            LapTime = TimeSpan.FromTicks(reader.GetInt64(1)),
                            FuelUsed = (float)reader.GetDouble(2),
                            AvgSpeed = (float)reader.GetDouble(3),
                            MaxSpeed = (float)reader.GetDouble(4),
                            AvgThrottle = (float)reader.GetDouble(5),
                            AvgBrake = (float)reader.GetDouble(6),
                            AvgSteeringAngle = (float)reader.GetDouble(7),
                            AvgEngineRpm = (int)reader.GetDouble(8),
                            AvgEngineTemp = (float)reader.GetDouble(9)
                        };
                    }
                }
            }
        }
    }
}
