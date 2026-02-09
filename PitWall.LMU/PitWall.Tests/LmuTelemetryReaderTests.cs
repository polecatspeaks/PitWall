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
CREATE TABLE ""GPS Speed"" (value FLOAT);
CREATE TABLE ""GPS Time"" (value DOUBLE);
CREATE TABLE ""Throttle Pos"" (value FLOAT);
CREATE TABLE ""Brake Pos"" (value FLOAT);
CREATE TABLE ""Steering Pos"" (value FLOAT);
CREATE TABLE ""Fuel Level"" (value FLOAT);
CREATE TABLE ""TyresTempCentre"" (value1 FLOAT, value2 FLOAT, value3 FLOAT, value4 FLOAT);

INSERT INTO ""GPS Speed"" VALUES (10.0), (11.0);
INSERT INTO ""GPS Time"" VALUES (100.0), (101.0);
INSERT INTO ""Throttle Pos"" VALUES (0.4), (0.5);
INSERT INTO ""Brake Pos"" VALUES (0.1), (0.2);
INSERT INTO ""Steering Pos"" VALUES (-0.1), (-0.2);
INSERT INTO ""Fuel Level"" VALUES (55.5), (55.0);
INSERT INTO ""TyresTempCentre"" VALUES (80, 81, 82, 83), (84, 85, 86, 87);";
            command.ExecuteNonQuery();

            return path;
        }

    }
}
