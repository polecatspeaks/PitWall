using System.Collections.Generic;

namespace PitWall.Models
{
    /// <summary>
    /// Live telemetry data snapshot from SimHub/iRacing
    /// This is the real-time telemetry that SimHub receives from the simulator
    /// </summary>
    public class SimHubTelemetry
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

        // Tyre wear percentages (0-100, where lower means more worn)
        public double TyreWearFrontLeft { get; set; }
        public double TyreWearFrontRight { get; set; }
        public double TyreWearRearLeft { get; set; }
        public double TyreWearRearRight { get; set; }

        // Opponent data for traffic and multi-class awareness
        public List<OpponentData> Opponents { get; set; } = new();
        public int PlayerPosition { get; set; }
    }
}
