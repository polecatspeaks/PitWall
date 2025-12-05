namespace PitWall.Models
{
    /// <summary>
    /// Telemetry data snapshot from the simulator
    /// </summary>
    public class Telemetry
    {
        public double FuelRemaining { get; set; }
        public double FuelCapacity { get; set; }
        public double LastLapTime { get; set; }
        public double BestLapTime { get; set; }
        public int CurrentLap { get; set; }
        public bool IsInPit { get; set; }
        public bool IsLapValid { get; set; }
        public string TrackName { get; set; } = string.Empty;
        public string CarName { get; set; } = string.Empty;
        public bool IsGameRunning { get; set; }
    }
}
