using System;
using System.Collections.Generic;
using System.Linq;
using PitWall.Models.Telemetry;

namespace PitWall.Profile
{
    /// <summary>
    /// Calculates multi-factor confidence score for profiles
    /// Confidence measures reliability of aggregated statistics
    /// 
    /// Factors:
    /// 1. Recency (weight): Recent data more important via exponential decay
    /// 2. Sample size: More laps = higher confidence (logarithmic scale)
    /// 3. Consistency: Low standard deviation = higher confidence
    /// 4. Session count: More sessions = higher confidence (up to saturation)
    /// 
    /// Final confidence = recency_factor * sample_factor * consistency_factor * session_factor
    /// Each factor ranges 0.0 to 1.0
    /// </summary>
    public class ConfidenceCalculator
    {
        private readonly RecencyWeightCalculator _recencyCalculator = new();

        /// <summary>
        /// Calculates overall confidence for a track profile
        /// Combines multiple quality indicators
        /// </summary>
        public float CalculateConfidence(
            List<SessionMetadata> sessions,
            List<LapMetadata> laps,
            float lapTimeStdDev)
        {
            if (sessions.Count == 0 || laps.Count == 0)
                return 0.0f;

            // Factor 1: Recency (0.0-1.0)
            // Average weight of all sessions
            double avgRecencyWeight = sessions
                .Select(s => _recencyCalculator.CalculateWeight(s.SessionDate))
                .Average();
            float recencyFactor = (float)avgRecencyWeight;

            // Factor 2: Sample size (0.0-1.0)
            // Logarithmic scale: 0 laps = 0.0, 10 laps = 0.5, 100 laps = 0.85, 500+ = 1.0
            int totalLaps = laps.Count;
            float sampleFactor = (float)Math.Min(1.0, Math.Log10(totalLaps + 1) / 3.0);

            // Factor 3: Consistency (0.0-1.0)
            // Lower stddev = higher confidence
            // Below 0.5s stddev = excellent (1.0)
            // 0.5-2.0s stddev = good to acceptable
            // Above 2.0s stddev = poor (scales down)
            float consistencyFactor = lapTimeStdDev < 0.5f
                ? 1.0f
                : Math.Max(0.0f, 1.0f - (lapTimeStdDev - 0.5f) / 2.0f);

            // Factor 4: Session count (0.0-1.0)
            // 1 session = 0.3, 5 sessions = 0.7, 10+ = 1.0
            float sessionFactor = Math.Min(1.0f, 0.3f + (sessions.Count * 0.07f));

            // Combine all factors (multiplicative)
            // This ensures ANY weak factor brings down overall confidence
            float confidence = recencyFactor * sampleFactor * consistencyFactor * sessionFactor;

            return Math.Max(0.0f, Math.Min(1.0f, confidence));
        }

        /// <summary>
        /// Calculates confidence for aggregated driver profile
        /// Based on number of car/track combinations with good confidence
        /// </summary>
        public float CalculateDriverConfidence(List<float> trackConfidences)
        {
            if (trackConfidences.Count == 0)
                return 0.0f;

            // Average confidence across all tracks
            float avgConfidence = trackConfidences.Average();

            // Bonus if many high-confidence profiles
            int highConfidenceCount = trackConfidences.Count(c => c > 0.7f);
            float diversityBonus = Math.Min(0.2f, highConfidenceCount * 0.05f);

            return Math.Min(1.0f, avgConfidence + diversityBonus);
        }
    }
}
