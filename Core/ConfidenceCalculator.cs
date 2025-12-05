using System;
using System.Collections.Generic;
using System.Linq;

namespace PitWall.Core
{
    /// <summary>
    /// Calculates confidence scores for driver profiles
    /// Based on recency, sample size, consistency, and session count
    /// </summary>
    public class ConfidenceCalculator
    {
        private const double STALE_THRESHOLD_DAYS = 180.0;

        /// <summary>
        /// Calculate confidence score (0.0 to 1.0) for a profile
        /// 
        /// Factors:
        /// - Recency (40%): How recent is the last session?
        /// - Sample size (30%): How many laps have been recorded?
        /// - Consistency (20%): How consistent is the data?
        /// - Session count (10%): How many sessions contributed?
        /// </summary>
        public double Calculate(IEnumerable<(DateTime Date, int LapCount, double FuelPerLap)> sessions, DateTime now)
        {
            var sessionList = sessions.ToList();

            if (sessionList.Count == 0)
            {
                return 0.0;
            }

            // Factor 1: Recency (40% weight)
            var daysSinceLastSession = (now - sessionList.Max(s => s.Date)).TotalDays;
            var recencyScore = Math.Exp(-daysSinceLastSession / 60.0);

            // Factor 2: Sample size (30% weight)
            var totalLaps = sessionList.Sum(s => s.LapCount);
            var sampleScore = Math.Min(1.0, totalLaps / 100.0);

            // Factor 3: Consistency (20% weight)
            var fuelValues = sessionList.Select(s => s.FuelPerLap).ToList();
            var mean = fuelValues.Average();
            var stdDev = Math.Sqrt(fuelValues.Average(v => Math.Pow(v - mean, 2)));
            var coefficientOfVariation = mean > 0 ? stdDev / mean : 1.0;
            var consistencyScore = Math.Exp(-coefficientOfVariation * 5);

            // Factor 4: Session count (10% weight)
            var sessionScore = Math.Min(1.0, sessionList.Count / 10.0);

            // Weighted combination
            var confidence =
                (recencyScore * 0.4) +
                (sampleScore * 0.3) +
                (consistencyScore * 0.2) +
                (sessionScore * 0.1);

            return Math.Round(confidence, 2);
        }

        /// <summary>
        /// Check if profile data is stale (>180 days since last session)
        /// </summary>
        public bool IsStale(DateTime lastSessionDate, DateTime now)
        {
            var daysSinceLastSession = (now - lastSessionDate).TotalDays;
            return daysSinceLastSession > STALE_THRESHOLD_DAYS;
        }

        /// <summary>
        /// Get human-readable confidence description
        /// </summary>
        public string GetConfidenceDescription(double confidence)
        {
            if (confidence >= 0.80) return "Excellent";
            if (confidence >= 0.60) return "Good";
            if (confidence >= 0.40) return "Fair";
            if (confidence >= 0.20) return "Low";
            return "Very Low";
        }
    }
}
