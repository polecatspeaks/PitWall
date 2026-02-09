using System;
using System.Collections.Generic;
using System.Linq;
using PitWall.Models;

namespace PitWall.Core
{
    /// <summary>
    /// Analyzes session data to extract driver behavior patterns
    /// </summary>
    public class ProfileAnalyzer
    {
        private const double SMOOTH_THRESHOLD = 1.0; // Lap time variance < 1 second
        private const double AGGRESSIVE_THRESHOLD = 3.0; // Lap time variance > 3 seconds
        private const int STALE_DAYS = 90;

        public DriverProfile AnalyzeSession(SessionData session)
        {
            var validLaps = session.Laps.Where(l => l.IsValid).ToList();

            if (validLaps.Count == 0)
            {
                throw new InvalidOperationException("No valid laps in session");
            }

            var profile = new DriverProfile
            {
                DriverName = session.DriverName,
                TrackName = session.TrackName,
                CarName = session.CarName,
                AverageFuelPerLap = CalculateAverageFuel(validLaps),
                TypicalTyreDegradation = CalculateAverageTyreDegradation(validLaps),
                Style = IdentifyDrivingStyle(validLaps),
                SessionsCompleted = 1,
                LastUpdated = DateTime.Now,
                Confidence = CalculateConfidence(validLaps.Count),
                IsStale = false,
                LastSessionDate = session.SessionDate
            };

            return profile;
        }

        public DriverProfile MergeProfiles(DriverProfile existing, DriverProfile newProfile)
        {
            int totalSessions = existing.SessionsCompleted + newProfile.SessionsCompleted;

            // Use the most recent session date for freshness
            var lastSessionDate = MaxDate(existing.LastSessionDate, newProfile.LastSessionDate);
            bool isStale = lastSessionDate.HasValue && (DateTime.UtcNow - lastSessionDate.Value).TotalDays > STALE_DAYS;

            // Confidence grows with session count (cap at 1.0)
            double confidence = Math.Min(1.0, totalSessions / 10.0);

            // Weighted average based on session count
            double mergedFuel = (existing.AverageFuelPerLap * existing.SessionsCompleted +
                                 newProfile.AverageFuelPerLap * newProfile.SessionsCompleted) / totalSessions;

            double mergedTyre = (existing.TypicalTyreDegradation * existing.SessionsCompleted +
                                 newProfile.TypicalTyreDegradation * newProfile.SessionsCompleted) / totalSessions;

            return new DriverProfile
            {
                DriverName = existing.DriverName,
                TrackName = existing.TrackName,
                CarName = existing.CarName,
                AverageFuelPerLap = mergedFuel,
                TypicalTyreDegradation = mergedTyre,
                Style = DetermineMergedStyle(existing.Style, newProfile.Style),
                SessionsCompleted = totalSessions,
                LastUpdated = DateTime.Now,
                Confidence = confidence,
                IsStale = isStale,
                LastSessionDate = lastSessionDate
            };
        }

        private static DateTime? MaxDate(DateTime? a, DateTime? b)
        {
            if (!a.HasValue) return b;
            if (!b.HasValue) return a;
            return a.Value >= b.Value ? a : b;
        }

        private double CalculateConfidence(int validLapCount)
        {
            // Simple heuristic: full confidence at 50 valid laps
            return Math.Min(1.0, validLapCount / 50.0);
        }

        private double CalculateAverageFuel(List<LapData> validLaps)
        {
            return validLaps.Average(l => l.FuelUsed);
        }

        private double CalculateAverageTyreDegradation(List<LapData> validLaps)
        {
            if (validLaps.Count < 2)
            {
                return 0.0;
            }

            // Calculate wear rate per lap from tyre wear progression
            var wearDeltas = new List<double>();
            for (int i = 1; i < validLaps.Count; i++)
            {
                double delta = validLaps[i].TyreWearAverage - validLaps[i - 1].TyreWearAverage;
                if (delta > 0)
                {
                    wearDeltas.Add(delta);
                }
            }

            return wearDeltas.Count > 0 ? wearDeltas.Average() : 0.0;
        }

        private DrivingStyle IdentifyDrivingStyle(List<LapData> validLaps)
        {
            if (validLaps.Count < 3)
            {
                return DrivingStyle.Unknown;
            }

            // Calculate lap time variance
            var lapTimes = validLaps.Select(l => l.LapTime.TotalSeconds).ToList();
            double mean = lapTimes.Average();
            double variance = lapTimes.Sum(t => Math.Pow(t - mean, 2)) / lapTimes.Count;
            double stdDev = Math.Sqrt(variance);

            if (stdDev < SMOOTH_THRESHOLD)
            {
                return DrivingStyle.Smooth;
            }
            else if (stdDev > AGGRESSIVE_THRESHOLD)
            {
                return DrivingStyle.Aggressive;
            }
            else
            {
                return DrivingStyle.Mixed;
            }
        }

        private DrivingStyle DetermineMergedStyle(DrivingStyle style1, DrivingStyle style2)
        {
            // If styles match, keep the same
            if (style1 == style2)
            {
                return style1;
            }

            // If one is Unknown, use the other
            if (style1 == DrivingStyle.Unknown)
            {
                return style2;
            }
            if (style2 == DrivingStyle.Unknown)
            {
                return style1;
            }

            // If styles differ, consider it Mixed
            return DrivingStyle.Mixed;
        }
    }
}
