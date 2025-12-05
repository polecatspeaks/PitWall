using PitWall.Models;

namespace PitWall.Core
{
    /// <summary>
    /// Interface for providing telemetry data from the simulator
    /// </summary>
    public interface ITelemetryProvider
    {
        /// <summary>
        /// Gets the current telemetry snapshot
        /// </summary>
        Telemetry GetCurrentTelemetry();

        /// <summary>
        /// Gets whether the game is currently running
        /// </summary>
        bool IsGameRunning { get; }
    }
}
