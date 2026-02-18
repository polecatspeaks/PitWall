using PitWall.Telemetry.Live.Models;

namespace PitWall.Agent.Services
{
    /// <summary>
    /// Provides live telemetry snapshot data to the AI agent.
    /// When available, the agent uses live data instead of the request context dictionary.
    /// </summary>
    public interface ILiveTelemetryProvider
    {
        /// <summary>
        /// Gets the latest telemetry snapshot, or null if no live data is available.
        /// </summary>
        TelemetrySnapshot? GetLatestSnapshot();
    }
}
