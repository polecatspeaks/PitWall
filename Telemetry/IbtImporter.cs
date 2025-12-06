using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PitWall.Models.Telemetry;

namespace PitWall.Telemetry
{
    /// <summary>
    /// IBT telemetry file importer
    /// Uses IRSDKSharper to parse iRacing binary telemetry files
    /// 
    /// TDD GREEN Phase: Minimal implementation to pass tests
    /// </summary>
    public class IbtImporter : ITelemetryImporter
    {
        private readonly string? _telemetryFolderOverride;

        public IbtImporter(string? overridePath = null)
        {
            _telemetryFolderOverride = overridePath;
        }

        /// <summary>
        /// Returns iRacing telemetry folder path
        /// Default: Documents/iRacing/telemetry/
        /// </summary>
        public Task<string> GetTelemetryFolderAsync()
        {
            // If override specified, return it
            if (!string.IsNullOrEmpty(_telemetryFolderOverride))
            {
                return Task.FromResult(_telemetryFolderOverride!);
            }

            // Try default iRacing telemetry folder
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string defaultPath = Path.Combine(documentsPath, "iRacing", "telemetry");

            return Task.FromResult(defaultPath);
        }

        /// <summary>
        /// Scans folder for .ibt files
        /// Returns list with file metadata
        /// </summary>
        public Task<List<IBTFileInfo>> ScanTelemetryFolderAsync(string folderPath)
        {
            var result = new List<IBTFileInfo>();

            if (!Directory.Exists(folderPath))
            {
                return Task.FromResult(result);
            }

            try
            {
                var files = Directory.GetFiles(folderPath, "*.ibt", SearchOption.AllDirectories);

                foreach (var filePath in files)
                {
                    var fileInfo = new FileInfo(filePath);
                    result.Add(new IBTFileInfo
                    {
                        FilePath = filePath,
                        FileName = fileInfo.Name,
                        FileDate = fileInfo.LastWriteTimeUtc,
                        FileSizeBytes = fileInfo.Length
                    });
                }

                // Sort by date, newest first
                result.Sort((a, b) => b.FileDate.CompareTo(a.FileDate));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scanning folder: {ex.Message}");
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Imports single IBT file
        /// <summary>
        /// Imports single IBT file
        /// Extracts session metadata using IbtFileReader
        /// </summary>
        public Task<ImportedSession> ImportIBTFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"IBT file not found: {filePath}");
            }

            var session = new ImportedSession
            {
                SourceFilePath = filePath,
                ImportedAt = DateTime.UtcNow
            };

            try
            {
                // Use IbtFileReader to parse binary IBT file
                using var reader = new IbtFileReader(filePath);

                // Parse session info YAML
                var sessionInfo = reader.ParseSessionInfo();

                // Extract metadata from session info
                session.SessionMetadata = ExtractSessionMetadata(sessionInfo, filePath);

                // Extract 60Hz telemetry samples
                session.RawSamples = reader.ReadTelemetrySamples();

                // Calculate lap-level aggregates
                session.Laps = CalculateLapAggregates(session.RawSamples);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error importing IBT file: {ex.Message}");
                throw;
            }

            return Task.FromResult(session);
        }

