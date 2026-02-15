using System;
using System.IO;

namespace LMUMemoryReader;

public static class StartupLogger
{
    private static string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LMUMemoryReader",
        "startup.log");

    public static void SetLogDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        _logPath = Path.Combine(directory, "startup.log");
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception? exception) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var writer = new StreamWriter(_logPath, append: true);
            writer.WriteLine($"[{DateTime.UtcNow:O}] {level}: {message}");
            if (exception != null)
            {
                writer.WriteLine(exception);
            }
        }
        catch
        {
            // Ignore logging failures.
        }
    }
}
