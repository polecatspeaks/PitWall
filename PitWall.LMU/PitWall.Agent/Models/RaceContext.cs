namespace PitWall.Agent.Models
{
    public class RaceContext
    {
        public string TrackName { get; set; } = string.Empty;
        public string CarName { get; set; } = string.Empty;
        public int CurrentLap { get; set; }
        public int TotalLaps { get; set; }
        public int Position { get; set; }

        public double FuelLevel { get; set; }
        public double FuelCapacity { get; set; }

        public double FuelLapsRemaining { get; set; }
        public double AvgFuelPerLap { get; set; }
        public int OptimalPitLap { get; set; }
        public double StrategyConfidence { get; set; }

        public double AverageTireWear { get; set; }
        public int TireLapsOnSet { get; set; }

        public double LastLapTime { get; set; }
        public double BestLapTime { get; set; }

        public double GapToAhead { get; set; }
        public double GapToBehind { get; set; }

        public string CurrentWeather { get; set; } = "Clear";
        public double TrackTemp { get; set; }

        public bool InPitLane { get; set; }
        public bool IsActivelyRacing => !InPitLane && CurrentLap > 0;
    }
}
