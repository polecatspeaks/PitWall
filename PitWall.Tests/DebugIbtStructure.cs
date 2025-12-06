using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using PitWall.Telemetry;

namespace PitWall.Tests
{
    /// <summary>
    /// Debug test to inspect IBT file structure
    /// </summary>
    public class DebugIbtStructure
    {
        private readonly ITestOutputHelper _output;

        public DebugIbtStructure(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void InspectIbtFileStructure()
        {
            string ibtPath = @"C:\Users\ohzee\Documents\iRacing\telemetry\mclaren720sgt3_charlotte 2025 roval2025 2025-11-16 13-15-19.ibt";

            if (!File.Exists(ibtPath))
            {
                return;
            }

            using var reader = new IbtFileReader(ibtPath);

            var variables = reader.ReadVariableHeaders();
            _output.WriteLine($"Total Variables: {variables.Count}");
            _output.WriteLine("");

            // Show first 20 variables to see naming pattern
            _output.WriteLine("First 20 Variables:");
            for (int i = 0; i < Math.Min(20, variables.Count); i++)
            {
                var v = variables[i];
                _output.WriteLine($"  [{i}] {v.Name} (Type: {v.Type}, Offset: {v.Offset}, Unit: {v.Unit})");
            }

            _output.WriteLine("");

            // Look for key variables we need
            var keyVars = new[] { "Speed", "Throttle", "Brake", "RPM", "Gear", "FuelLevel", "Lap", "LapDist" };
            _output.WriteLine("Key Variables:");
            foreach (var key in keyVars)
            {
                var found = variables.FirstOrDefault(v => v.Name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
                if (found != null)
                {
                    _output.WriteLine($"  {found.Name} (Type: {found.Type}, Offset: {found.Offset}, Unit: {found.Unit})");
                }
                else
                {
                    _output.WriteLine($"  {key} - NOT FOUND");
                }
            }
            
            _output.WriteLine("");
            
            // Look for lap-related variables
            _output.WriteLine("Lap-related Variables:");
            var lapVars = variables.Where(v => v.Name.Trim('\0').IndexOf("lap", StringComparison.OrdinalIgnoreCase) >= 0).Take(15);
            foreach (var v in lapVars)
            {
                _output.WriteLine($"  {v.Name.Trim('\0')} (Type: {v.Type}, Offset: {v.Offset}, Unit: {v.Unit.Trim('\0')})");
            }
            
            _output.WriteLine("");
            _output.WriteLine("Testing sample reads:");
            var samples = reader.ReadTelemetrySamples();
            _output.WriteLine($"Total samples: {samples.Count}");
            if (samples.Count > 0)
            {
                _output.WriteLine($"Sample 0: Lap={samples[0].LapNumber}, Speed={samples[0].Speed}");
                _output.WriteLine($"Sample 1000: Lap={samples[Math.Min(1000, samples.Count-1)].LapNumber}, Speed={samples[Math.Min(1000, samples.Count-1)].Speed}");
                
                var distinctLaps = samples.Select(s => s.LapNumber).Distinct().OrderBy(n => n).Take(10).ToList();
                _output.WriteLine($"Distinct laps: {string.Join(", ", distinctLaps)}");
            }
        }
    }
}
