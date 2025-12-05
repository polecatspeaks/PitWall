using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using PitWall.Core;
using PitWall.Models;

namespace PitWall.Tests.Core
{
    public class ProfileAnalyzerTests
    {
        [Fact]
        public void AnalyzeSession_StoresFuelUsageProfile()
        {
            // Arrange
            var analyzer = new ProfileAnalyzer();
            var session = CreateTestSession();

            // Act
            var profile = analyzer.AnalyzeSession(session);

            // Assert
            Assert.NotNull(profile);
            Assert.Equal("TestDriver", profile.DriverName);
            Assert.Equal("TestTrack", profile.TrackName);
            Assert.Equal("TestCar", profile.CarName);
            Assert.InRange(profile.AverageFuelPerLap, 2.4, 2.6); // 2.5 average
            Assert.Equal(1, profile.SessionsCompleted);
        }

        [Fact]
        public void AnalyzeSession_IdentifiesSmoothDrivingStyle()
        {
            // Arrange
            var analyzer = new ProfileAnalyzer();
            var session = CreateConsistentSession(); // Small variance

            // Act
            var profile = analyzer.AnalyzeSession(session);

            // Assert
            Assert.Equal(DrivingStyle.Smooth, profile.Style);
        }

        [Fact]
        public void AnalyzeSession_IdentifiesAggressiveDrivingStyle()
        {
            // Arrange
            var analyzer = new ProfileAnalyzer();
            var session = CreateErraticSession(); // High variance

            // Act
            var profile = analyzer.AnalyzeSession(session);

            // Assert
            Assert.Equal(DrivingStyle.Aggressive, profile.Style);
        }

        [Fact]
        public void AnalyzeSession_IdentifiesMixedDrivingStyle()
        {
            // Arrange
            var analyzer = new ProfileAnalyzer();
            var session = CreateMixedSession(); // Moderate variance

            // Act
            var profile = analyzer.AnalyzeSession(session);

            // Assert
            Assert.Equal(DrivingStyle.Mixed, profile.Style);
        }

        [Fact]
        public void AnalyzeSession_CalculatesTypicalTyreDegradation()
        {
            // Arrange
            var analyzer = new ProfileAnalyzer();
            var session = CreateTestSession();

            // Act
            var profile = analyzer.AnalyzeSession(session);

            // Assert
            Assert.InRange(profile.TypicalTyreDegradation, 0.4, 0.6); // 0.5% per lap
        }

        [Fact]
        public void AnalyzeSession_IgnoresInvalidLaps()
        {
            // Arrange
            var analyzer = new ProfileAnalyzer();
            var session = CreateSessionWithInvalidLaps();

            // Act
            var profile = analyzer.AnalyzeSession(session);

            // Assert
            // Should only count 3 valid laps, not 5 total
            Assert.InRange(profile.AverageFuelPerLap, 2.4, 2.6);
        }

        [Fact]
        public void MergeProfiles_CombinesMultipleSessions()
        {
            // Arrange
            var analyzer = new ProfileAnalyzer();
            var profile1 = CreateProfile(2.5, 0.5, 5);
            var profile2 = CreateProfile(2.7, 0.6, 3);

            // Act
            var merged = analyzer.MergeProfiles(profile1, profile2);

            // Assert
            Assert.Equal(8, merged.SessionsCompleted);
            // Weighted average: (2.5*5 + 2.7*3) / 8 = 2.575
            Assert.InRange(merged.AverageFuelPerLap, 2.55, 2.6);
        }

        // Helper methods
        private SessionData CreateTestSession()
        {
            return new SessionData
            {
                DriverName = "TestDriver",
                TrackName = "TestTrack",
                CarName = "TestCar",
                SessionType = "Race",
                SessionDate = DateTime.Now,
                TotalFuelUsed = 10.0,
                SessionDuration = TimeSpan.FromMinutes(10),
                Laps = new List<LapData>
                {
                    new LapData { LapNumber = 1, FuelUsed = 2.5, IsValid = true, IsClear = true, TyreWearAverage = 0.5 },
                    new LapData { LapNumber = 2, FuelUsed = 2.5, IsValid = true, IsClear = true, TyreWearAverage = 1.0 },
                    new LapData { LapNumber = 3, FuelUsed = 2.5, IsValid = true, IsClear = true, TyreWearAverage = 1.5 },
                    new LapData { LapNumber = 4, FuelUsed = 2.5, IsValid = true, IsClear = true, TyreWearAverage = 2.0 }
                }
            };
        }

