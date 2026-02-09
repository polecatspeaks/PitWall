using System;
using PitWall.Models;
using PitWall.UI;
using Xunit;

namespace PitWall.Tests.UI
{
    public class ProfileDisplayFormatterTests
    {
        [Fact]
        public void FormatDetails_IncludesKeyFields()
        {
            var profile = new DriverProfile
            {
                DriverName = "Test Driver",
                TrackName = "Spa",
                CarName = "GT3",
                AverageFuelPerLap = 2.5,
                TypicalTyreDegradation = 0.03,
                Style = DrivingStyle.Smooth,
                SessionsCompleted = 4,
                LastUpdated = DateTime.UtcNow.AddDays(-2),
                Confidence = 0.6,
                IsStale = false
            };

            var text = ProfileDisplayFormatter.FormatDetails(profile);

            Assert.Contains("Test Driver", text);
            Assert.Contains("Spa", text);
            Assert.Contains("GT3", text);
            Assert.Contains("Fuel/lap: 2.50", text);
            Assert.Contains("Smooth", text);
            Assert.Contains("Confidence", text);
            Assert.Contains("Sessions: 4", text);
            Assert.Contains("Freshness", text);
        }

        [Fact]
        public void FormatDetails_MarksStale()
        {
            var profile = new DriverProfile
            {
                DriverName = "Old Driver",
                TrackName = "Old Track",
                CarName = "Old Car",
                AverageFuelPerLap = 1.5,
                TypicalTyreDegradation = 0.02,
                Style = DrivingStyle.Unknown,
                SessionsCompleted = 1,
                LastUpdated = DateTime.UtcNow.AddDays(-200),
                Confidence = 0.1,
                IsStale = true
            };

            var text = ProfileDisplayFormatter.FormatDetails(profile);

            Assert.Contains("Stale", text);
        }
    }
}
