using System;
using System.Collections.Generic;
using System.IO;
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
        /// TODO: Implement IRSDKSharper integration for actual parsing
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
                ImportedAt = DateTime.UtcNow,
                SessionMetadata = new SessionMetadata
                {
                    SessionId = Path.GetFileNameWithoutExtension(filePath),
                    SessionDate = File.GetLastWriteTimeUtc(filePath),
                    SourceFilePath = filePath,
                    ProcessedDate = DateTime.UtcNow
                }
            };

            // TODO: Implement IRSDKSharper parsing
            // 1. Load IBT file with IRSDKSharper
            // 2. Extract session metadata (driver, car, track, session type)
            // 3. Extract all 60Hz telemetry samples
            // 4. Calculate lap aggregates
            // 5. Populate session.SessionMetadata, session.RawSamples, session.Laps

            return Task.FromResult(session);
        }
    }
}
