using System;
using PitWall.Core.Models;
using PitWall.Strategy;
using Xunit;

namespace PitWall.Tests
{
    public class StrategyTests
    {
        [Fact]
        public void TyreTemp_Overheat_ReturnsWarning()
        {
            var engine = new StrategyEngine();
            var sample = new TelemetrySample(DateTime.UtcNow, 150, new double[] { 112, 85, 90, 88 }, 30, 0, 0.5, 0);

            var result = engine.Evaluate(sample);

            Assert.Contains("Tyre overheat", result);
        }

        [Fact]
        public void FuelProjection_CalculatesLaps()
        {
            var engine = new StrategyEngine();
            var sample = new TelemetrySample(DateTime.UtcNow, 100, new double[] { 80, 80, 80, 80 }, 50.0, 0, 0.5, 0);

            var laps = engine.ProjectLapsRemaining(sample, 2.5);

            Assert.Equal(20, laps);
        }
    }
}
