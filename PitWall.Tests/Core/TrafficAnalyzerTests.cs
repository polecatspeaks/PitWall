using PitWall.Core;
using PitWall.Models;
using Xunit;

namespace PitWall.Tests.Core
{
    public class TrafficAnalyzerTests
    {
        [Fact]
        public void ClassifyOpponent_SlowerBy10Seconds_ReturnsSlowerClass()
        {
            var analyzer = new TrafficAnalyzer();
            var playerBest = 120.0;
            var opponentBest = 130.0;

            var classification = analyzer.ClassifyOpponent(playerBest, opponentBest);

            Assert.Equal(TrafficClass.SlowerClass, classification);
        }

        [Fact]
        public void ClassifyOpponent_FasterBy5Seconds_ReturnsFasterClass()
        {
            var analyzer = new TrafficAnalyzer();
            var playerBest = 120.0;
            var opponentBest = 115.0;

            var classification = analyzer.ClassifyOpponent(playerBest, opponentBest);

            Assert.Equal(TrafficClass.FasterClass, classification);
        }

        [Fact]
        public void ClassifyOpponent_WithinOneSecond_ReturnsSameClass()
        {
            var analyzer = new TrafficAnalyzer();
            var playerBest = 120.0;
            var opponentBest = 120.5;

            var classification = analyzer.ClassifyOpponent(playerBest, opponentBest);

            Assert.Equal(TrafficClass.SameClass, classification);
        }

        [Fact]
        public void IsPitEntryUnsafe_FasterClassWithin3Seconds_ReturnsTrue()
        {
            var analyzer = new TrafficAnalyzer();
            var opponents = new[]
            {
                new OpponentData { Position = 1, BestLapTime = 110.0, GapSeconds = 2.5 },
                new OpponentData { Position = 3, BestLapTime = 120.0, GapSeconds = 5.0 }
            };

            var isUnsafe = analyzer.IsPitEntryUnsafe(120.0, opponents);

            Assert.True(isUnsafe);
        }

        [Fact]
        public void IsPitEntryUnsafe_FasterClassFarBehind_ReturnsFalse()
        {
            var analyzer = new TrafficAnalyzer();
            var opponents = new[]
            {
                new OpponentData { Position = 1, BestLapTime = 110.0, GapSeconds = 8.0 },
                new OpponentData { Position = 3, BestLapTime = 120.0, GapSeconds = 3.0 }
            };

            var isUnsafe = analyzer.IsPitEntryUnsafe(120.0, opponents);

            Assert.False(isUnsafe);
        }

        [Fact]
        public void IsPitEntryUnsafe_NoFasterClassNearby_ReturnsFalse()
        {
            var analyzer = new TrafficAnalyzer();
            var opponents = new[]
            {
                new OpponentData { Position = 2, BestLapTime = 120.5, GapSeconds = 2.0 },
                new OpponentData { Position = 3, BestLapTime = 130.0, GapSeconds = 10.0 }
            };

            var isUnsafe = analyzer.IsPitEntryUnsafe(120.0, opponents);

            Assert.False(isUnsafe);
        }

        [Fact]
        public void GetTrafficMessage_FasterClassApproaching_ReturnsWarning()
        {
            var analyzer = new TrafficAnalyzer();
            var opponents = new[]
            {
                new OpponentData { Position = 1, BestLapTime = 110.0, GapSeconds = 2.5, CarName = "LMP2" }
            };

            var message = analyzer.GetTrafficMessage(120.0, opponents);

            Assert.Contains("faster class", message.ToLower());
            Assert.Contains("2.5", message);
        }
    }
}
