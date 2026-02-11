using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PitWall.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace PitWall.Tests.Integration
{
    public class Session276TelemetryTests
    {
        private readonly ITestOutputHelper _output;
        private const string DbPath = "../../../../data/lmu_telemetry_session_276.db";

        public Session276TelemetryTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Session276_BrakeAndLapValues_ShouldAppearInStream()
        {
            if (!File.Exists(DbPath))
            {
                _output.WriteLine($"DB not found at {DbPath}, skipping.");
                return;
            }

            var reader = new LmuTelemetryReader(DbPath);
            var samples = new List<PitWall.Core.Models.TelemetrySample>();

            // Read first 2000 rows (should cover rows 0-1999, including row 1289 where brake starts)
            await foreach (var sample in reader.ReadSamplesAsync(276, 0, 1999))
            {
                samples.Add(sample);
            }

            _output.WriteLine($"Total samples read: {samples.Count}");

            // Check for brake values
            var nonZeroBrake = samples.Where(s => s.Brake > 0).ToList();
            _output.WriteLine($"Samples with non-zero brake: {nonZeroBrake.Count}");
            
            if (nonZeroBrake.Count > 0)
            {
                var first = nonZeroBrake.First();
                var index = samples.IndexOf(first);
                _output.WriteLine($"First non-zero brake at index {index}: brake={first.Brake:F4}, throttle={first.Throttle:F4}");
            }

            // Check for lap values
            var nonZeroLap = samples.Where(s => s.LapNumber > 0).ToList();
            _output.WriteLine($"Samples with non-zero lap: {nonZeroLap.Count}");
            
            if (nonZeroLap.Count > 0)
            {
                _output.WriteLine($"Lap values found: {string.Join(", ", nonZeroLap.Take(10).Select(s => s.LapNumber))}");
            }

            // Assertions
            Assert.NotEmpty(nonZeroBrake); // Should have brake data
            Assert.NotEmpty(nonZeroLap);   // Should have lap data
        }
    }
}
