using PitWall.Telemetry.Live.Models;
using PitWall.Telemetry.Live.Services;

namespace PitWall.Agent.Services
{
    /// <summary>
    /// Adapts the live TelemetryPipelineService into the ILiveTelemetryProvider
    /// interface for the AI agent. For in-process use when Agent is co-hosted with the API.
    /// </summary>
    public class LiveTelemetryProviderAdapter : ILiveTelemetryProvider
    {
        private readonly TelemetryPipelineService _pipeline;

        public LiveTelemetryProviderAdapter(TelemetryPipelineService pipeline)
        {
            _pipeline = pipeline ?? throw new System.ArgumentNullException(nameof(pipeline));
        }

        public TelemetrySnapshot? GetLatestSnapshot()
        {
            return _pipeline.LatestSnapshot;
        }
    }
}
