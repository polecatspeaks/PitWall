using System;
using System.IO.MemoryMappedFiles;

namespace PitWall.Tools.MemoryDumper
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("LMU Memory Dumper - attempting to open memory...");
            var name = "Local\\LMU_Telemetry";
            try
            {
                using var mmf = MemoryMappedFile.OpenExisting(name);
                using var accessor = mmf.CreateViewAccessor();
                var buffer = new byte[accessor.Capacity];
                accessor.ReadArray(0, buffer, 0, buffer.Length);
                Console.WriteLine($"Read {buffer.Length} bytes from {name}");
                var path = System.IO.Path.Combine(Environment.CurrentDirectory, "dumps");
                System.IO.Directory.CreateDirectory(path);
                var file = System.IO.Path.Combine(path, $"dump_{DateTime.UtcNow:yyyyMMddHHmmss}.bin");
                System.IO.File.WriteAllBytes(file, buffer);
                Console.WriteLine($"Wrote dump to {file}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed: {ex.Message}");
            }
        }
    }
}
