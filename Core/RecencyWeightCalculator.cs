using System;
using System.Collections.Generic;
using System.Linq;

namespace PitWall.Core
{
    /// <summary>
    /// Calculates recency weights using exponential decay
    /// Recent data is weighted more heavily than old data
    /// </summary>
    public class RecencyWeightCalculator
    {
        private const double HALF_LIFE_DAYS = 90.0;

        /// <summary>
        /// Calculate weight for a session based on its age
        /// Uses exponential decay with 90-day half-life
        /// 
        /// Examples:
        /// - 0 days old:   weight = 1.00 (100%)
        /// - 30 days old:  weight = 0.81 (81%)
        /// - 90 days old:  weight = 0.50 (50%)
        /// - 180 days old: weight = 0.25 (25%)
        /// - 365 days old: weight = 0.06 (6%)
        /// </summary>
        public double CalculateWeight(DateTime sessionDate, DateTime referenceDate)
        {
            var ageInDays = (referenceDate - sessionDate).TotalDays;

            if (ageInDays < 0)
            {
                // Future date - use full weight (shouldn't happen in normal use)
                return 1.0;
            }

            // Exponential decay: weight halves every 90 days
            var weight = Math.Exp(-Math.Log(2) * ageInDays / HALF_LIFE_DAYS);

            return weight;
        }

        /// <summary>
        /// Calculate weighted average of fuel consumption across sessions
        /// Recent sessions weighted more heavily than old sessions
        /// </summary>
        public double CalculateWeightedAverageFuel(IEnumerable<(DateTime Date, double FuelPerLap)> sessions, DateTime now)
        {
            double weightedSum = 0;
            double weightSum = 0;

            foreach (var session in sessions)
            {
                var weight = CalculateWeight(session.Date, now);
                weightedSum += session.FuelPerLap * weight;
                weightSum += weight;
            }

            return weightSum > 0 ? weightedSum / weightSum : 0;
        }

        /// <summary>
        /// Calculate weighted average of tyre degradation across sessions
        /// Recent sessions weighted more heavily than old sessions
        /// </summary>
        public double CalculateWeightedAverageTyres(IEnumerable<(DateTime Date, double TyreDeg)> sessions, DateTime now)
        {
            double weightedSum = 0;
            double weightSum = 0;

            foreach (var session in sessions)
            {
                var weight = CalculateWeight(session.Date, now);
                weightedSum += session.TyreDeg * weight;
                weightSum += weight;
            }

            return weightSum > 0 ? weightedSum / weightSum : 0;
        }
    }
}
