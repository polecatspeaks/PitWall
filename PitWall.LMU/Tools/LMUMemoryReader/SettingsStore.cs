using System;
using System.IO;
using System.Text.Json;

namespace LMUMemoryReader;

public sealed class AppSettings
{
    public string OutputDirectory { get; set; } = string.Empty;
    public string LmuInstallPath { get; set; } = string.Empty;
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        TypeInfoResolver = TelemetryJsonContext.Default
    };

    public static string GetSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "LMUMemoryReader", "settings.json");
    }

    public static AppSettings Load()
    {
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var path = GetSettingsPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, Options);
            File.WriteAllText(path, json);
        }
        catch
        {
            // Swallow settings errors to avoid blocking the UI on exit.
        }
    }
}
