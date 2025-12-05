using System;
using System.Collections.Generic;
using System.Linq;
using PitWall.Models;

namespace PitWall.Core
{
    /// <summary>
    /// Analyzes opponent traffic patterns for multi-class racing awareness.
    /// </summary>
    public class TrafficAnalyzer
    {
        private const double ClassThresholdSeconds = 2.0;
        private const double UnsafeGapThresholdSeconds = 5.0;

        /// <summary>
        /// Classifies an opponent based on lap time delta.
        /// </summary>
        public TrafficClass ClassifyOpponent(double playerBestLap, double opponentBestLap)
        {
            double delta = opponentBestLap - playerBestLap;

            if (delta < -ClassThresholdSeconds)
            {
                return TrafficClass.FasterClass;
            }
            else if (delta > ClassThresholdSeconds)
            {
                return TrafficClass.SlowerClass;
            }
            else
            {
                return TrafficClass.SameClass;
            }
        }

        /// <summary>
        /// Determines if pit entry is unsafe due to approaching faster class traffic.
        /// </summary>
        public bool IsPitEntryUnsafe(double playerBestLap, IEnumerable<OpponentData> opponents)
        {
            foreach (var opponent in opponents)
            {
                if (opponent.BestLapTime <= 0) continue;

                var classification = ClassifyOpponent(playerBestLap, opponent.BestLapTime);
                if (classification == TrafficClass.FasterClass && opponent.GapSeconds < UnsafeGapThresholdSeconds)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Generates a traffic warning message for pit delay.
        /// </summary>
        public string GetTrafficMessage(double playerBestLap, IEnumerable<OpponentData> opponents)
        {
            var fasterCars = opponents
                .Where(o => o.BestLapTime > 0 && ClassifyOpponent(playerBestLap, o.BestLapTime) == TrafficClass.FasterClass)
                .Where(o => o.GapSeconds < UnsafeGapThresholdSeconds)
                .OrderBy(o => o.GapSeconds)
                .ToList();

            if (fasterCars.Count == 0)
            {
                return string.Empty;
            }

            var nearest = fasterCars.First();
            return $"Wait for faster class: {nearest.CarName} {nearest.GapSeconds:F1}s behind";
        }
    }
}
