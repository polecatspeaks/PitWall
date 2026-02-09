using System;
using PitWall.Models;

namespace PitWall.UI
{
    /// <summary>
    /// Formats driver profile data for UI display.
    /// </summary>
    public static class ProfileDisplayFormatter
    {
        public static string FormatDetails(DriverProfile profile)
        {
            if (profile == null)
            {
                return "Select a profile to view details";
            }

            var daysAgo = (DateTime.Now - profile.LastUpdated).TotalDays;
            string freshness = profile.IsStale ? "Stale" : "Fresh";
            string confidence = profile.Confidence > 0 ? $"{profile.Confidence:P0}" : "n/a";

            return string.Join("\n", new[]
            {
                $"Driver: {profile.DriverName}",
                $"Track/Car: {profile.TrackName} / {profile.CarName}",
                $"Fuel/lap: {profile.AverageFuelPerLap:F2} | Style: {profile.Style}",
                $"Confidence: {confidence} | Sessions: {profile.SessionsCompleted}",
                $"Last Updated: {daysAgo:F0} days ago ({profile.LastUpdated:yyyy-MM-dd}) | Freshness: {freshness}"
            });
        }
    }
}
