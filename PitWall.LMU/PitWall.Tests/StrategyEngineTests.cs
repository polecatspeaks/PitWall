using System;
using PitWall.Core.Models;
using PitWall.Strategy;
using Xunit;

namespace PitWall.Tests
{
    public class StrategyEngineTests
    {
        [Fact]
        public void Evaluate_WithWheelLockRisk_ReturnsBrakeWarning()
        {
            var engine = new StrategyEngine();
            var sample = new TelemetrySample(DateTime.UtcNow, 150, new double[] { 90, 90, 90, 90 }, 20, 0.95, 0.0, 0.0);

            var result = engine.EvaluateWithConfidence(sample);

            Assert.Contains("Wheel lock", result.Recommendation, StringComparison.OrdinalIgnoreCase);
            Assert.True(result.Confidence >= 0.7);
        }

        [Fact]
        public void Evaluate_WithBrakeOverlap_ReturnsCoaching()
        {
            var engine = new StrategyEngine();
            var sample = new TelemetrySample(DateTime.UtcNow, 80, new double[] { 90, 90, 90, 90 }, 20, 0.7, 0.4, 0.1);

            var result = engine.EvaluateWithConfidence(sample);

            Assert.Contains("overlap", result.Recommendation, StringComparison.OrdinalIgnoreCase);
            Assert.True(result.Confidence >= 0.5);
        }

        [Fact]
        public void Evaluate_WithPitWindow_ReturnsPitWindowMessage()
        {
            var engine = new StrategyEngine();
            var sample = new TelemetrySample(DateTime.UtcNow, 100, new double[] { 90, 90, 90, 90 }, 8, 0.1, 0.2, 0.0);

            var result = engine.EvaluateWithConfidence(sample);

            Assert.Contains("Pit window", result.Recommendation, StringComparison.OrdinalIgnoreCase);
            Assert.True(result.Confidence >= 0.5);
        }
    }
}
