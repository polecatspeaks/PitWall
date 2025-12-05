using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PitWall.Core;
using PitWall.Models;
using PitWall.Storage;

namespace PitWall.Replay
{
    /// <summary>
    /// Processes iRacing replay files to seed profile database
    /// Scans folder, sorts chronologically, extracts telemetry, and builds profiles
    /// </summary>
    public class ReplayProcessor
    {
        private readonly ReplayMetadataParser _metadataParser;
        private readonly ProfileAnalyzer _profileAnalyzer;
        private readonly RecencyWeightCalculator _weightCalculator;
        private readonly ConfidenceCalculator _confidenceCalculator;
        private readonly SQLiteProfileDatabase _database;
        private const int MinReplayDurationSeconds = 600; // 10 minutes

        public event EventHandler<ReplayProcessingProgressEventArgs>? ProgressChanged;
        public event EventHandler<ReplayProcessingCompleteEventArgs>? ProcessingComplete;

        public ReplayProcessor(SQLiteProfileDatabase database)
        {
            _metadataParser = new ReplayMetadataParser();
            _profileAnalyzer = new ProfileAnalyzer();
            _weightCalculator = new RecencyWeightCalculator();
            _confidenceCalculator = new ConfidenceCalculator();
            _database = database;
        }

        /// <summary>
        /// Scan replay folder and return discovered replay files sorted chronologically
        /// </summary>
        public List<ReplayFileInfo> ScanReplayFolder(string replayFolder)
        {
            if (!Directory.Exists(replayFolder))
            {
                throw new DirectoryNotFoundException($"Replay folder not found: {replayFolder}");
            }

            var files = Directory.GetFiles(replayFolder, "*.rpy", SearchOption.TopDirectoryOnly);
            
            var replays = new List<ReplayFileInfo>();
            foreach (var file in files)
            {
                try
                {
                    var sessionDate = _metadataParser.ExtractSessionDate(file);
                    replays.Add(new ReplayFileInfo
                    {
                        FilePath = file,
                        SessionDate = sessionDate,
                        FileSize = new FileInfo(file).Length
                    });
                }
                catch
                {
                    // Skip files that can't be parsed
                    continue;
                }
            }

            // CRITICAL: Sort chronologically (oldest first)
            return replays.OrderBy(r => r.SessionDate).ToList();
        }

        /// <summary>
        /// Process replay library in chronological order
        /// Runs on background thread to avoid blocking SimHub
        /// </summary>
        public async Task ProcessReplayLibraryAsync(
            string replayFolder, 
            string driverName,
            CancellationToken cancellationToken = default)
        {
            var replays = ScanReplayFolder(replayFolder);

            if (replays.Count == 0)
            {
                OnProcessingComplete(new ReplayProcessingCompleteEventArgs
                {
                    Success = true,
                    ProfilesCreated = 0,
                    ReplaysProcessed = 0,
                    Message = "No replay files found"
                });
                return;
            }

            // Notify UI we’ve scanned the folder and have a work count
            OnProgressChanged(new ReplayProcessingProgressEventArgs
            {
                CurrentFile = $"Found {replays.Count} replays...",
                CurrentIndex = 0,
                TotalFiles = replays.Count,
                SessionDate = replays.First().SessionDate
            });

            int processed = 0;
            int skipped = 0;
            var trackCarCombos = new HashSet<(string track, string car)>();

            foreach (var replay in replays)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    OnProgressChanged(new ReplayProcessingProgressEventArgs
                    {
                        CurrentFile = Path.GetFileName(replay.FilePath),
                        CurrentIndex = processed + skipped + 1,
                        TotalFiles = replays.Count,
                        SessionDate = replay.SessionDate
                    });

                    var metadata = _metadataParser.ParseMetadata(replay.FilePath);

                    // Skip junk/short replays (<10 minutes)
                    if (metadata.SessionLength > 0 && metadata.SessionLength < MinReplayDurationSeconds)
                    {
                        skipped++;
                        continue;
                    }
                    
                    // For now, we can't extract full telemetry from .rpy files without iRacing SDK
                    // Instead, store minimal metadata for future enhancement
                    // In Phase 5A, this will be extended to extract lap data
                    
                    // Store time-series entry
                    await _database.StoreTimeSeriesSession(
                        driver: driverName,
                        track: metadata.TrackName,
                        car: metadata.CarName,
                        sessionDate: metadata.SessionDate,
                        sessionId: metadata.SessionId,
                        sessionType: metadata.SessionType,
                        lapCount: 0, // TODO: Extract from telemetry
                        fuelPerLap: 0.0, // TODO: Extract from telemetry
                        avgLapTime: 0.0, // TODO: Extract from telemetry
                        lapTimeStdDev: 0.0, // TODO: Extract from telemetry
                        replayFilePath: replay.FilePath
                    );

                    trackCarCombos.Add((metadata.TrackName, metadata.CarName));
                    processed++;
                }
                catch (Exception)
                {
                    skipped++;
                    // Continue processing other replays
                }

