using System;
using System.Collections.Generic;

namespace PitWall.Profile
{
    /// <summary>
    /// Calculates recency weight using 90-day exponential decay
    /// Formula: weight = exp(-ln(2) * days_since_session / 90)
    /// 
    /// Properties:
    /// - At day 0: weight = 1.0 (full importance)
    /// - At day 90: weight = 0.5 (half-life)
    /// - At day 180: weight = 0.25 (two half-lives)
    /// - Asymptotically approaches 0 but never reaches it
    /// </summary>
    public class RecencyWeightCalculator
    {
        private const double HALF_LIFE_DAYS = 90.0;

        /// <summary>
        /// Calculates weight for a session based on age
        /// </summary>
        public double CalculateWeight(DateTime sessionDate)
        {
            var daysSince = (DateTime.UtcNow - sessionDate.ToUniversalTime()).TotalDays;
            if (daysSince < 0) daysSince = 0; // Future dates get full weight

            // weight = exp(-ln(2) * daysSince / HALF_LIFE_DAYS)
            // Simplifies to: weight = 2^(-daysSince / HALF_LIFE_DAYS)
            return Math.Exp(-Math.Log(2) * daysSince / HALF_LIFE_DAYS);
        }

        /// <summary>
        /// Calculates weighted average fuel consumption
        /// </summary>
        public double CalculateWeightedAverage(List<(DateTime date, double value)> measurements)
        {
            if (measurements.Count == 0) return 0.0;

            double totalWeightedValue = 0.0;
            double totalWeight = 0.0;

            foreach (var (date, value) in measurements)
            {
                double weight = CalculateWeight(date);
                totalWeightedValue += value * weight;
                totalWeight += weight;
            }

            return totalWeight > 0 ? totalWeightedValue / totalWeight : 0.0;
        }
    }
}
