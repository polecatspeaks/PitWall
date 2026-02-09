using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace PitWall.Tools.TelemetrySimulator
{
    class Program
    {
        static void Main()
        {
            const string MEMORY_NAME = "Local\\LMU_Telemetry";
            const int MEMORY_SIZE = 8192;
            using var mmf = MemoryMappedFile.CreateNew(MEMORY_NAME, MEMORY_SIZE);
            using var accessor = mmf.CreateViewAccessor();
            Console.WriteLine("Simulating LMU telemetry. Press Ctrl+C to exit.");
            var rnd = new Random();
            while (true)
            {
                // write a simple pattern so dumper/reader can detect changes
                accessor.Write(0, rnd.Next());
                Thread.Sleep(100); // 10Hz by default for this simple simulator
            }
        }
    }
}
