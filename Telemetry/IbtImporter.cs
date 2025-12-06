using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PitWall.Models.Telemetry;

namespace PitWall.Telemetry
{
    /// <summary>
    /// Implementation of IBT telemetry importer
    /// Uses iRSDKSharp to parse iRacing binary telemetry files
    /// 
    /// Responsibilities:
    /// 1. Detect iRacing telemetry folder (Documents/iRacing/telemetry/)
    /// 2. Scan folder for .ibt files
    /// 3. Parse IBT headers (session metadata)
    /// 4. Extract ALL 60Hz telemetry samples
    /// 5. Calculate lap-level aggregates
    /// 6. Return ImportedSession with metadata, laps, and samples
    /// </summary>
    public class IbtImporter : ITelemetryImporter
    {
        private readonly string _telemetryFolderOverride;

        public IbtImporter(string? overridePath = null)
        {
            _telemetryFolderOverride = overridePath ?? "";
        }

        /// <summary>
        /// Finds iRacing telemetry folder
        /// Default: {Documents}/iRacing/telemetry/
        /// Can be overridden via constructor or settings
        /// </summary>
        public async Task<string> GetTelemetryFolderAsync()
        {
            if (!string.IsNullOrEmpty(_telemetryFolderOverride))
            {
                return _telemetryFolderOverride;
            }

            // Try default path
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string defaultPath = Path.Combine(documentsPath, "iRacing", "telemetry");

            if (Directory.Exists(defaultPath))
            {
                return defaultPath;
            }

            // TODO: Prompt user to select folder if default doesn't exist
            throw new DirectoryNotFoundException(
                $"iRacing telemetry folder not found at {defaultPath}. " +
                "Please configure the path in settings.");
        }

        /// <summary>
        /// Scans telemetry folder for .ibt files
        /// Returns file info (name, path, date, size)
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
                // Log error but don't throw - allow partial results
                System.Diagnostics.Debug.WriteLine($"Error scanning telemetry folder: {ex.Message}");
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Imports a single IBT file
        /// Parses header and extracts 60Hz samples
        /// 
        /// TODO: Implement using iRSDKSharp:
        /// 1. Open binary IBT file
        /// 2. Read header (variable length, ends with telemetry header)
        /// 3. Extract metadata:
        ///    - DriverName, CarName, TrackName
        ///    - SessionType (Practice, Qualify, Race, etc)
        ///    - SessionDate/time
        /// 4. Read all 60Hz telemetry samples
        ///    - Tick rate determines sample frequency
        ///    - All channel variables from iRSDK
        /// 5. Aggregate into LapMetadata
        /// 6. Return ImportedSession
        /// </summary>
        public Task<ImportedSession> ImportIBTFileAsync(string filePath)
        {
            var session = new ImportedSession
            {
                SourceFilePath = filePath,
                ImportedAt = DateTime.UtcNow
            };

            try
            {
                // TODO: Implement IBT file parsing with iRSDKSharp
                // For now, return empty but properly structured ImportedSession
                
                session.SessionMetadata = new SessionMetadata
                {
                    SessionId = Path.GetFileNameWithoutExtension(filePath),
                    SessionDate = File.GetLastWriteTimeUtc(filePath),
                    SourceFilePath = filePath,
                    ProcessedDate = DateTime.UtcNow
                };

                // TODO: When iRSDKSharp integrated:
                // 1. Parse IBT header for metadata
                // 2. Extract all 60Hz samples into RawSamples list
                // 3. Aggregate RawSamples into Laps list
                // 4. Calculate per-lap statistics

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error importing IBT file {filePath}: {ex.Message}");
                throw;
            }

            return Task.FromResult(session);
        }
    }
}
