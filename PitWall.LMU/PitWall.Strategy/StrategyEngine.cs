using System;
using PitWall.Core.Models;

namespace PitWall.Strategy
{
    public class StrategyEngine
    {
        public string Evaluate(TelemetrySample sample)
        {
            if (sample?.TyreTempsC != null)
            {
                foreach (var t in sample.TyreTempsC)
                {
                    if (t >= 110.0)
                    {
                        return "Tyre overheat: reduce pace / pit soon";
                    }
                }
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
