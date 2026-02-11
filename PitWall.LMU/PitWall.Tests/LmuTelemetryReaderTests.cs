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

    }
}