        /// <summary>
        /// Extracts session metadata from parsed IBT session info
        /// Navigates nested YAML structure to find driver, car, track, session type
        /// </summary>
        private SessionMetadata ExtractSessionMetadata(Dictionary<string, object> sessionInfo, string filePath)
        {
            var metadata = new SessionMetadata
            {
                SessionId = Path.GetFileNameWithoutExtension(filePath),
                SessionDate = File.GetLastWriteTimeUtc(filePath),
                SourceFilePath = filePath,
                ProcessedDate = DateTime.UtcNow
            };

            try
            {
                // Extract WeekendInfo
                if (sessionInfo.TryGetValue("WeekendInfo", out var weekendInfoObj) &&
                    weekendInfoObj is Dictionary<object, object> weekendInfo)
                {
                    if (weekendInfo.TryGetValue("TrackDisplayName", out var trackName))
                        metadata.TrackName = trackName?.ToString() ?? "";
                }

                // Extract DriverInfo
                if (sessionInfo.TryGetValue("DriverInfo", out var driverInfoObj) &&
                    driverInfoObj is Dictionary<object, object> driverInfo)
                {
                    // Get driver car index (usually 0 for single player)
                    int driverCarIdx = 0;
                    if (driverInfo.TryGetValue("DriverCarIdx", out var carIdx))
                    {
                        driverCarIdx = Convert.ToInt32(carIdx);
                    }

                    // Get drivers list
                    if (driverInfo.TryGetValue("Drivers", out var driversObj) &&
                        driversObj is List<object> drivers && drivers.Count > driverCarIdx)
                    {
                        if (drivers[driverCarIdx] is Dictionary<object, object> driver)
                        {
                            if (driver.TryGetValue("UserName", out var userName))
                                metadata.DriverName = userName?.ToString() ?? "";
                            if (driver.TryGetValue("CarScreenName", out var carName))
                                metadata.CarName = carName?.ToString() ?? "";
                        }
                    }
                }

                // Extract SessionInfo
                if (sessionInfo.TryGetValue("SessionInfo", out var sessionInfoObj) &&
                    sessionInfoObj is Dictionary<object, object> sessionInfoDict)
                {
                    if (sessionInfoDict.TryGetValue("Sessions", out var sessionsObj) &&
                        sessionsObj is List<object> sessions && sessions.Count > 0)
                    {
                        // Get first session (typically the race session)
                        if (sessions[0] is Dictionary<object, object> firstSession)
                        {
                            if (firstSession.TryGetValue("SessionType", out var sessionType))
                                metadata.SessionType = sessionType?.ToString() ?? "Unknown";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting metadata: {ex.Message}");
            }

            return metadata;
        }

        /// <summary>
        /// Calculates lap-level aggregates from raw 60Hz samples
        /// Groups samples by lap number and computes fuel, time, speed statistics
        /// </summary>
        private List<LapMetadata> CalculateLapAggregates(List<TelemetrySample> samples)
        {
            var laps = new List<LapMetadata>();

            if (samples == null || samples.Count == 0)
            {
                return laps;
            }

            // Group samples by lap number
            var lapGroups = samples
                .Where(s => s.LapNumber > 0) // Exclude lap 0 (out lap/formation)
                .GroupBy(s => s.LapNumber)
                .OrderBy(g => g.Key);

            foreach (var lapGroup in lapGroups)
            {
                var lapSamples = lapGroup.ToList();

                if (lapSamples.Count < 10) // Skip laps with too few samples
                {
                    continue;
                }

                var lap = new LapMetadata
                {
                    LapNumber = lapGroup.Key,
                    IsValid = true, // Assume valid unless we detect issues
                    IsClear = true  // Assume clear unless we detect incidents
                };

                // Calculate fuel usage
                var fuelLevels = lapSamples.Select(s => s.FuelLevel).Where(f => f > 0).ToList();
                if (fuelLevels.Count > 1)
                {
                    lap.FuelUsed = fuelLevels.First() - fuelLevels.Last();
                    lap.FuelRemaining = fuelLevels.Last();
                }

                // Calculate lap time (approximate from sample count at 60Hz)
                lap.LapTime = TimeSpan.FromSeconds(lapSamples.Count / 60.0);

                // Calculate speed statistics
                var speeds = lapSamples.Select(s => s.Speed).Where(s => s > 0).ToList();
                if (speeds.Count > 0)
                {
                    lap.AvgSpeed = speeds.Average();
                    lap.MaxSpeed = speeds.Max();
                }

                // Calculate input averages
                lap.AvgThrottle = lapSamples.Average(s => s.Throttle);
                lap.AvgBrake = lapSamples.Average(s => s.Brake);
                lap.AvgSteeringAngle = lapSamples.Average(s => s.SteeringAngle);

                // Calculate engine statistics
                var rpms = lapSamples.Select(s => s.EngineRpm).Where(r => r > 0).ToList();
                if (rpms.Count > 0)
                {
                    lap.AvgEngineRpm = (int)rpms.Average();
                }

                var temps = lapSamples.Select(s => s.EngineTemp).Where(t => t > 0).ToList();
                if (temps.Count > 0)
                {
                    lap.AvgEngineTemp = temps.Average();
                }

                laps.Add(lap);
            }

            return laps;
        }
    }
}

