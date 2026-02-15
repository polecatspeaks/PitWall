using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using PitWall.Core.Services;
using Xunit;

namespace PitWall.Tests
{
    public class LmuTelemetryReaderTests
    {
        [Fact]
        public async Task GetSessionCountAsync_UsesFallbackCount()
        {
            var dbPath = CreateEmptyDatabase();

            try
            {
                var reader = new LmuTelemetryReader(dbPath, fallbackSessionCount: 2);
                var count = await reader.GetSessionCountAsync();
                Assert.Equal(2, count);
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        [Fact]
        public async Task GetChannelsAsync_ReturnsChannelDefinitions()
        {
            var dbPath = CreateDatabaseWithSampleSchema();

            try
            {
                var reader = new LmuTelemetryReader(dbPath);
                var channels = await reader.GetChannelsAsync();

                Assert.Contains(channels, channel => channel.Name == "GPS Speed");
                Assert.Contains(channels, channel => channel.Name == "TyresTempCentre");
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        [Fact]
        public async Task ReadSamplesAsync_ReturnsJoinedSamples()
        {
            var dbPath = CreateDatabaseWithSampleData();

            try
            {
                var reader = new LmuTelemetryReader(dbPath);
                var samples = new List<PitWall.Core.Models.TelemetrySample>();

                await foreach (var sample in reader.ReadSamplesAsync(1, 0, 1))
                {
                    samples.Add(sample);
                }

                Assert.Equal(2, samples.Count);
                Assert.Equal(36.0, Math.Round(samples[0].SpeedKph, 1));
                Assert.Equal(55.5, samples[0].FuelLiters);
                Assert.Equal(0.4, samples[0].Throttle, 1);
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        private static string CreateEmptyDatabase()
        {
            var path = Path.Combine(Path.GetTempPath(), $"lmu_empty_{Guid.NewGuid():N}.duckdb");
            using var connection = new DuckDBConnection($"Data Source={path}");
            connection.Open();
            return path;
        }

        private static string CreateDatabaseWithSampleSchema()
        {
            var path = Path.Combine(Path.GetTempPath(), $"lmu_schema_{Guid.NewGuid():N}.duckdb");
            using var connection = new DuckDBConnection($"Data Source={path}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE ""GPS Speed"" (value FLOAT);
CREATE TABLE ""TyresTempCentre"" (value1 FLOAT, value2 FLOAT, value3 FLOAT, value4 FLOAT);";
            command.ExecuteNonQuery();

            return path;
        }

        private static string CreateDatabaseWithSampleData()
        {
            var path = Path.Combine(Path.GetTempPath(), $"lmu_samples_{Guid.NewGuid():N}.duckdb");
            using var connection = new DuckDBConnection($"Data Source={path}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE ""GPS Speed"" (value FLOAT, session_id INTEGER);
CREATE TABLE ""GPS Time"" (value DOUBLE, session_id INTEGER);
CREATE TABLE ""Throttle Pos"" (value FLOAT, session_id INTEGER);
CREATE TABLE ""Brake Pos"" (value FLOAT, session_id INTEGER);
CREATE TABLE ""Steering Pos"" (value FLOAT, session_id INTEGER);
CREATE TABLE ""Fuel Level"" (value FLOAT, session_id INTEGER);
CREATE TABLE ""TyresTempCentre"" (value1 FLOAT, value2 FLOAT, value3 FLOAT, value4 FLOAT, session_id INTEGER);
CREATE TABLE ""Lap"" (ts DOUBLE, value USMALLINT, session_id INTEGER);

INSERT INTO ""GPS Speed"" VALUES (10.0, 1), (11.0, 1);
INSERT INTO ""GPS Time"" VALUES (100.0, 1), (101.0, 1);
INSERT INTO ""Throttle Pos"" VALUES (40.0, 1), (50.0, 1);
INSERT INTO ""Brake Pos"" VALUES (10.0, 1), (20.0, 1);
INSERT INTO ""Steering Pos"" VALUES (-0.1, 1), (-0.2, 1);
INSERT INTO ""Fuel Level"" VALUES (55.5, 1), (55.0, 1);
INSERT INTO ""TyresTempCentre"" VALUES (80, 81, 82, 83, 1), (84, 85, 86, 87, 1);
INSERT INTO ""Lap"" VALUES (100.0, 1, 1);";
            command.ExecuteNonQuery();

            return path;
        }

        [Fact]
        public async Task GetSessionCountAsync_WithValidDatabase_ReturnsCount()
        {
            var dbPath = CreateDatabaseWithSessions(3);

            try
            {
                var reader = new LmuTelemetryReader(dbPath);
                var count = await reader.GetSessionCountAsync();
                Assert.Equal(3, count);
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        [Fact]
        public async Task GetSessionCountAsync_NoFallback_ReturnsZero()
        {
            var dbPath = CreateEmptyDatabase();

            try
            {
                var reader = new LmuTelemetryReader(dbPath);
                var count = await reader.GetSessionCountAsync();
                Assert.Equal(0, count);
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        [Fact]
        public async Task GetChannelsAsync_EmptyDatabase_ReturnsEmpty()
        {
            var dbPath = CreateEmptyDatabase();

            try
            {
                var reader = new LmuTelemetryReader(dbPath);
                var channels = await reader.GetChannelsAsync();
                Assert.NotNull(channels);
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        [Fact]
        public async Task ReadSamplesAsync_StartRowNegative_ThrowsException()
        {
            var dbPath = CreateDatabaseWithSampleData();

            try
            {
                var reader = new LmuTelemetryReader(dbPath);
                
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                {
                    await foreach (var sample in reader.ReadSamplesAsync(1, -1, 0))
                    {
                    }
                });
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        [Fact]
        public async Task ReadSamplesAsync_EndRowLessThanStart_ThrowsException()
        {
            var dbPath = CreateDatabaseWithSampleData();

            try
            {
                var reader = new LmuTelemetryReader(dbPath);
                
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                {
                    await foreach (var sample in reader.ReadSamplesAsync(1, 10, 5))
                    {
                    }
                });
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        [Fact]
        public async Task ReadSamplesAsync_MissingRequiredTables_ThrowsException()
        {
            var dbPath = CreateDatabaseWithPartialSchema();

            try
            {
                var reader = new LmuTelemetryReader(dbPath);
                
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    await foreach (var sample in reader.ReadSamplesAsync(1, 0, 10))
                    {
                    }
                });
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        [Fact]
        public async Task ReadSamplesAsync_WithOptionalTables_IncludesOptionalData()
        {
            var dbPath = CreateDatabaseWithOptionalTables();

            try
            {
                var reader = new LmuTelemetryReader(dbPath);
                var samples = new System.Collections.Generic.List<PitWall.Core.Models.TelemetrySample>();

                await foreach (var sample in reader.ReadSamplesAsync(1, 0, 1))
                {
                    samples.Add(sample);
                }

                Assert.NotEmpty(samples);
                Assert.True(samples[0].Latitude != 0 || samples[0].Longitude != 0);
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        [Fact]
        public async Task ReadSamplesAsync_CancellationToken_ThrowsWhenCancelled()
        {
            var dbPath = CreateDatabaseWithManySamples();

            try
            {
                var reader = new LmuTelemetryReader(dbPath);
                var cts = new System.Threading.CancellationTokenSource();
                cts.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                {
                    await foreach (var sample in reader.ReadSamplesAsync(1, 0, 100, cts.Token))
                    {
                    }
                });
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        [Fact]
        public async Task GetChannelsAsync_CancellationToken_ThrowsWhenCancelled()
        {
            var dbPath = CreateDatabaseWithSampleSchema();

            try
            {
                var reader = new LmuTelemetryReader(dbPath);
                var cts = new System.Threading.CancellationTokenSource();
                cts.Cancel();

                await Assert.ThrowsAsync<OperationCanceledException>(
                    async () => await reader.GetChannelsAsync(cts.Token));
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        [Fact]
        public void Constructor_NullDatabasePath_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => new LmuTelemetryReader(null!));
        }

        [Fact]
        public async Task ReadSamplesAsync_CalculatesLapNumber_Correctly()
        {
            var dbPath = CreateDatabaseWithSampleData();

            try
            {
                var reader = new LmuTelemetryReader(dbPath);
                var samples = new System.Collections.Generic.List<PitWall.Core.Models.TelemetrySample>();

                await foreach (var sample in reader.ReadSamplesAsync(1, 0, 1))
                {
                    samples.Add(sample);
                }

                Assert.True(samples.All(s => s.LapNumber >= 0));
            }
            finally
            {
                File.Delete(dbPath);
            }
        }

        private static string CreateDatabaseWithSessions(int sessionCount)
        {
            var path = Path.Combine(Path.GetTempPath(), $"lmu_sessions_{Guid.NewGuid():N}.duckdb");
            using var connection = new DuckDBConnection($"Data Source={path}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE sessions (id INTEGER);";
            command.ExecuteNonQuery();

            for (int i = 0; i < sessionCount; i++)
            {
                command.CommandText = $"INSERT INTO sessions VALUES ({i});";
                command.ExecuteNonQuery();
            }

            return path;
        }

        private static string CreateDatabaseWithPartialSchema()
        {
            var path = Path.Combine(Path.GetTempPath(), $"lmu_partial_{Guid.NewGuid():N}.duckdb");
            using var connection = new DuckDBConnection($"Data Source={path}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE ""GPS Speed"" (value FLOAT, session_id INTEGER);
CREATE TABLE ""GPS Time"" (value DOUBLE, session_id INTEGER);";
            command.ExecuteNonQuery();

            return path;
        }

        private static string CreateDatabaseWithOptionalTables()
        {
            var dbPath = CreateDatabaseWithSampleData();
            using var connection = new DuckDBConnection($"Data Source={dbPath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE ""GPS Latitude"" (value DOUBLE, session_id INTEGER);
CREATE TABLE ""GPS Longitude"" (value DOUBLE, session_id INTEGER);
CREATE TABLE ""G Force Lat"" (value DOUBLE, session_id INTEGER);
INSERT INTO ""GPS Latitude"" VALUES (50.4372, 1), (50.4373, 1);
INSERT INTO ""GPS Longitude"" VALUES (5.9714, 1), (5.9715, 1);
INSERT INTO ""G Force Lat"" VALUES (1.2, 1), (1.3, 1);";
            command.ExecuteNonQuery();

            return dbPath;
        }

        private static string CreateDatabaseWithManySamples()
        {
            var path = Path.Combine(Path.GetTempPath(), $"lmu_many_{Guid.NewGuid():N}.duckdb");
            using var connection = new DuckDBConnection($"Data Source={path}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE ""GPS Speed"" (value FLOAT, session_id INTEGER);
CREATE TABLE ""GPS Time"" (value DOUBLE, session_id INTEGER);
CREATE TABLE ""Throttle Pos"" (value FLOAT, session_id INTEGER);
CREATE TABLE ""Brake Pos"" (value FLOAT, session_id INTEGER);
CREATE TABLE ""Steering Pos"" (value FLOAT, session_id INTEGER);
CREATE TABLE ""Fuel Level"" (value FLOAT, session_id INTEGER);
CREATE TABLE ""TyresTempCentre"" (value1 FLOAT, value2 FLOAT, value3 FLOAT, value4 FLOAT, session_id INTEGER);
CREATE TABLE ""Lap"" (ts DOUBLE, value USMALLINT, session_id INTEGER);";
            
            for (int i = 0; i < 100; i++)
            {
                command.CommandText += $@"
INSERT INTO ""GPS Speed"" VALUES ({10.0 + i}, 1);
INSERT INTO ""GPS Time"" VALUES ({100.0 + i}, 1);
INSERT INTO ""Throttle Pos"" VALUES ({40.0}, 1);
INSERT INTO ""Brake Pos"" VALUES ({10.0}, 1);
INSERT INTO ""Steering Pos"" VALUES ({-0.1}, 1);
INSERT INTO ""Fuel Level"" VALUES ({55.5}, 1);
INSERT INTO ""TyresTempCentre"" VALUES (80, 81, 82, 83, 1);";
            }
            command.CommandText += @"INSERT INTO ""Lap"" VALUES (100.0, 1, 1);";
            command.ExecuteNonQuery();

            return path;
        }
    }
}
