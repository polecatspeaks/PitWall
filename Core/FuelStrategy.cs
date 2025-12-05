using System;
using System.Collections.Generic;
using System.Linq;

namespace PitWall.Core
{
    /// <summary>
    /// Tracks fuel consumption across laps and predicts remaining laps based on usage patterns.
    /// </summary>
    public class FuelStrategy
    {
        private readonly List<LapFuelData> _lapData;

        public FuelStrategy()
        {
            _lapData = new List<LapFuelData>();
        }

        /// <summary>
        /// Calculates fuel used during a stint.
        /// </summary>
        public double CalculateFuelUsed(double startFuel, double endFuel)
        {
            return startFuel - endFuel;
        }

        /// <summary>
        /// Records fuel data for a completed lap.
        /// </summary>
        public void RecordLap(int lapNumber, double startFuel, double endFuel)
        {
            double fuelUsed = CalculateFuelUsed(startFuel, endFuel);
            _lapData.Add(new LapFuelData
            {
                LapNumber = lapNumber,
                StartFuel = startFuel,
                EndFuel = endFuel,
                FuelUsed = fuelUsed
            });
        }

        /// <summary>
        /// Gets the total number of recorded laps.
        /// </summary>
        public int GetLapCount()
        {
            return _lapData.Count;
        }

        /// <summary>
        /// Gets the fuel used on a specific lap.
        /// </summary>
        public double GetFuelUsedOnLap(int lapNumber)
        {
            var lap = _lapData.FirstOrDefault(l => l.LapNumber == lapNumber);
            return lap?.FuelUsed ?? 0.0;
        }

        /// <summary>
        /// Calculates the average fuel consumption per lap across all recorded laps.
        /// </summary>
        public double GetAverageFuelPerLap()
        {
            if (_lapData.Count == 0)
                return 0.0;

            return _lapData.Average(l => l.FuelUsed);
        }

        /// <summary>
        /// Predicts how many full laps can be completed with the current fuel level.
        /// </summary>
        public int PredictLapsRemaining(double currentFuel)
        {
            double averageFuelPerLap = GetAverageFuelPerLap();
            
            if (averageFuelPerLap <= 0.0)
                return 0;

            return (int)Math.Floor(currentFuel / averageFuelPerLap);
        }

        /// <summary>
        /// Clears all recorded lap data.
        /// </summary>
        public void Reset()
        {
            _lapData.Clear();
        }

        private class LapFuelData
        {
            public int LapNumber { get; set; }
            public double StartFuel { get; set; }
            public double EndFuel { get; set; }
            public double FuelUsed { get; set; }
        }
    }
}
