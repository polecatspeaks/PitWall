using System.Collections.Generic;

namespace PitWall.Models
{
    /// <summary>
    /// Opponent car data for traffic/multi-class awareness
    /// </summary>
    public class OpponentData
    {
        public int Position { get; set; }
        public string CarName { get; set; } = string.Empty;
        public double GapSeconds { get; set; }
        public bool IsInPitLane { get; set; }
        public double BestLapTime { get; set; }
    }
}
