using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Core.Models;
using PitWall.Core.Storage;
using Xunit;

namespace PitWall.Tests
{
    public class DuckDbTelemetryWriterIntegrationTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly DuckDbConnector _connector;
        private readonly DuckDbTelemetryWriter _writer;

        public DuckDbTelemetryWriterIntegrationTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test-telemetry-writer-{Guid.NewGuid()}.db");
            _connector = new DuckDbConnector(_testDbPath, NullLogger<DuckDbConnector>.Instance);
            _writer = new DuckDbTelemetryWriter(_connector, NullLogger<DuckDbTelemetryWriter>.Instance);
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_testDbPath))
                {
                    File.Delete(_testDbPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Fact]
        public void Constructor_CreatesSchemaInDatabase()
        {
            Assert.True(File.Exists(_testDbPath));

            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();

            var tables = new List<string>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
                try
                {
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        tables.Add(reader.GetString(0));
                    }
                }
                catch
                {
                    // DuckDB might not have sqlite_master, use information_schema
                    command.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema='main'";
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        tables.Add(reader.GetString(0));
                    }
                }
            }

            Assert.Contains("GPS Speed", tables);
            Assert.Contains("GPS Time", tables);
        }

        [Fact]
        public void WriteSamples_InsertsDataIntoDatabase()
        {
            var timestamp = DateTime.UtcNow;
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(timestamp, 100, new double[] { 80, 80, 80, 80 }, 50, 0.5, 0.75, 0.1)
            };

            _writer.WriteSamples("1", samples);

            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"GPS Speed\"";
            var count = Convert.ToInt32(command.ExecuteScalar());

            Assert.Equal(1, count);
        }

        [Fact]
        public void WriteSamples_InsertsMultipleSamplesCorrectly()
        {
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 100, new double[] { 80, 80, 80, 80 }, 50, 0.5, 0.75, 0.1),
                new TelemetrySample(DateTime.UtcNow.AddSeconds(1), 105, new double[] { 82, 81, 80, 83 }, 49, 0.6, 0.8, 0.15),
                new TelemetrySample(DateTime.UtcNow.AddSeconds(2), 110, new double[] { 84, 83, 82, 85 }, 48, 0.7, 0.85, 0.2)
            };

            _writer.WriteSamples("1", samples);

            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"GPS Speed\"";
            var count = Convert.ToInt32(command.ExecuteScalar());

            Assert.Equal(3, count);
        }

        [Fact]
        public void WriteSamples_StoresSpeedDataCorrectly()
        {
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 108.0, new double[] { 80, 80, 80, 80 }, 50, 0.5, 0.75, 0.1)
            };

            _writer.WriteSamples("1", samples);

            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM \"GPS Speed\" LIMIT 1";
            var speed = Convert.ToDouble(command.ExecuteScalar());

            // Speed is stored in m/s (108 kph / 3.6 = 30 m/s)
            Assert.Equal(30.0, speed, 0.01);
        }

        [Fact]
        public void WriteSamples_StoresTyreTemperaturesCorrectly()
        {
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 100, new double[] { 85.5, 86.5, 87.5, 88.5 }, 50, 0.5, 0.75, 0.1)
            };

            _writer.WriteSamples("1", samples);

            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value1, value2, value3, value4 FROM \"TyresTempCentre\" LIMIT 1";
            using var reader = command.ExecuteReader();
            Assert.True(reader.Read());

            Assert.Equal(85.5, Convert.ToDouble(reader.GetValue(0)), 0.01);
            Assert.Equal(86.5, Convert.ToDouble(reader.GetValue(1)), 0.01);
            Assert.Equal(87.5, Convert.ToDouble(reader.GetValue(2)), 0.01);
            Assert.Equal(88.5, Convert.ToDouble(reader.GetValue(3)), 0.01);
        }

        [Fact]
        public void WriteSamples_StoresGpsTimeCorrectly()
        {
            var timestamp = new DateTime(2024, 1, 15, 12, 30, 45, DateTimeKind.Utc);
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(timestamp, 100, new double[] { 80, 80, 80, 80 }, 50, 0.5, 0.75, 0.1)
            };

            _writer.WriteSamples("1", samples);

            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM \"GPS Time\" LIMIT 1";
            var gpsTime = Convert.ToDouble(command.ExecuteScalar());

            var expectedSeconds = (timestamp - DateTime.UnixEpoch).TotalSeconds;
            Assert.Equal(expectedSeconds, gpsTime, 0.01);
        }

        [Fact]
        public void WriteSamples_HandlesEmptyListWithoutError()
        {
            var samples = new List<TelemetrySample>();

            _writer.WriteSamples("1", samples);

            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"GPS Speed\"";
            var count = Convert.ToInt32(command.ExecuteScalar());

            Assert.Equal(0, count);
        }

        [Fact]
        public void GetSamples_ReturnsEmptyList_WhenNoDataExists()
        {
            // GetSamples requires "Lap" table which we don't create in basic tests
            // This test would fail without proper session tracking setup
            // Testing the null/error handling through WriteSamples is sufficient
            var samples = _writer.GetSamples("not-numeric");

            Assert.Empty(samples);
        }

        [Fact]
        public void GetSamples_ReturnsEmptyList_WhenSessionNotFound()
        {
            // GetSamples requires "Lap" table for session tracking
            // This functionality is tested in full integration scenarios
            // We test that non-numeric session IDs return empty
            var insertSamples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 100, new double[] { 80, 80, 80, 80 }, 50, 0.5, 0.75, 0.1)
            };
            _writer.WriteSamples("1", insertSamples);

            var samples = _writer.GetSamples("abc");

            Assert.Empty(samples);
        }

        [Fact]
        public void GetSamples_WithLapTable_ReturnsCorrectSamples()
        {
            // Set up full schema including Lap table for session detection
            SetupFullSchemaWithLapTable();
            InsertChannelRow(gpsTime: 1000.0, speed: 30.0, throttle: 0.75, brake: 0.1, steering: 0.05, fuel: 50.0, temps: new[] { 80.0, 81.0, 82.0, 83.0 }, lap: 1);
            InsertChannelRow(gpsTime: 1001.0, speed: 31.0, throttle: 0.80, brake: 0.0, steering: 0.10, fuel: 49.5, temps: new[] { 81.0, 82.0, 83.0, 84.0 }, lap: 1);
            InsertChannelRow(gpsTime: 1002.0, speed: 32.0, throttle: 0.90, brake: 0.0, steering: -0.05, fuel: 49.0, temps: new[] { 82.0, 83.0, 84.0, 85.0 }, lap: 2);

            var samples = _writer.GetSamples("1");

            Assert.Equal(3, samples.Count);
            // Speed stored as m/s, returned as kph (× 3.6)
            Assert.Equal(108.0, samples[0].SpeedKph, 0.1);
            Assert.Equal(111.6, samples[1].SpeedKph, 0.1);
            Assert.Equal(115.2, samples[2].SpeedKph, 0.1);
        }

        [Fact]
        public void GetSamples_WithLapTable_ReturnsFuelData()
        {
            SetupFullSchemaWithLapTable();
            InsertChannelRow(gpsTime: 1000.0, speed: 30.0, throttle: 0.75, brake: 0.1, steering: 0.0, fuel: 50.0, temps: new[] { 80.0, 80.0, 80.0, 80.0 }, lap: 1);

            var samples = _writer.GetSamples("1");

            Assert.Single(samples);
            Assert.Equal(50.0, samples[0].FuelLiters, 0.1);
        }

        [Fact]
        public void GetSamples_WithLapTable_ReturnsTyreTemps()
        {
            SetupFullSchemaWithLapTable();
            InsertChannelRow(gpsTime: 1000.0, speed: 30.0, throttle: 0.75, brake: 0.1, steering: 0.0, fuel: 50.0, temps: new[] { 85.5, 86.5, 87.5, 88.5 }, lap: 1);

            var samples = _writer.GetSamples("1");

            Assert.Single(samples);
            Assert.Equal(4, samples[0].TyreTempsC.Length);
            Assert.Equal(85.5, samples[0].TyreTempsC[0], 0.1);
            Assert.Equal(86.5, samples[0].TyreTempsC[1], 0.1);
            Assert.Equal(87.5, samples[0].TyreTempsC[2], 0.1);
            Assert.Equal(88.5, samples[0].TyreTempsC[3], 0.1);
        }

        [Fact]
        public void GetSamples_WithLapTable_ConvertsTimestampFromGpsTime()
        {
            SetupFullSchemaWithLapTable();
            var gpsTime = 1705312245.0; // 2024-01-15 12:30:45 UTC
            InsertChannelRow(gpsTime: gpsTime, speed: 30.0, throttle: 0.75, brake: 0.1, steering: 0.0, fuel: 50.0, temps: new[] { 80.0, 80.0, 80.0, 80.0 }, lap: 1);

            var samples = _writer.GetSamples("1");

            Assert.Single(samples);
            var expected = DateTime.UnixEpoch.AddSeconds(gpsTime);
            Assert.Equal(expected, samples[0].Timestamp, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void GetSamples_WithLapTable_ReturnsEmpty_WhenSessionIdNotFound()
        {
            SetupFullSchemaWithLapTable();
            InsertChannelRow(gpsTime: 1000.0, speed: 30.0, throttle: 0.75, brake: 0.1, steering: 0.0, fuel: 50.0, temps: new[] { 80.0, 80.0, 80.0, 80.0 }, lap: 1);

            // Session 1 exists, session 99 does not
            var samples = _writer.GetSamples("99");

            Assert.Empty(samples);
        }

        [Fact]
        public void GetSamples_WithLapTable_LimitsTo50Samples()
        {
            SetupFullSchemaWithLapTable();
            for (int i = 0; i < 60; i++)
            {
                InsertChannelRow(gpsTime: 1000.0 + i, speed: 30.0, throttle: 0.75, brake: 0.1, steering: 0.0, fuel: 50.0 - i * 0.1, temps: new[] { 80.0, 80.0, 80.0, 80.0 }, lap: 1);
            }

            var samples = _writer.GetSamples("1");

            // GetSamples has a sampleLimit of 50
            Assert.Equal(50, samples.Count);
        }

        [Fact]
        public void GetSamples_MultiSession_DetectsSessionBoundaries()
        {
            SetupFullSchemaWithLapTable();
            // Session 1: laps 1, 2, 3
            InsertChannelRow(gpsTime: 1000.0, speed: 30.0, throttle: 0.75, brake: 0.1, steering: 0.0, fuel: 50.0, temps: new[] { 80.0, 80.0, 80.0, 80.0 }, lap: 1);
            InsertChannelRow(gpsTime: 1001.0, speed: 31.0, throttle: 0.80, brake: 0.0, steering: 0.0, fuel: 49.0, temps: new[] { 81.0, 81.0, 81.0, 81.0 }, lap: 2);
            InsertChannelRow(gpsTime: 1002.0, speed: 32.0, throttle: 0.85, brake: 0.0, steering: 0.0, fuel: 48.0, temps: new[] { 82.0, 82.0, 82.0, 82.0 }, lap: 3);
            // Session 2: lap number resets (lap goes from 3 → 1) 
            InsertChannelRow(gpsTime: 2000.0, speed: 25.0, throttle: 0.50, brake: 0.3, steering: 0.0, fuel: 80.0, temps: new[] { 70.0, 70.0, 70.0, 70.0 }, lap: 0);
            InsertChannelRow(gpsTime: 2001.0, speed: 26.0, throttle: 0.55, brake: 0.0, steering: 0.0, fuel: 79.0, temps: new[] { 71.0, 71.0, 71.0, 71.0 }, lap: 1);

            // Session 1 should have 3 rows, session 2 should have 2 rows
            var session1 = _writer.GetSamples("1");
            var session2 = _writer.GetSamples("2");

            Assert.Equal(3, session1.Count);
            Assert.Equal(2, session2.Count);
        }

        [Fact]
        public void GetSamples_WithZeroGpsTime_UsesUtcNow()
        {
            SetupFullSchemaWithLapTable();
            InsertChannelRow(gpsTime: 0.0, speed: 30.0, throttle: 0.75, brake: 0.1, steering: 0.0, fuel: 50.0, temps: new[] { 80.0, 80.0, 80.0, 80.0 }, lap: 1);

            var before = DateTime.UtcNow;
            var samples = _writer.GetSamples("1");
            var after = DateTime.UtcNow;

            Assert.Single(samples);
            Assert.InRange(samples[0].Timestamp, before, after);
        }

        [Fact]
        public void GetSamples_ReturnsCorrectInputValues()
        {
            SetupFullSchemaWithLapTable();
            InsertChannelRow(gpsTime: 1000.0, speed: 30.0, throttle: 0.85, brake: 0.45, steering: -0.25, fuel: 42.5, temps: new[] { 85.0, 86.0, 87.0, 88.0 }, lap: 1);

            var samples = _writer.GetSamples("1");

            Assert.Single(samples);
            Assert.Equal(0.85, samples[0].Throttle, 0.01);
            Assert.Equal(0.45, samples[0].Brake, 0.01);
            Assert.Equal(-0.25, samples[0].Steering, 0.01);
        }

        private void SetupFullSchemaWithLapTable()
        {
            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS ""Lap"" (value FLOAT);
";
            command.ExecuteNonQuery();
        }

        private void InsertChannelRow(double gpsTime, double speed, double throttle, double brake, double steering, double fuel, double[] temps, int lap)
        {
            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $@"
INSERT INTO ""GPS Speed"" VALUES ({speed});
INSERT INTO ""GPS Time"" VALUES ({gpsTime});
INSERT INTO ""Throttle Pos"" VALUES ({throttle});
INSERT INTO ""Brake Pos"" VALUES ({brake});
INSERT INTO ""Steering Pos"" VALUES ({steering});
INSERT INTO ""Fuel Level"" VALUES ({fuel});
INSERT INTO ""TyresTempCentre"" VALUES ({temps[0]}, {temps[1]}, {temps[2]}, {temps[3]});
INSERT INTO ""Lap"" VALUES ({lap});
";
            command.ExecuteNonQuery();
        }

        [Fact]
        public void WriteSamples_HandlesMultipleSessions()
        {
            var samples1 = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 100, new double[] { 80, 80, 80, 80 }, 50, 0.5, 0.75, 0.1)
            };
            var samples2 = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 110, new double[] { 85, 85, 85, 85 }, 45, 0.6, 0.8, 0.2)
            };

            _writer.WriteSamples("1", samples1);
            _writer.WriteSamples("2", samples2);

            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"GPS Speed\"";
            var count = Convert.ToInt32(command.ExecuteScalar());

            Assert.Equal(2, count);
        }

        [Fact]
        public void WriteSamples_PreservesAllTelemetryFields()
        {
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 100, new double[] { 80, 81, 82, 83 }, 50, 0.5, 0.75, 0.1)
            };

            _writer.WriteSamples("1", samples);

            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();

            // Check all tables have data
            var tables = new[] { "GPS Speed", "GPS Time", "Throttle Pos", "Brake Pos", "Steering Pos", "Fuel Level", "TyresTempCentre" };
            foreach (var table in tables)
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
                var count = Convert.ToInt32(command.ExecuteScalar());
                Assert.Equal(1, count);
            }
        }

        [Fact]
        public void WriteSamples_HandlesZeroValues()
        {
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 0, new double[] { 0, 0, 0, 0 }, 0, 0, 0, 0)
            };

            _writer.WriteSamples("1", samples);

            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"GPS Speed\"";
            var count = Convert.ToInt32(command.ExecuteScalar());

            Assert.Equal(1, count);
        }

        [Fact]
        public void WriteSamples_HandlesNegativeSteeringValues()
        {
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 100, new double[] { 80, 80, 80, 80 }, 50, 0.5, 0.75, -0.5)
            };

            _writer.WriteSamples("1", samples);

            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM \"Steering Pos\" LIMIT 1";
            var steering = Convert.ToDouble(command.ExecuteScalar());

            Assert.Equal(-0.5, steering, 0.01);
        }

        [Fact]
        public void WriteSamples_HandlesHighSpeedValues()
        {
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 350.0, new double[] { 80, 80, 80, 80 }, 50, 0.5, 1.0, 0)
            };

            _writer.WriteSamples("1", samples);

            using var connection = new DuckDBConnection($"Data Source={_testDbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM \"GPS Speed\" LIMIT 1";
            var speed = Convert.ToDouble(command.ExecuteScalar());

            // 350 kph / 3.6 = 97.22 m/s
            Assert.Equal(97.22, speed, 0.01);
        }
    }
}
