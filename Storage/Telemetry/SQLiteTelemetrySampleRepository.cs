using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading.Tasks;
using PitWall.Models.Telemetry;

namespace PitWall.Storage.Telemetry
{
    /// <summary>
    /// SQLite implementation of ITelemetrySampleRepository
    /// 
    /// Performance considerations:
    /// - Bulk insert with transaction for 28K+ samples
    /// - Indexed by SessionId and LapNumber for efficient queries
    /// - Normalized schema to avoid data duplication
    /// </summary>
    public class SQLiteTelemetrySampleRepository : ITelemetrySampleRepository
    {
        private readonly string _dbPath;

        public SQLiteTelemetrySampleRepository(string dbPath)
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
                        CREATE TABLE IF NOT EXISTS TelemetrySamples (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            SessionId TEXT NOT NULL,
                            LapNumber INTEGER NOT NULL,
                            Speed REAL NOT NULL,
                            Throttle REAL NOT NULL,
                            Brake REAL NOT NULL,
                            Gear INTEGER NOT NULL,
                            EngineRpm INTEGER NOT NULL,
                            SteeringAngle REAL NOT NULL,
                            FuelLevel REAL NOT NULL,
                            FOREIGN KEY (SessionId) REFERENCES Sessions(SessionId) ON DELETE CASCADE
                        );
                        CREATE INDEX IF NOT EXISTS idx_samples_session ON TelemetrySamples(SessionId);
                        CREATE INDEX IF NOT EXISTS idx_samples_session_lap ON TelemetrySamples(SessionId, LapNumber);
                    ";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public async Task SaveSamplesAsync(string sessionId, List<TelemetrySample> samples)
        {
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                await conn.OpenAsync();

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // Use prepared statement for efficiency
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = @"
                                INSERT INTO TelemetrySamples 
                                (SessionId, LapNumber, Speed, Throttle, Brake, Gear, EngineRpm, SteeringAngle, FuelLevel)
                                VALUES 
                                (@sessionId, @lapNumber, @speed, @throttle, @brake, @gear, @engineRpm, @steeringAngle, @fuelLevel)
                            ";

                            var sessionIdParam = cmd.Parameters.Add("@sessionId", System.Data.DbType.String);
                            var lapNumberParam = cmd.Parameters.Add("@lapNumber", System.Data.DbType.Int32);
                            var speedParam = cmd.Parameters.Add("@speed", System.Data.DbType.Double);
                            var throttleParam = cmd.Parameters.Add("@throttle", System.Data.DbType.Double);
                            var brakeParam = cmd.Parameters.Add("@brake", System.Data.DbType.Double);
                            var gearParam = cmd.Parameters.Add("@gear", System.Data.DbType.Int32);
                            var engineRpmParam = cmd.Parameters.Add("@engineRpm", System.Data.DbType.Int32);
                            var steeringAngleParam = cmd.Parameters.Add("@steeringAngle", System.Data.DbType.Double);
                            var fuelLevelParam = cmd.Parameters.Add("@fuelLevel", System.Data.DbType.Double);

                            foreach (var sample in samples)
                            {
                                sessionIdParam.Value = sessionId;
                                lapNumberParam.Value = sample.LapNumber;
                                speedParam.Value = sample.Speed;
                                throttleParam.Value = sample.Throttle;
                                brakeParam.Value = sample.Brake;
                                gearParam.Value = sample.Gear;
                                engineRpmParam.Value = sample.EngineRpm;
                                steeringAngleParam.Value = sample.SteeringAngle;
                                fuelLevelParam.Value = sample.FuelLevel;
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

        public async Task<List<TelemetrySample>> GetSamplesAsync(string sessionId, int? lapNumber)
        {
            var samples = new List<TelemetrySample>();

            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    if (lapNumber.HasValue)
                    {
                        cmd.CommandText = @"
                            SELECT LapNumber, Speed, Throttle, Brake, Gear, EngineRpm, SteeringAngle, FuelLevel
                            FROM TelemetrySamples
                            WHERE SessionId = @sessionId AND LapNumber = @lapNumber
                            ORDER BY Id
                        ";
                        cmd.Parameters.AddWithValue("@sessionId", sessionId);
                        cmd.Parameters.AddWithValue("@lapNumber", lapNumber.Value);
                    }
                    else
                    {
                        cmd.CommandText = @"
                            SELECT LapNumber, Speed, Throttle, Brake, Gear, EngineRpm, SteeringAngle, FuelLevel
                            FROM TelemetrySamples
                            WHERE SessionId = @sessionId
                            ORDER BY Id
                        ";
                        cmd.Parameters.AddWithValue("@sessionId", sessionId);
                    }

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            samples.Add(new TelemetrySample
                            {
                                LapNumber = reader.GetInt32(0),
                                Speed = (float)reader.GetDouble(1),
                                Throttle = (float)reader.GetDouble(2),
                                Brake = (float)reader.GetDouble(3),
                                Gear = reader.GetInt32(4),
                                EngineRpm = reader.GetInt32(5),
                                SteeringAngle = (float)reader.GetDouble(6),
                                FuelLevel = (float)reader.GetDouble(7)
                            });
                        }
                    }
                }
            }

            return samples;
        }

        public async Task<int> GetSampleCountAsync(string sessionId)
        {
            using (var conn = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM TelemetrySamples WHERE SessionId = @sessionId";
                    cmd.Parameters.AddWithValue("@sessionId", sessionId);
                    var result = await cmd.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
        }
    }
}
