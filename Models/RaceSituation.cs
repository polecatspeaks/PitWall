using System;

namespace PitWall.Models
{
    /// <summary>
    /// Current race situation for undercut/overcut analysis
    /// </summary>
    public class RaceSituation
    {
        public double GapToCarAhead { get; set; }
        public double GapToCarBehind { get; set; }
        public double PitStopDuration { get; set; }
        public int CurrentTyreLaps { get; set; }
        public double FreshTyreAdvantage { get; set; } // seconds per lap
        public int CurrentPosition { get; set; }
        public int OpponentTyreAge { get; set; } // laps on current tyres for car ahead
    }
}
