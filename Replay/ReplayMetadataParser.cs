using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace PitWall.Replay
{
    /// <summary>
    /// Parses iRacing replay files (.rpy) to extract metadata
    /// </summary>
    public class ReplayMetadataParser
    {
        private static readonly Regex DateStampedPattern = new Regex(
            @"^(\d{4})_(\d{2})_(\d{2})_(\d{2})_(\d{2})_(\d{2})$",
            RegexOptions.Compiled
        );

        private static readonly Regex SubsessionPattern = new Regex(
            @"^subses(\d+)$",
            RegexOptions.Compiled
        );

        /// <summary>
        /// Extract session date from replay filename
        /// Supports two patterns:
        /// 1. Date-stamped: 2025_11_08_09_58_17.rpy
        /// 2. Subsession: subses80974445.rpy (requires YAML parsing)
        /// </summary>
        public DateTime ExtractSessionDate(string filePath)
        {
            var filename = Path.GetFileNameWithoutExtension(filePath);

            // Pattern 1: Date-stamped filename
            var dateMatch = DateStampedPattern.Match(filename);
            if (dateMatch.Success)
            {
                int year = int.Parse(dateMatch.Groups[1].Value);
                int month = int.Parse(dateMatch.Groups[2].Value);
                int day = int.Parse(dateMatch.Groups[3].Value);
                int hour = int.Parse(dateMatch.Groups[4].Value);
                int minute = int.Parse(dateMatch.Groups[5].Value);
                int second = int.Parse(dateMatch.Groups[6].Value);

                return new DateTime(year, month, day, hour, minute, second);
            }

            // Pattern 2: Subsession filename - parse from YAML header
            var subsesMatch = SubsessionPattern.Match(filename);
            if (subsesMatch.Success)
            {
                return ParseSessionDateFromYaml(filePath);
            }

            throw new FormatException($"Unknown replay filename format: {filename}");
        }

        /// <summary>
        /// Parse full metadata from iRacing YAML header
        /// Reads only a small portion of the file to avoid walking the entire binary replay
        /// </summary>
        public ReplayMetadata ParseMetadata(string filePath)
        {
            const int maxLines = 500;       // cap header reads to avoid scanning huge files
            const int maxBytes = 1_000_000; // ~1MB safety limit

            var metadata = new ReplayMetadata
            {
                SessionDate = ExtractSessionDate(filePath)
            };

            try
            {
                using var stream = File.OpenRead(filePath);
                using var reader = new StreamReader(stream);

                string? line;
                int linesRead = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    linesRead++;

                    // stop early if we've read enough of the header or hit our byte guardrail
                    if (line.Trim() == "---" || linesRead > maxLines || reader.BaseStream.Position > maxBytes)
                    {
                        break;
                    }

                    // Parse key-value pairs
                    if (line.Contains(":"))
                    {
                        var parts = line.Split(new[] { ':' }, 2);
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string value = parts[1].Trim().Trim('"');

                            switch (key)
                            {
                                case "track_name":
                                case "TrackName":
                                    metadata.TrackName = value;
                                    break;
                                case "car_name":
                                case "CarName":
                                    metadata.CarName = value;
                                    break;
                                case "session_type":
                                case "SessionType":
                                    metadata.SessionType = value;
                                    break;
                                case "session_id":
                                case "SessionId":
                                case "subsession_id":
                                case "SubsessionId":
                                    metadata.SessionId = value;
                                    break;
                                case "session_length":
                                case "SessionLength":
                                    int.TryParse(value, out int length);
                                    metadata.SessionLength = length;
                                    break;
                                case "session_start_time":
                                case "SessionStartTime":
                                    if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime startTime))
                                    {
                                        metadata.SessionDate = startTime;
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to parse replay metadata from {filePath}: {ex.Message}", ex);
            }

            return metadata;
        }

        /// <summary>
        /// Parse session date from YAML header for subsession-named replays
        /// </summary>
        private DateTime ParseSessionDateFromYaml(string filePath)
        {
            const int maxLines = 200;
            const int maxBytes = 512_000; // ~0.5MB guardrail

            try
            {
                using var stream = File.OpenRead(filePath);
                using var reader = new StreamReader(stream);

                string? line;
                int linesRead = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    linesRead++;

                    if (line.Trim() == "---" || linesRead > maxLines || reader.BaseStream.Position > maxBytes)
                    {
                        break;
                    }

                    if (line.Contains(":"))
                    {
                        var parts = line.Split(new[] { ':' }, 2);
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string value = parts[1].Trim().Trim('"');

                            if (key == "session_start_time" || key == "SessionStartTime")
                            {
                                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime startTime))
                                {
                                    return startTime;
                                }
                            }
                        }
                    }
                }

                // Fallback to file creation time if YAML parsing fails
                return File.GetCreationTime(filePath);
            }
            catch
            {
                // Last resort: use file creation time
                return File.GetCreationTime(filePath);
            }
        }
    }
}