                // Small delay to avoid excessive CPU usage
                await Task.Delay(10, cancellationToken);
            }

            // Generate weighted profiles from time-series data
            await RegenerateProfilesAsync(driverName, trackCarCombos);

            OnProcessingComplete(new ReplayProcessingCompleteEventArgs
            {
                Success = true,
                ProfilesCreated = trackCarCombos.Count,
                ReplaysProcessed = processed,
                ReplaysSkipped = skipped,
                Message = $"Processed {processed} replays, created {trackCarCombos.Count} profiles"
            });
        }

        /// <summary>
        /// Regenerate weighted profiles from time-series data
        /// Uses recency weighting and confidence scoring
        /// </summary>
        private async Task RegenerateProfilesAsync(
            string driverName, 
            IEnumerable<(string track, string car)> combinations)
        {
            var now = DateTime.Now;

            foreach (var (track, car) in combinations)
            {
                var timeSeries = await _database.GetTimeSeries(driverName, track, car);

                if (timeSeries.Count == 0)
                {
                    continue;
                }

                // Calculate weighted averages using recency weighting
                var fuelSessions = timeSeries.Select(s => (s.Date, s.FuelPerLap)).ToList();
                var tyreSessions = timeSeries.Select(s => (s.Date, s.TyreDeg)).ToList();

                double weightedFuel = _weightCalculator.CalculateWeightedAverageFuel(fuelSessions, now);
                double weightedTyres = _weightCalculator.CalculateWeightedAverageTyres(tyreSessions, now);

                // Calculate confidence score
                var confidenceData = timeSeries.Select(s => (s.Date, s.LapCount, s.FuelPerLap)).ToList();
                double confidence = _confidenceCalculator.Calculate(confidenceData, now);

                // Check if data is stale
                var lastSessionDate = timeSeries.Max(s => s.Date);
                bool isStale = _confidenceCalculator.IsStale(lastSessionDate, now);

                // Create or update profile
                var profile = new DriverProfile
                {
                    DriverName = driverName,
                    TrackName = track,
                    CarName = car,
                    AverageFuelPerLap = weightedFuel,
                    TypicalTyreDegradation = weightedTyres,
                    Style = DrivingStyle.Unknown, // Can't determine from metadata alone
                    SessionsCompleted = timeSeries.Count,
                    LastUpdated = now,
                    Confidence = confidence,
                    IsStale = isStale,
                    LastSessionDate = lastSessionDate
                };

                await _database.SaveProfile(profile);
            }
        }

        protected virtual void OnProgressChanged(ReplayProcessingProgressEventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }

        protected virtual void OnProcessingComplete(ReplayProcessingCompleteEventArgs e)
        {
            ProcessingComplete?.Invoke(this, e);
        }
    }

    public class ReplayProcessingProgressEventArgs : EventArgs
    {
        public string CurrentFile { get; set; } = string.Empty;
        public int CurrentIndex { get; set; }
        public int TotalFiles { get; set; }
        public DateTime SessionDate { get; set; }
    }

    public class ReplayProcessingCompleteEventArgs : EventArgs
    {
        public bool Success { get; set; }
        public int ProfilesCreated { get; set; }
        public int ReplaysProcessed { get; set; }
        public int ReplaysSkipped { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
