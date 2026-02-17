namespace PitWall.Telemetry.Live.Services
{
    /// <summary>
    /// Current operating mode of the telemetry pipeline.
    /// </summary>
    public enum TelemetryMode
    {
        /// <summary>No data source connected — pipeline is waiting.</summary>
        Idle,

        /// <summary>Reading live data from shared memory (LMU is running).</summary>
        Live,

        /// <summary>Reading replay data from DuckDB (LMU is not running).</summary>
        Replay
    }
}
