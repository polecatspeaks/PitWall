using System;
using Xunit;
using PitWall.Core;
using PitWall.Models;

namespace PitWall.Tests.Core
{
    public class UndercutStrategyTests
    {
        [Fact]
        public void CanUndercut_GapAllowsPositionGain_ReturnsTrue()
        {
            // Arrange
            var strategy = new UndercutStrategy();
            var situation = new RaceSituation
            {
                GapToCarAhead = 5.0,        // 5 seconds ahead
                PitStopDuration = 25.0,     // 25s pit time
                FreshTyreAdvantage = 2.0,   // 2s/lap faster on new tyres
                CurrentTyreLaps = 15,
                OpponentTyreAge = 15        // Same age tyres
            };

            // Act
            var canUndercut = strategy.CanUndercut(situation);

            // Assert
            // After pit: -20s (net loss = 25 - 5)
            // Lap 1: +2s gain, gap = -18s
            // Lap 2: +2s gain, gap = -16s
            // Lap 3: +2s gain, gap = -14s
            // Need ~10 laps to overcome, but if opponent pits after us we gain position
            Assert.True(canUndercut);
        }

        [Fact]
        public void CanUndercut_GapTooLarge_ReturnsFalse()
        {
            // Arrange
            var strategy = new UndercutStrategy();
            var situation = new RaceSituation
            {
                GapToCarAhead = 30.0,       // 30 seconds ahead - too far
                PitStopDuration = 25.0,
                FreshTyreAdvantage = 2.0,
                CurrentTyreLaps = 10,
                OpponentTyreAge = 10
            };

            // Act
            var canUndercut = strategy.CanUndercut(situation);

            // Assert
            // Gap too large to make up even with tyre advantage
            Assert.False(canUndercut);
        }

        [Fact]
        public void CanUndercut_SmallTyreAdvantage_ReturnsFalse()
        {
            // Arrange
            var strategy = new UndercutStrategy();
            var situation = new RaceSituation
            {
                GapToCarAhead = 5.0,
                PitStopDuration = 25.0,
                FreshTyreAdvantage = 0.5,   // Only 0.5s/lap advantage - not enough
                CurrentTyreLaps = 20,
                OpponentTyreAge = 20
            };

            // Act
            var canUndercut = strategy.CanUndercut(situation);

            // Assert
            // Tyre advantage too small to overcome pit delta
            Assert.False(canUndercut);
        }

        [Fact]
        public void CanOvercut_OpponentWillPitSoon_ReturnsTrue()
        {
            // Arrange
            var strategy = new UndercutStrategy();
            var situation = new RaceSituation
            {
                GapToCarBehind = 8.0,       // 8 seconds behind
                PitStopDuration = 25.0,
                FreshTyreAdvantage = 1.5,   // Car behind gets 1.5s/lap advantage if they pit
                CurrentTyreLaps = 10,
                OpponentTyreAge = 18        // Car behind on old tyres, likely to pit
            };

            // Act
            var canOvercut = strategy.CanOvercut(situation);

            // Assert
            // If we stay out, car behind pits and loses 17s (25-8)
            // We can build enough gap to stay ahead when we pit later
            Assert.True(canOvercut);
        }

        [Fact]
        public void CanOvercut_GapTooSmall_ReturnsFalse()
        {
            // Arrange
            var strategy = new UndercutStrategy();
            var situation = new RaceSituation
            {
                GapToCarBehind = 2.0,       // Only 2 seconds - too close
                PitStopDuration = 25.0,
                FreshTyreAdvantage = 2.0,
                CurrentTyreLaps = 10,
                OpponentTyreAge = 15
            };

            // Act
            var canOvercut = strategy.CanOvercut(situation);

            // Assert
            // Gap too small to defend against undercut
            Assert.False(canOvercut);
        }

        [Fact]
        public void CalculatePositionGain_UndercutSuccessful_ReturnsPositiveGain()
        {
            // Arrange
            var strategy = new UndercutStrategy();
            var situation = new RaceSituation
            {
                GapToCarAhead = 6.0,
                PitStopDuration = 25.0,
                FreshTyreAdvantage = 2.5,
                CurrentTyreLaps = 12,
                OpponentTyreAge = 12
            };

            // Act
            var positionGain = strategy.CalculatePositionGain(situation);

            // Assert
            // Should simulate positive gain (we overtake car ahead)
            Assert.True(positionGain > 0);
        }

        [Fact]
        public void EstimateFreshTyreAdvantage_HighDegradation_ReturnsLargeAdvantage()
        {
            // Arrange
            var strategy = new UndercutStrategy();
            int currentTyreLaps = 20;
            double tyreDegradationPerLap = 0.15; // 0.15s/lap degradation

            // Act
            var advantage = strategy.EstimateFreshTyreAdvantage(currentTyreLaps, tyreDegradationPerLap);

            // Assert
            // Fresh tyres vs 20 lap old tyres: 20 * 0.15 = 3.0s advantage
            Assert.InRange(advantage, 2.5, 3.5);
        }

        [Fact]
        public void EstimateFreshTyreAdvantage_NewTyres_ReturnsSmallAdvantage()
        {
            // Arrange
            var strategy = new UndercutStrategy();
            int currentTyreLaps = 3;
            double tyreDegradationPerLap = 0.1;

            // Act
            var advantage = strategy.EstimateFreshTyreAdvantage(currentTyreLaps, tyreDegradationPerLap);

            // Assert
            // Fresh vs 3 lap old: 3 * 0.1 = 0.3s advantage (minimal)
            Assert.InRange(advantage, 0.2, 0.5);
        }
    }
}