        private SessionData CreateConsistentSession()
        {
            return new SessionData
            {
                DriverName = "SmoothDriver",
                TrackName = "TestTrack",
                CarName = "TestCar",
                SessionType = "Race",
                SessionDate = DateTime.Now,
                TotalFuelUsed = 10.0,
                SessionDuration = TimeSpan.FromMinutes(10),
                Laps = new List<LapData>
                {
                    new LapData { LapNumber = 1, LapTime = TimeSpan.FromSeconds(90.0), FuelUsed = 2.5, IsValid = true, IsClear = true },
                    new LapData { LapNumber = 2, LapTime = TimeSpan.FromSeconds(90.1), FuelUsed = 2.5, IsValid = true, IsClear = true },
                    new LapData { LapNumber = 3, LapTime = TimeSpan.FromSeconds(89.9), FuelUsed = 2.5, IsValid = true, IsClear = true },
                    new LapData { LapNumber = 4, LapTime = TimeSpan.FromSeconds(90.2), FuelUsed = 2.5, IsValid = true, IsClear = true }
                }
            };
        }

        private SessionData CreateErraticSession()
        {
            return new SessionData
            {
                DriverName = "AggressiveDriver",
                TrackName = "TestTrack",
                CarName = "TestCar",
                SessionType = "Race",
                SessionDate = DateTime.Now,
                TotalFuelUsed = 10.0,
                SessionDuration = TimeSpan.FromMinutes(10),
                Laps = new List<LapData>
                {
                    new LapData { LapNumber = 1, LapTime = TimeSpan.FromSeconds(88.0), FuelUsed = 3.0, IsValid = true, IsClear = true },
                    new LapData { LapNumber = 2, LapTime = TimeSpan.FromSeconds(95.0), FuelUsed = 2.0, IsValid = true, IsClear = true },
                    new LapData { LapNumber = 3, LapTime = TimeSpan.FromSeconds(87.0), FuelUsed = 3.2, IsValid = true, IsClear = true },
                    new LapData { LapNumber = 4, LapTime = TimeSpan.FromSeconds(93.0), FuelUsed = 1.8, IsValid = true, IsClear = true }
                }
            };
        }

        private SessionData CreateMixedSession()
        {
            return new SessionData
            {
                DriverName = "MixedDriver",
                TrackName = "TestTrack",
                CarName = "TestCar",
                SessionType = "Race",
                SessionDate = DateTime.Now,
                TotalFuelUsed = 10.0,
                SessionDuration = TimeSpan.FromMinutes(10),
                Laps = new List<LapData>
                {
                    new LapData { LapNumber = 1, LapTime = TimeSpan.FromSeconds(90.0), FuelUsed = 2.5, IsValid = true, IsClear = true },
                    new LapData { LapNumber = 2, LapTime = TimeSpan.FromSeconds(92.0), FuelUsed = 2.3, IsValid = true, IsClear = true },
                    new LapData { LapNumber = 3, LapTime = TimeSpan.FromSeconds(89.0), FuelUsed = 2.7, IsValid = true, IsClear = true },
                    new LapData { LapNumber = 4, LapTime = TimeSpan.FromSeconds(91.0), FuelUsed = 2.5, IsValid = true, IsClear = true }
                }
            };
        }

        private SessionData CreateSessionWithInvalidLaps()
        {
            return new SessionData
            {
                DriverName = "TestDriver",
                TrackName = "TestTrack",
                CarName = "TestCar",
                SessionType = "Race",
                SessionDate = DateTime.Now,
                TotalFuelUsed = 12.5,
                SessionDuration = TimeSpan.FromMinutes(12),
                Laps = new List<LapData>
                {
                    new LapData { LapNumber = 1, FuelUsed = 2.5, IsValid = true, IsClear = true },
                    new LapData { LapNumber = 2, FuelUsed = 5.0, IsValid = false, IsClear = false }, // Invalid - pit lap
                    new LapData { LapNumber = 3, FuelUsed = 2.5, IsValid = true, IsClear = true },
                    new LapData { LapNumber = 4, FuelUsed = 0.0, IsValid = false, IsClear = false }, // Invalid - incomplete
                    new LapData { LapNumber = 5, FuelUsed = 2.5, IsValid = true, IsClear = true }
                }
            };
        }

        private DriverProfile CreateProfile(double fuelPerLap, double tyreDeg, int sessions)
        {
            return new DriverProfile
            {
                DriverName = "TestDriver",
                TrackName = "TestTrack",
                CarName = "TestCar",
                AverageFuelPerLap = fuelPerLap,
                TypicalTyreDegradation = tyreDeg,
                Style = DrivingStyle.Smooth,
                SessionsCompleted = sessions,
                LastUpdated = DateTime.Now
            };
        }
    }
}
