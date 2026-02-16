using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Telemetry.Live.Models;

namespace PitWall.Telemetry.Live.Services
{
    /// <summary>
    /// Reads live telemetry data from LMU shared memory.
    /// Implements TDD approach - this is the minimal implementation to make tests pass.
    /// </summary>
    public class LiveTelemetryReader
    {
        private readonly ITelemetryDataSource _dataSource;
        private readonly ILogger<LiveTelemetryReader> _logger;

        /// <summary>
        /// Create a LiveTelemetryReader with a custom data source (for testing)
        /// </summary>
        public LiveTelemetryReader(ITelemetryDataSource dataSource, ILogger<LiveTelemetryReader>? logger = null)
        {
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            _logger = logger ?? NullLogger<LiveTelemetryReader>.Instance;
        }

        /// <summary>
        /// Read a single telemetry snapshot.
        /// Returns null if data source is not available or an error occurs.
        /// </summary>
        public async Task<TelemetrySnapshot?> ReadAsync()
        {
            try
            {
                // Check if data source is available
                if (!_dataSource.IsAvailable())
                {
                    _logger.LogDebug("Telemetry data source is not available");
                    return null;
                }

                // Read snapshot from source
                var snapshot = await _dataSource.ReadSnapshotAsync();
                
                if (snapshot == null)
                {
                    _logger.LogWarning("Data source returned null snapshot");
                    return null;
                }

                // Ensure snapshot has required fields
                if (snapshot.Timestamp == default)
                {
                    snapshot.Timestamp = DateTime.UtcNow;
                }

                if (string.IsNullOrEmpty(snapshot.SessionId))
                {
                    snapshot.SessionId = Guid.NewGuid().ToString();
                }

                _logger.LogDebug("Successfully read telemetry snapshot at {Timestamp}", snapshot.Timestamp);
                return snapshot;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading telemetry snapshot");
                return null;
            }
        }
    }
}
