using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PitWall.Models.Telemetry;

namespace PitWall.Telemetry
{
    /// <summary>
    /// Interface for importing telemetry from iRacing IBT files
    /// Supports:
    /// - Auto-detection of Documents/iRacing/telemetry/ folder
    /// - Binary IBT file parsing using iRSDKSharp
    /// - All 60Hz telemetry channels
    /// - Session metadata extraction
    /// - Lap-level aggregation
    /// </summary>
    public interface ITelemetryImporter
    {
        /// <summary>
        /// Detects iRacing telemetry folder location
        /// Returns Documents/iRacing/telemetry or user-configured path
        /// </summary>
        Task<string> GetTelemetryFolderAsync();

        /// <summary>
        /// Scans telemetry folder for .ibt files
        /// Returns list of available IBT files with metadata
        /// </summary>
        Task<List<IBTFileInfo>> ScanTelemetryFolderAsync(string folderPath);

        /// <summary>
        /// Imports a single IBT file
        /// Parses header (driver, car, track, session type)
        /// Extracts all 60Hz samples
        /// Calculates lap-level aggregates
        /// </summary>
        Task<ImportedSession> ImportIBTFileAsync(string filePath);
    }

    /// <summary>
    /// Information about an IBT file (from file system)
    /// </summary>
    public class IBTFileInfo
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public DateTime FileDate { get; set; }
        public long FileSizeBytes { get; set; }
    }

    /// <summary>
    /// Result of importing a single IBT file
    /// Contains session metadata, lap aggregates, and all 60Hz samples
    /// </summary>
    public class ImportedSession
    {
        public SessionMetadata SessionMetadata { get; set; } = new();
        public List<LapMetadata> Laps { get; set; } = new();
        public List<TelemetrySample> RawSamples { get; set; } = new();
        public string SourceFilePath { get; set; } = "";
        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    }
}
