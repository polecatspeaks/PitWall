using System.Threading.Tasks;
using PitWall.Telemetry.Live.Models;

namespace PitWall.Telemetry.Live.Services
{
    /// <summary>
    /// Interface for telemetry data sources (shared memory, mock, replay, etc.)
    /// </summary>
    public interface ITelemetryDataSource
    {
        /// <summary>
        /// Check if the telemetry source is available (e.g., game is running)
        /// </summary>
        bool IsAvailable();

        /// <summary>
        /// Read a single telemetry snapshot from the source
        /// </summary>
        Task<TelemetrySnapshot?> ReadSnapshotAsync();
    }
}
