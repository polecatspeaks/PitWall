using System;
using PitWall.Agent.Models;
using PitWall.Agent.Services.RulesEngine;
using Xunit;

namespace PitWall.Tests
{
    public class RulesEngineTests
    {
        private readonly RulesEngine _engine;

        public RulesEngineTests()
        {
            _engine = new RulesEngine();
        }

        #region Fuel Query Tests

        [Fact]
        public void TryAnswer_FuelQuery_CriticalLevel_ReturnsUrgentMessage()
        {
            // Arrange
            var context = new RaceContext
            {
                FuelLapsRemaining = 1.0,
                AvgFuelPerLap = 2.5
            };

            // Act
            var response = _engine.TryAnswer("How much fuel do I have?", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("FUEL CRITICAL", response.Answer);
            Assert.Contains("Box immediately", response.Answer);
            Assert.Equal("RulesEngine", response.Source);
            Assert.Equal(1.0, response.Confidence);
            Assert.True(response.Success);
        }

        [Fact]
        public void TryAnswer_FuelQuery_LowLevel_ReturnsWarning()
        {
            // Arrange
            var context = new RaceContext
            {
                FuelLapsRemaining = 2.5,
                AvgFuelPerLap = 2.0
            };

            // Act
            var response = _engine.TryAnswer("fuel remaining", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("Low fuel", response.Answer);
            Assert.Contains("2.5 laps", response.Answer);
            Assert.Contains("Plan to pit soon", response.Answer);
            Assert.Equal(1.0, response.Confidence); // 2.5 < 3.0 = 1.0 confidence
        }

        [Fact]
        public void TryAnswer_FuelQuery_NormalLevel_ReturnsInfo()
        {
            // Arrange
            var context = new RaceContext
            {
                FuelLapsRemaining = 8.5,
                AvgFuelPerLap = 2.3
            };

            // Act
            var response = _engine.TryAnswer("how much gas left", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("8.5 laps", response.Answer);
            Assert.Contains("2.3", response.Answer);
            Assert.Contains("liters per lap", response.Answer);
            Assert.True(response.Confidence >= 0.7);
        }

        [Theory]
        [InlineData("fuel")]
        [InlineData("gas")]
        [InlineData("laps remaining")]
        [InlineData("how much fuel")]
        [InlineData("FUEL LEVEL")]
        [InlineData("Gas Status")]
        public void TryAnswer_FuelQuery_RecognizesKeywords(string query)
        {
            // Arrange
            var context = new RaceContext
            {
                FuelLapsRemaining = 5.0,
                AvgFuelPerLap = 2.0
            };

            // Act
            var response = _engine.TryAnswer(query, context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("laps", response.Answer, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Pit Query Tests

        [Fact]
        public void TryAnswer_PitQuery_FuelCritical_ReturnsBoxNow()
        {
            // Arrange
            var context = new RaceContext
            {
                FuelLapsRemaining = 1.0,
                CurrentLap = 10,
                OptimalPitLap = 15,
                AverageTireWear = 50
            };

            // Act
            var response = _engine.TryAnswer("should i pit?", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("BOX THIS LAP", response.Answer);
            Assert.Contains("Fuel or tire critical", response.Answer);
        }

        [Fact]
        public void TryAnswer_PitQuery_TireCritical_ReturnsBoxNow()
        {
            // Arrange
            var context = new RaceContext
            {
                FuelLapsRemaining = 5.0,
                CurrentLap = 10,
                OptimalPitLap = 15,
                AverageTireWear = 10
            };

            // Act
            var response = _engine.TryAnswer("pit stop", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("BOX THIS LAP", response.Answer);
            Assert.Contains("Fuel or tire critical", response.Answer);
        }

        [Fact]
        public void TryAnswer_PitQuery_InOptimalWindow_ReturnsBoxNow()
        {
            // Arrange
            var context = new RaceContext
            {
                FuelLapsRemaining = 5.0,
                CurrentLap = 15,
                OptimalPitLap = 15,
                AverageTireWear = 40,
                StrategyConfidence = 0.85
            };

            // Act
            var response = _engine.TryAnswer("box", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("Box this lap", response.Answer);
            Assert.Contains("Optimal window", response.Answer);
            Assert.Contains("5.0 laps", response.Answer);
            Assert.Contains("40%", response.Answer);
            Assert.Equal(0.85, response.Confidence);
        }

        [Fact]
        public void TryAnswer_PitQuery_BeforeOptimal_ReturnsStayOut()
        {
            // Arrange
            var context = new RaceContext
            {
                FuelLapsRemaining = 8.0,
                CurrentLap = 10,
                OptimalPitLap = 15,
                AverageTireWear = 60
            };

            // Act
            var response = _engine.TryAnswer("when to pit", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("Stay out 5 more laps", response.Answer);
            Assert.Contains("pit on lap 15", response.Answer);
        }

        [Fact]
        public void TryAnswer_PitQuery_AfterOptimal_ReturnsLatePit()
        {
            // Arrange
            var context = new RaceContext
            {
                FuelLapsRemaining = 3.0,
                CurrentLap = 20,
                OptimalPitLap = 15,
                AverageTireWear = 30
            };

            // Act
            var response = _engine.TryAnswer("come in", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("should have pitted already", response.Answer);
            Assert.Contains("Box this lap if possible", response.Answer);
        }

        [Theory]
        [InlineData("pit")]
        [InlineData("box")]
        [InlineData("stop")]
        [InlineData("come in")]
        [InlineData("should i pit")]
        public void TryAnswer_PitQuery_RecognizesKeywords(string query)
        {
            // Arrange
            var context = new RaceContext
            {
                FuelLapsRemaining = 5.0,
                CurrentLap = 10,
                OptimalPitLap = 12,
                AverageTireWear = 50
            };

            // Act
            var response = _engine.TryAnswer(query, context);

            // Assert
            Assert.NotNull(response);
            Assert.NotEmpty(response.Answer);
        }

        #endregion

        #region Tire Query Tests

        [Fact]
        public void TryAnswer_TireQuery_CriticalWear_ReturnsWarning()
        {
            // Arrange
            var context = new RaceContext
            {
                AverageTireWear = 15,
                TireLapsOnSet = 25
            };

            // Act
            var response = _engine.TryAnswer("tire status", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("15% remaining", response.Answer);
            Assert.Contains("25 laps old", response.Answer);
            Assert.Contains("critically worn", response.Answer);
            Assert.Equal(0.9, response.Confidence);
        }

        [Fact]
        public void TryAnswer_TireQuery_DegradedWear_ReturnsWarning()
        {
            // Arrange
            var context = new RaceContext
            {
                AverageTireWear = 35,
                TireLapsOnSet = 18
            };

            // Act
            var response = _engine.TryAnswer("how are the tyres", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("35% remaining", response.Answer);
            Assert.Contains("18 laps old", response.Answer);
            Assert.Contains("degrading", response.Answer);
            Assert.Contains("plan pit stop soon", response.Answer);
        }

        [Fact]
        public void TryAnswer_TireQuery_GoodWear_ReturnsStatus()
        {
            // Arrange
            var context = new RaceContext
            {
                AverageTireWear = 75,
                TireLapsOnSet = 5
            };

            // Act
            var response = _engine.TryAnswer("tire wear", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("75% remaining", response.Answer);
            Assert.Contains("5 laps old", response.Answer);
            Assert.DoesNotContain("critically worn", response.Answer);
            Assert.DoesNotContain("degrading", response.Answer);
        }

        [Theory]
        [InlineData("tire")]
        [InlineData("tyre")]
        [InlineData("rubber")]
        [InlineData("grip")]
        [InlineData("wear")]
        public void TryAnswer_TireQuery_RecognizesKeywords(string query)
        {
            // Arrange
            var context = new RaceContext
            {
                AverageTireWear = 50,
                TireLapsOnSet = 10
            };

            // Act
            var response = _engine.TryAnswer(query, context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("50% remaining", response.Answer);
        }

        #endregion

        #region Gap Query Tests

        [Fact]
        public void TryAnswer_GapQuery_LeadingPosition_ReturnsLeadInfo()
        {
            // Arrange
            var context = new RaceContext
            {
                Position = 1,
                GapToBehind = 3.5
            };

            // Act
            var response = _engine.TryAnswer("what's my position", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("P1", response.Answer);
            Assert.Contains("Leading by 3.5 seconds", response.Answer);
            Assert.Equal(1.0, response.Confidence);
        }

        [Fact]
        public void TryAnswer_GapQuery_MidFieldPosition_ReturnsGaps()
        {
            // Arrange
            var context = new RaceContext
            {
                Position = 5,
                GapToAhead = 2.3,
                GapToBehind = 1.8
            };

            // Act
            var response = _engine.TryAnswer("gap to leader", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("P5", response.Answer);
            Assert.Contains("+2.3s", response.Answer);
            Assert.Contains("-1.8s", response.Answer);
            Assert.Contains("Gap ahead", response.Answer);
            Assert.Contains("Gap behind", response.Answer);
        }

        [Theory]
        [InlineData("gap")]
        [InlineData("position")]
        [InlineData("where am i")]
        [InlineData("ahead")]
        [InlineData("behind")]
        public void TryAnswer_GapQuery_RecognizesKeywords(string query)
        {
            // Arrange
            var context = new RaceContext
            {
                Position = 3,
                GapToAhead = 1.5,
                GapToBehind = 2.1
            };

            // Act
            var response = _engine.TryAnswer(query, context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("P3", response.Answer);
        }

        #endregion

        #region Weather Query Tests

        [Fact]
        public void TryAnswer_WeatherQuery_ReturnsWeatherInfo()
        {
            // Arrange
            var context = new RaceContext
            {
                CurrentWeather = "Sunny",
                TrackTemp = 32
            };

            // Act
            var response = _engine.TryAnswer("what's the weather", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("Sunny", response.Answer);
            Assert.Contains("32C", response.Answer);
            Assert.Contains("Track", response.Answer);
            Assert.Equal(0.8, response.Confidence);
        }

        [Theory]
        [InlineData("weather")]
        [InlineData("rain")]
        [InlineData("wet")]
        [InlineData("dry")]
        [InlineData("track temp")]
        public void TryAnswer_WeatherQuery_RecognizesKeywords(string query)
        {
            // Arrange
            var context = new RaceContext
            {
                CurrentWeather = "Clear",
                TrackTemp = 28
            };

            // Act
            var response = _engine.TryAnswer(query, context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("Clear", response.Answer);
            Assert.Contains("28C", response.Answer);
        }

        #endregion

        #region Pace Query Tests

        [Fact]
        public void TryAnswer_PaceQuery_FasterLap_ReturnsPositiveDelta()
        {
            // Arrange
            var context = new RaceContext
            {
                LastLapTime = 92.345,
                BestLapTime = 91.234
            };

            // Act
            var response = _engine.TryAnswer("what's my pace", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("1:32.345", response.Answer);
            Assert.Contains("1:31.234", response.Answer);
            Assert.Contains("+1.111", response.Answer);
            Assert.Equal(0.95, response.Confidence);
        }

        [Fact]
        public void TryAnswer_PaceQuery_SlowerLap_ReturnsNegativeDelta()
        {
            // Arrange
            var context = new RaceContext
            {
                LastLapTime = 90.123,
                BestLapTime = 91.456
            };

            // Act
            var response = _engine.TryAnswer("lap time delta", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("1:30.123", response.Answer);
            Assert.Contains("1:31.456", response.Answer);
            Assert.Contains("-1.333", response.Answer);
        }

        [Fact]
        public void TryAnswer_PaceQuery_FormatsLapTimeCorrectly()
        {
            // Arrange
            var context = new RaceContext
            {
                LastLapTime = 125.678,
                BestLapTime = 125.678
            };

            // Act
            var response = _engine.TryAnswer("lap time", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("2:05.678", response.Answer);
        }

        [Theory]
        [InlineData("pace")]
        [InlineData("lap time")]
        [InlineData("delta")]
        [InlineData("fast")]
        [InlineData("slow")]
        public void TryAnswer_PaceQuery_RecognizesKeywords(string query)
        {
            // Arrange
            var context = new RaceContext
            {
                LastLapTime = 90.5,
                BestLapTime = 90.0
            };

            // Act
            var response = _engine.TryAnswer(query, context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("1:30", response.Answer);
        }

        #endregion

        #region Confidence Calculation Tests

        [Theory]
        [InlineData(1.0, 1.0)]
        [InlineData(2.9, 1.0)]
        [InlineData(3.5, 0.9)]
        [InlineData(4.4, 0.9)]
        [InlineData(5.0, 0.8)]
        [InlineData(5.9, 0.8)]
        [InlineData(7.0, 0.7)]
        [InlineData(10.0, 0.7)]
        public void TryAnswer_FuelQuery_CalculatesCorrectConfidence(double fuelLaps, double expectedConfidence)
        {
            // Arrange
            var context = new RaceContext
            {
                FuelLapsRemaining = fuelLaps,
                AvgFuelPerLap = 2.0
            };

            // Act
            var response = _engine.TryAnswer("fuel", context);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(expectedConfidence, response.Confidence);
        }

        #endregion

        #region Unrecognized Query Tests

        [Fact]
        public void TryAnswer_UnrecognizedQuery_ReturnsNull()
        {
            // Arrange
            var context = new RaceContext();

            // Act
            var response = _engine.TryAnswer("what is the meaning of life", context);

            // Assert
            Assert.Null(response);
        }

        [Fact]
        public void TryAnswer_EmptyQuery_ReturnsNull()
        {
            // Arrange
            var context = new RaceContext();

            // Act
            var response = _engine.TryAnswer("", context);

            // Assert
            Assert.Null(response);
        }

        [Fact]
        public void TryAnswer_WhitespaceQuery_ReturnsNull()
        {
            // Arrange
            var context = new RaceContext();

            // Act
            var response = _engine.TryAnswer("   ", context);

            // Assert
            Assert.Null(response);
        }

        #endregion

        #region Response Properties Tests

        [Fact]
        public void TryAnswer_AllResponses_HaveValidResponseTime()
        {
            // Arrange
            var context = new RaceContext
            {
                FuelLapsRemaining = 5.0,
                AvgFuelPerLap = 2.0
            };

            // Act
            var response = _engine.TryAnswer("fuel", context);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.ResponseTimeMs >= 0);
        }

        [Fact]
        public void TryAnswer_AllResponses_MarkedAsSuccess()
        {
            // Arrange
            var context = new RaceContext
            {
                Position = 1,
                GapToBehind = 2.0
            };

            // Act
            var response = _engine.TryAnswer("position", context);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Success);
        }

        [Fact]
        public void TryAnswer_AllResponses_HaveSource()
        {
            // Arrange
            var context = new RaceContext
            {
                CurrentWeather = "Rainy",
                TrackTemp = 20
            };

            // Act
            var response = _engine.TryAnswer("weather", context);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("RulesEngine", response.Source);
        }

        #endregion

        #region Query Normalization Tests

        [Fact]
        public void TryAnswer_CaseInsensitive_RecognizesQueries()
        {
            // Arrange
            var context = new RaceContext
            {
                FuelLapsRemaining = 5.0,
                AvgFuelPerLap = 2.0
            };

            // Act
            var lower = _engine.TryAnswer("fuel", context);
            var upper = _engine.TryAnswer("FUEL", context);
            var mixed = _engine.TryAnswer("FuEl", context);

            // Assert
            Assert.NotNull(lower);
            Assert.NotNull(upper);
            Assert.NotNull(mixed);
        }

        [Fact]
        public void TryAnswer_TrimsWhitespace_RecognizesQueries()
        {
            // Arrange
            var context = new RaceContext
            {
                AverageTireWear = 50,
                TireLapsOnSet = 10
            };

            // Act
            var response = _engine.TryAnswer("   tire status   ", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("50% remaining", response.Answer);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void TryAnswer_ZeroFuelRemaining_HandlesCritical()
        {
            // Arrange
            var context = new RaceContext
            {
                FuelLapsRemaining = 0.0,
                AvgFuelPerLap = 2.0
            };

            // Act
            var response = _engine.TryAnswer("fuel", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("FUEL CRITICAL", response.Answer);
        }

        [Fact]
        public void TryAnswer_ZeroTireWear_HandlesGracefully()
        {
            // Arrange
            var context = new RaceContext
            {
                AverageTireWear = 0,
                TireLapsOnSet = 30
            };

            // Act
            var response = _engine.TryAnswer("tire", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("0% remaining", response.Answer);
            Assert.Contains("critically worn", response.Answer);
        }

        [Fact]
        public void TryAnswer_PositionOne_ZeroGapBehind_HandlesGracefully()
        {
            // Arrange
            var context = new RaceContext
            {
                Position = 1,
                GapToBehind = 0.0
            };

            // Act
            var response = _engine.TryAnswer("position", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("P1", response.Answer);
            Assert.Contains("0.0 seconds", response.Answer);
        }

        [Fact]
        public void TryAnswer_IdenticalLapTimes_ShowsZeroDelta()
        {
            // Arrange
            var context = new RaceContext
            {
                LastLapTime = 90.000,
                BestLapTime = 90.000
            };

            // Act
            var response = _engine.TryAnswer("pace", context);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("+0.000", response.Answer);
        }

        #endregion
    }
}
