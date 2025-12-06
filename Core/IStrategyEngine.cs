using PitWall.Models;

namespace PitWall.Core
{
    /// <summary>
    /// Interface for the strategy engine that generates recommendations
    /// </summary>
    public interface IStrategyEngine
    {
        /// <summary>
        /// Gets a recommendation based on current telemetry
        /// </summary>
        Recommendation GetRecommendation(SimHubTelemetry telemetry);

        /// <summary>
        /// Records a completed lap for strategy analysis
        /// </summary>
        void RecordLap(SimHubTelemetry telemetry);
    }
}
