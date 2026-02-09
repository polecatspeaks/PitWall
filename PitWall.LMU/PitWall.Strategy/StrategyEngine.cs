using System;
using PitWall.Core.Models;

namespace PitWall.Strategy
{
    public class StrategyEngine
    {
        private const double TyreOverheatThreshold = 110.0;
        private const double AvgLapFuelConsumption = 1.8; // Liters per lap (configurable)
        private const double CriticalFuelLevel = 2.0; // Liters

        public string Evaluate(TelemetrySample sample)
        {
            if (sample == null)
                return "Invalid sample";

            // Check tyre temperatures
            if (sample.TyreTempsC != null)
            {
                foreach (var t in sample.TyreTempsC)
                {
                    if (t >= TyreOverheatThreshold)
                    {
                        return "Tyre overheat: reduce pace / pit soon";
                    }
                }
            }

            // Check fuel levels
            int lapsRemaining = ProjectLapsRemaining(sample, AvgLapFuelConsumption);
            if (sample.FuelLiters <= CriticalFuelLevel)
            {
                return $"Critical fuel: pit now (~{lapsRemaining} laps remaining)";
            }
            else if (lapsRemaining <= 3)
            {
                return $"Plan pit stop: fuel for ~{lapsRemaining} laps remaining";
            }

            return "No immediate action";
        }

        public int ProjectLapsRemaining(TelemetrySample sample, double avgLapFuelLiters)
        {
            if (sample == null) return 0;
            if (avgLapFuelLiters <= 0) return 0;
            return (int)Math.Floor(sample.FuelLiters / avgLapFuelLiters);
        }
    }
}
