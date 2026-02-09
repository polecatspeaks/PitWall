using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PitWall.Models;
using PitWall.Models.Telemetry;
using PitWall.Storage;
using PitWall.Storage.Telemetry;
using PitWall.Telemetry;

namespace PitWall.Core
{
    /// <summary>
    /// Generates DriverProfile objects from imported telemetry sessions
    /// Bridges Phase 5A (hierarchical telemetry) with the profile system
    /// </summary>
    public class ProfileGenerator
    {
        private readonly ISessionRepository _sessionRepository;
        private readonly IProfileDatabase _profileDatabase;

        public ProfileGenerator(ISessionRepository sessionRepository, IProfileDatabase profileDatabase)
        {
            _sessionRepository = sessionRepository;
            _profileDatabase = profileDatabase;
        }

        /// <summary>
        /// Generate profiles from all imported telemetry sessions
        /// Groups sessions by driver/track/car and creates aggregated profiles
        /// </summary>
        public async Task<int> GenerateProfilesFromImportedSessionsAsync()
        {
            // Get all recent sessions from telemetry database
            var sessions = await _sessionRepository.GetRecentSessionsAsync(1000);

            if (sessions == null || sessions.Count == 0)
            {
                return 0;
            }

            // Group sessions by driver/track/car combination
            var sessionGroups = sessions
                .Where(s => s.SessionMetadata != null && s.Laps != null && s.Laps.Count > 0)
                .GroupBy(s => new
                {
                    Driver = s.SessionMetadata.DriverName ?? "Unknown",
                    Track = s.SessionMetadata.TrackName ?? "Unknown",
                    Car = s.SessionMetadata.CarName ?? "Unknown"
                });

            int profilesCreated = 0;

            foreach (var group in sessionGroups)
            {
                try
                {
                    var profile = GenerateProfileFromSessions(group.ToList());
                    await _profileDatabase.SaveProfile(profile);
                    profilesCreated++;
                }
                catch (Exception ex)
                {
                    // Log error but continue with other profiles
                    Logger.Error($"Failed to generate profile for {group.Key.Driver}/{group.Key.Track}/{group.Key.Car}: {ex.Message}");
                }
            }

            return profilesCreated;
        }

        /// <summary>
        /// Generate a single DriverProfile from multiple sessions of the same driver/track/car
        /// </summary>
        private DriverProfile GenerateProfileFromSessions(List<ImportedSession> sessions)
        {
            var firstSession = sessions.First().SessionMetadata;

            // Calculate average fuel per lap across all sessions
            var allLaps = sessions
                .SelectMany(s => s.Laps ?? new List<LapMetadata>())
                .Where(lap => lap.FuelUsed > 0 && lap.FuelUsed < 50) // Filter outliers
                .ToList();

            double avgFuelPerLap = allLaps.Any()
                ? allLaps.Average(lap => lap.FuelUsed)
                : 0.0;

            // Calculate typical tyre degradation (simplified: lap time increase over stint)
            double tyreDegradation = CalculateTyreDegradation(sessions);

            // Determine driving style from lap time variance and throttle/brake patterns
            var drivingStyle = DetermineDrivingStyle(allLaps);

            // Calculate confidence based on number of laps
            int totalLaps = allLaps.Count;
            double confidence = Math.Min(1.0, totalLaps / 50.0); // Full confidence at 50+ laps

            // Get most recent session date
            var mostRecentSession = sessions
                .OrderByDescending(s => s.SessionMetadata.SessionDate)
                .First();

            // Check if data is stale (older than 90 days)
            bool isStale = (DateTime.UtcNow - mostRecentSession.SessionMetadata.SessionDate).TotalDays > 90;

            return new DriverProfile
            {
                DriverName = firstSession.DriverName,
                TrackName = firstSession.TrackName,
                CarName = firstSession.CarName,
                AverageFuelPerLap = avgFuelPerLap,
                TypicalTyreDegradation = tyreDegradation,
                Style = drivingStyle,
                LastUpdated = DateTime.UtcNow,
                SessionsCompleted = sessions.Count,
                Confidence = confidence,
                IsStale = isStale,
                LastSessionDate = mostRecentSession.SessionMetadata.SessionDate
            };
        }

        /// <summary>
        /// Calculate tyre degradation by analyzing lap time increases within sessions
        /// </summary>
        private double CalculateTyreDegradation(List<ImportedSession> sessions)
        {
            var degradationRates = new List<double>();

            foreach (var session in sessions)
            {
                var laps = session.Laps?.OrderBy(l => l.LapNumber).ToList();
                if (laps == null || laps.Count < 5) continue;

                // Compare first 3 laps to last 3 laps (avoid outliers)
                var earlyLaps = laps.Skip(1).Take(3).Where(l => l.LapTime.TotalSeconds > 0).ToList();
                var lateLaps = laps.Skip(Math.Max(0, laps.Count - 3)).Where(l => l.LapTime.TotalSeconds > 0).ToList();

                if (earlyLaps.Any() && lateLaps.Any())
                {
                    double earlyAvg = earlyLaps.Average(l => l.LapTime.TotalSeconds);
                    double lateAvg = lateLaps.Average(l => l.LapTime.TotalSeconds);
                    double degradation = (lateAvg - earlyAvg) / earlyAvg; // Percentage increase
                    degradationRates.Add(degradation);
                }
            }

            return degradationRates.Any() ? degradationRates.Average() : 0.02; // Default 2% degradation
        }

        /// <summary>
        /// Determine driving style from lap statistics
        /// </summary>
        private DrivingStyle DetermineDrivingStyle(List<LapMetadata> laps)
        {
            if (laps.Count < 5) return DrivingStyle.Unknown;

            // Calculate lap time variance
            var lapTimes = laps.Where(l => l.LapTime.TotalSeconds > 0).Select(l => l.LapTime.TotalSeconds).ToList();
            if (lapTimes.Count < 5) return DrivingStyle.Unknown;

            double mean = lapTimes.Average();
            double variance = lapTimes.Average(t => Math.Pow(t - mean, 2));
            double stdDev = Math.Sqrt(variance);
            double coefficientOfVariation = stdDev / mean;

            // Smooth: Low variance (CV < 2%)
            if (coefficientOfVariation < 0.02)
                return DrivingStyle.Smooth;

            // Aggressive: High variance (CV > 5%)
            if (coefficientOfVariation > 0.05)
                return DrivingStyle.Aggressive;

            return DrivingStyle.Mixed;
        }
    }
}
