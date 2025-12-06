using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PitWall.Telemetry
{
    /// <summary>
    /// Binary reader for iRacing IBT (Binary Telemetry) files
    /// 
    /// IBT Format Structure:
    /// - Header (144 bytes): Contains version info and offsets to data sections
    /// - Session Info (YAML): Driver, car, track, session metadata
    /// - Variable Headers: Telemetry variable definitions (name, type, offset)
    /// - Telemetry Data: Raw 60Hz sample data
    /// 
    /// Reference: iRacing SDK documentation
    /// </summary>
    public class IbtFileReader : IDisposable
    {
        private readonly string _filePath;
        private BinaryReader? _reader;
        private FileStream? _stream;

        // IBT header offsets
        private const int HEADER_VERSION_OFFSET = 0;
        private const int HEADER_STATUS_OFFSET = 4;
        private const int HEADER_TICK_RATE_OFFSET = 8;
        private const int HEADER_SESSION_INFO_UPDATE_OFFSET = 12;
        private const int HEADER_SESSION_INFO_LENGTH_OFFSET = 16;
        private const int HEADER_SESSION_INFO_OFFSET = 20;
        private const int HEADER_NUM_VARS_OFFSET = 24;
        private const int HEADER_VAR_HEADER_OFFSET = 28;
        private const int HEADER_NUM_BUF_OFFSET = 32;
        private const int HEADER_BUF_LEN_OFFSET = 36;

        public IbtFileReader(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"IBT file not found: {filePath}");
            }
            _filePath = filePath;
        }

        /// <summary>
        /// Reads the session info YAML string from IBT file
        /// Contains driver, car, track, session metadata
        /// </summary>
        public string ReadSessionInfoYaml()
        {
            EnsureStreamOpen();

            // Read session info offset and length from header
            _reader!.BaseStream.Seek(HEADER_SESSION_INFO_OFFSET, SeekOrigin.Begin);
            int sessionInfoOffset = _reader.ReadInt32();

            _reader.BaseStream.Seek(HEADER_SESSION_INFO_LENGTH_OFFSET, SeekOrigin.Begin);
            int sessionInfoLength = _reader.ReadInt32();

            if (sessionInfoLength <= 0 || sessionInfoOffset <= 0)
            {
                return string.Empty;
            }

            // Read session info YAML
            _reader.BaseStream.Seek(sessionInfoOffset, SeekOrigin.Begin);
            byte[] yamlBytes = _reader.ReadBytes(sessionInfoLength);

            // Convert to string (null-terminated)
            string yaml = Encoding.UTF8.GetString(yamlBytes);
            int nullIndex = yaml.IndexOf('\0');
            if (nullIndex >= 0)
            {
                yaml = yaml.Substring(0, nullIndex);
            }

            return yaml;
        }

        /// <summary>
        /// Parses session info YAML into a dictionary structure
        /// Extracts: DriverName, CarName, TrackName, SessionType, etc.
        /// </summary>
        public Dictionary<string, object> ParseSessionInfo()
        {
            string yaml = ReadSessionInfoYaml();

            if (string.IsNullOrEmpty(yaml))
            {
                return new Dictionary<string, object>();
            }

            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                var sessionInfo = deserializer.Deserialize<Dictionary<string, object>>(yaml);
                return sessionInfo ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing session YAML: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Reads telemetry variable definitions
        /// Returns list of variable metadata (name, type, offset in buffer)
        /// </summary>
        public List<TelemetryVariable> ReadVariableHeaders()
        {
            EnsureStreamOpen();

            var variables = new List<TelemetryVariable>();

            // Read number of variables
            _reader!.BaseStream.Seek(HEADER_NUM_VARS_OFFSET, SeekOrigin.Begin);
            int numVars = _reader.ReadInt32();

            // Read variable header offset
            _reader.BaseStream.Seek(HEADER_VAR_HEADER_OFFSET, SeekOrigin.Begin);
            int varHeaderOffset = _reader.ReadInt32();

            if (numVars <= 0 || varHeaderOffset <= 0)
            {
                return variables;
            }

            // Read each variable header (144 bytes each)
            _reader.BaseStream.Seek(varHeaderOffset, SeekOrigin.Begin);

            for (int i = 0; i < numVars; i++)
            {
                var variable = new TelemetryVariable
                {
                    Type = _reader.ReadInt32(),
                    Offset = _reader.ReadInt32(),
                    Count = _reader.ReadInt32()
                };

                // Read variable name (32 bytes, null-terminated)
                byte[] nameBytes = _reader.ReadBytes(32);
                variable.Name = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');

                // Read description (64 bytes, null-terminated)
                byte[] descBytes = _reader.ReadBytes(64);
                variable.Description = Encoding.UTF8.GetString(descBytes).TrimEnd('\0');

                // Read unit (32 bytes, null-terminated)
                byte[] unitBytes = _reader.ReadBytes(32);
                variable.Unit = Encoding.UTF8.GetString(unitBytes).TrimEnd('\0');

                // Skip padding to next 144-byte boundary if needed
                int bytesRead = 4 + 4 + 4 + 32 + 64 + 32; // 140 bytes
                int padding = 144 - bytesRead;
                if (padding > 0)
                {
                    _reader.ReadBytes(padding);
                }

                variables.Add(variable);
            }

            return variables;
        }

        /// <summary>
        /// Reads the tick rate (samples per second)
        /// Typically 60 Hz for iRacing
        /// </summary>
        public int ReadTickRate()
        {
            EnsureStreamOpen();

            _reader!.BaseStream.Seek(HEADER_TICK_RATE_OFFSET, SeekOrigin.Begin);
            return _reader.ReadInt32();
        }

        private void EnsureStreamOpen()
        {
            if (_stream == null)
            {
                _stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _reader = new BinaryReader(_stream);
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _stream?.Dispose();
        }
    }

    /// <summary>
    /// Telemetry variable metadata from IBT file
    /// Describes one telemetry channel (Speed, RPM, FuelLevel, etc.)
    /// </summary>
    public class TelemetryVariable
    {
        public int Type { get; set; }              // Variable type (int, float, double, etc.)
        public int Offset { get; set; }            // Offset in telemetry buffer
        public int Count { get; set; }             // Array length (1 for scalar, >1 for arrays)
        public string Name { get; set; } = "";     // Variable name (e.g., "Speed")
        public string Description { get; set; } = "";  // Human-readable description
        public string Unit { get; set; } = "";     // Unit (e.g., "m/s", "rpm")
    }
}
