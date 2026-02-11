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

            // Debug: print first 10 lap numbers
            _output.WriteLine($"First 10 lap numbers: {string.Join(", ", samples.Take(10).Select(s => s.LapNumber))}");
            _output.WriteLine($"Last 10 lap numbers: {string.Join(", ", samples.Skip(Math.Max(0, samples.Count - 10)).Select(s => s.LapNumber))}");

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
            // Note: GPS Time ranges from ~37.62 to ~137.61 seconds
            // Lap 0 ends at ts=181.42, so all samples in this range should be Lap 0
            var lap0Count = samples.Count(s => s.LapNumber == 0);
            var nonZeroLap = samples.Where(s => s.LapNumber > 0).ToList();
            _output.WriteLine($"Samples with Lap 0: {lap0Count}");
            _output.WriteLine($"Samples with Lap > 0: {nonZeroLap.Count}");

            // Assertions
            Assert.NotEmpty(nonZeroBrake); // Should have brake data
            Assert.True(lap0Count > 1900, $"Expected most samples to be Lap 0, got {lap0Count}"); // Should be mostly Lap 0
            Assert.True(nonZeroLap.Count == 0, $"Expected no Lap > 0 samples in first 100 seconds, got {nonZeroLap.Count}"); // Should have no Lap > 0
        }
    }
}
