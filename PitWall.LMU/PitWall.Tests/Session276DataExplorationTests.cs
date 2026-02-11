using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using Xunit;
using Xunit.Abstractions;

namespace PitWall.Tests
{
    public class Session276DataExplorationTests
    {
        private readonly ITestOutputHelper _output;
        private const string DbPath = "../../../../data/lmu_telemetry_session_276.db";

        public Session276DataExplorationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void CheckTableSchema()
        {
            if (!File.Exists(DbPath))
            {
                _output.WriteLine($"DB not found at {DbPath}, skipping.");
                return;
            }

            using var connection = new DuckDBConnection($"Data Source={DbPath}");
            connection.Open();

            // Check schema of Lap table
            _output.WriteLine("=== Lap table schema ===");
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT column_name, data_type 
                    FROM information_schema.columns 
                    WHERE table_name = 'Lap' 
                    ORDER BY ordinal_position;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    _output.WriteLine($"{reader.GetString(0)}: {reader.GetString(1)}");
                }
            }

            // Check all columns with gps_time or timestamp
            _output.WriteLine("\n=== Columns with 'gps' or 'time' in name ===");
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT table_name, column_name 
                    FROM information_schema.columns 
                    WHERE LOWER(column_name) LIKE '%gps%' OR LOWER(column_name) LIKE '%time%'
                    ORDER BY table_name, ordinal_position;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    _output.WriteLine($"{reader.GetString(0)}.{reader.GetString(1)}");
                }
            }
        }

        [Fact]
        public void ExploreSession276Data()
        {
            if (!File.Exists(DbPath))
            {
                _output.WriteLine($"DB not found at {DbPath}, skipping.");
                return;
            }

            using var connection = new DuckDBConnection($"Data Source={DbPath}");
            connection.Open();

            // Check row counts
            _output.WriteLine("=== Row Counts ===");
            var tables = new[] { "Throttle Pos", "Brake Pos", "Lap", "GPS Time" };
            foreach (var table in tables)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"SELECT COUNT(*) FROM \"{table}\" WHERE session_id = 276;";
                var count = cmd.ExecuteScalar();
                _output.WriteLine($"{table}: {count}");
            }

            // Check first 5 rows of Brake Pos
            _output.WriteLine("\n=== First 5 Brake Pos rows ===");
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT rowid, value FROM \"Brake Pos\" WHERE session_id = 276 ORDER BY rowid LIMIT 5;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    _output.WriteLine($"Row {reader.GetValue(0)}: {reader.GetValue(1)}");
                }
            }

            // Check rows 100-105 of Brake Pos
            _output.WriteLine("\n=== Brake Pos rows 100-105 ===");
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT rowid, value FROM \"Brake Pos\" WHERE session_id = 276 ORDER BY rowid LIMIT 6 OFFSET 99;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    _output.WriteLine($"Row {reader.GetValue(0)}: {reader.GetValue(1)}");
                }
            }

            // Check Lap values distribution
            _output.WriteLine("\n=== Lap values distribution ===");
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT value, COUNT(*) as cnt 
                    FROM ""Lap"" 
                    WHERE session_id = 276 
                    GROUP BY value 
                    ORDER BY value 
                    LIMIT 10;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    _output.WriteLine($"Lap {reader.GetValue(0)}: {reader.GetValue(1)} rows");
                }
            }

            // Check first 10 Lap values
            _output.WriteLine("\n=== First 10 Lap values ===");
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT rowid, value FROM \"Lap\" WHERE session_id = 276 ORDER BY rowid LIMIT 10;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    _output.WriteLine($"Row {reader.GetValue(0)}: {reader.GetValue(1)}");
                }
            }

            // Check brake values mid-stream
            _output.WriteLine("\n=== Brake Pos rows 5000-5005 ===");
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT rowid, value FROM \"Brake Pos\" WHERE session_id = 276 ORDER BY rowid LIMIT 6 OFFSET 4999;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    _output.WriteLine($"Row {reader.GetValue(0)}: {reader.GetValue(1)}");
                }
            }

            // Find first non-zero brake value
            _output.WriteLine("\n=== First 10 non-zero Brake values ===");
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT rowid, value FROM \"Brake Pos\" WHERE session_id = 276 AND value > 0 ORDER BY rowid LIMIT 10;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    _output.WriteLine($"Row {reader.GetValue(0)}: {reader.GetValue(1)}");
                }
            }

            // Check Lap ts values
            _output.WriteLine("\n=== All Lap rows with ts values ===");
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT rowid, ts, value FROM main.\"Lap\" WHERE session_id = 276 ORDER BY ts;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    _output.WriteLine($"Row {reader.GetValue(0)}: ts={reader.GetValue(1)}, lap={reader.GetValue(2)}");
                }
            }

            // Compare with GPS Time
            _output.WriteLine("\n=== GPS Time range ===");
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT MIN(value) as min_time, MAX(value) as max_time, COUNT(*) as cnt
                    FROM ""GPS Time"" 
                    WHERE session_id = 276;";
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    _output.WriteLine($"Min: {reader.GetValue(0)}, Max: {reader.GetValue(1)}, Count: {reader.GetValue(2)}");
                }
            }

            // Check Lap ts range
            _output.WriteLine("\n=== Lap ts range ===");
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT MIN(ts) as min_ts, MAX(ts) as max_ts, COUNT(*) as cnt
                    FROM main.""Lap"" 
                    WHERE session_id = 276;";
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    _output.WriteLine($"Min: {reader.GetValue(0)}, Max: {reader.GetValue(1)}, Count: {reader.GetValue(2)}");
                }
            }

            // Check which required tables have ts column
            _output.WriteLine("\n=== Tables with ts column ===");
            var requiredTables = new[] { "GPS Speed", "GPS Time", "Throttle Pos", "Brake Pos", "Steering Pos", "Fuel Level", "TyresTempCentre", "Lap" };
            foreach (var table in requiredTables)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT column_name 
                    FROM information_schema.columns 
                    WHERE table_name = ? AND column_name = 'ts';";
                var tableParam = cmd.CreateParameter();
                tableParam.Value = table;
                cmd.Parameters.Add(tableParam);
                using var reader = cmd.ExecuteReader();
                var hasTs = reader.Read();
                _output.WriteLine($"{table}: {(hasTs ? "YES" : "NO")}");
            }

            // Check GPS Time values
            _output.WriteLine("\n=== First 10 GPS Time values ===");
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT rowid, value FROM \"GPS Time\" WHERE session_id = 276 ORDER BY rowid LIMIT 10;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    _output.WriteLine($"Row {reader.GetValue(0)}: {reader.GetValue(1)}");
                }
            }
        }
    }
}
