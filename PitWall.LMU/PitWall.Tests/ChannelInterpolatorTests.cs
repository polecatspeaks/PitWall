using PitWall.Core.Utilities;
using Xunit;
using System;

namespace PitWall.Tests
{
    public class ChannelInterpolatorTests
    {
        // ── CreateTimeGrid ──────────────────────────────────────────────

        [Fact]
        public void CreateTimeGrid_50Hz_ProducesCorrectSpacing()
        {
            var grid = ChannelInterpolator.CreateTimeGrid(0.0, 1.0, 50);

            Assert.Equal(51, grid.Length); // 0.00, 0.02, ... 1.00
            Assert.Equal(0.0, grid[0], 6);
            Assert.Equal(0.02, grid[1], 6);
            Assert.Equal(1.0, grid[^1], 6);
        }

        [Fact]
        public void CreateTimeGrid_WithOffset_StartsAtCorrectTime()
        {
            var grid = ChannelInterpolator.CreateTimeGrid(100.0, 101.0, 50);

            Assert.Equal(100.0, grid[0], 6);
            Assert.Equal(100.02, grid[1], 6);
        }

        [Fact]
        public void CreateTimeGrid_EndBeforeStart_ReturnsEmpty()
        {
            var grid = ChannelInterpolator.CreateTimeGrid(5.0, 3.0, 50);

            Assert.Empty(grid);
        }

        [Fact]
        public void CreateTimeGrid_EqualStartEnd_ReturnsEmpty()
        {
            var grid = ChannelInterpolator.CreateTimeGrid(1.0, 1.0, 50);

            Assert.Empty(grid);
        }

        [Fact]
        public void CreateTimeGrid_ZeroFrequency_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ChannelInterpolator.CreateTimeGrid(0, 1, 0));
        }

        [Fact]
        public void CreateTimeGrid_NegativeFrequency_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ChannelInterpolator.CreateTimeGrid(0, 1, -10));
        }

        [Fact]
        public void CreateTimeGrid_1Hz_ProducesOnePointPerSecond()
        {
            var grid = ChannelInterpolator.CreateTimeGrid(0.0, 5.0, 1);

            Assert.Equal(6, grid.Length); // 0, 1, 2, 3, 4, 5
            for (int i = 0; i < grid.Length; i++)
            {
                Assert.Equal(i, grid[i], 6);
            }
        }

        // ── EstimateChannelTimestamps ───────────────────────────────────

        [Fact]
        public void EstimateChannelTimestamps_EvenlyDistributesSamples()
        {
            var ts = ChannelInterpolator.EstimateChannelTimestamps(5, 0.0, 4.0);

            Assert.Equal(5, ts.Length);
            Assert.Equal(0.0, ts[0], 6);
            Assert.Equal(1.0, ts[1], 6);
            Assert.Equal(2.0, ts[2], 6);
            Assert.Equal(3.0, ts[3], 6);
            Assert.Equal(4.0, ts[4], 6);
        }

        [Fact]
        public void EstimateChannelTimestamps_SingleSample_ReturnsStartTime()
        {
            var ts = ChannelInterpolator.EstimateChannelTimestamps(1, 10.0, 20.0);

            Assert.Single(ts);
            Assert.Equal(10.0, ts[0], 6);
        }

        [Fact]
        public void EstimateChannelTimestamps_ZeroSamples_ReturnsEmpty()
        {
            var ts = ChannelInterpolator.EstimateChannelTimestamps(0, 0.0, 1.0);

            Assert.Empty(ts);
        }

        [Fact]
        public void EstimateChannelTimestamps_TwoSamples_ReturnsStartAndEnd()
        {
            var ts = ChannelInterpolator.EstimateChannelTimestamps(2, 5.0, 10.0);

            Assert.Equal(2, ts.Length);
            Assert.Equal(5.0, ts[0], 6);
            Assert.Equal(10.0, ts[1], 6);
        }

        // ── Interpolate ────────────────────────────────────────────────

        [Fact]
        public void Interpolate_ExactTimestamps_ReturnsExactValues()
        {
            var srcTs = new[] { 0.0, 1.0, 2.0, 3.0 };
            var srcVals = new[] { 10.0, 20.0, 30.0, 40.0 };
            var targetTs = new[] { 0.0, 1.0, 2.0, 3.0 };

            var result = ChannelInterpolator.Interpolate(srcTs, srcVals, targetTs);

            Assert.Equal(10.0, result[0], 6);
            Assert.Equal(20.0, result[1], 6);
            Assert.Equal(30.0, result[2], 6);
            Assert.Equal(40.0, result[3], 6);
        }

        [Fact]
        public void Interpolate_MidpointValues_InterpolatesLinearly()
        {
            var srcTs = new[] { 0.0, 2.0 };
            var srcVals = new[] { 0.0, 100.0 };
            var targetTs = new[] { 0.5, 1.0, 1.5 };

            var result = ChannelInterpolator.Interpolate(srcTs, srcVals, targetTs);

            Assert.Equal(25.0, result[0], 6);
            Assert.Equal(50.0, result[1], 6);
            Assert.Equal(75.0, result[2], 6);
        }

        [Fact]
        public void Interpolate_BeforeFirstTimestamp_ClampsToFirst()
        {
            var srcTs = new[] { 1.0, 2.0 };
            var srcVals = new[] { 100.0, 200.0 };
            var targetTs = new[] { 0.0, 0.5 };

            var result = ChannelInterpolator.Interpolate(srcTs, srcVals, targetTs);

            Assert.Equal(100.0, result[0], 6);
            Assert.Equal(100.0, result[1], 6);
        }

        [Fact]
        public void Interpolate_AfterLastTimestamp_ClampsToLast()
        {
            var srcTs = new[] { 0.0, 1.0 };
            var srcVals = new[] { 50.0, 75.0 };
            var targetTs = new[] { 1.5, 2.0 };

            var result = ChannelInterpolator.Interpolate(srcTs, srcVals, targetTs);

            Assert.Equal(75.0, result[0], 6);
            Assert.Equal(75.0, result[1], 6);
        }

        [Fact]
        public void Interpolate_WithScaleFactor_AppliesMultiplier()
        {
            var srcTs = new[] { 0.0, 1.0 };
            var srcVals = new[] { 10.0, 20.0 }; // m/s
            var targetTs = new[] { 0.0, 0.5, 1.0 };

            var result = ChannelInterpolator.Interpolate(srcTs, srcVals, targetTs, scaleFactor: 3.6);

            Assert.Equal(36.0, result[0], 6);   // 10 * 3.6
            Assert.Equal(54.0, result[1], 6);   // 15 * 3.6
            Assert.Equal(72.0, result[2], 6);   // 20 * 3.6
        }

        [Fact]
        public void Interpolate_EmptySource_ReturnsZeros()
        {
            var result = ChannelInterpolator.Interpolate(
                Array.Empty<double>(),
                Array.Empty<double>(),
                new[] { 0.0, 1.0, 2.0 });

            Assert.Equal(3, result.Length);
            Assert.All(result, v => Assert.Equal(0.0, v));
        }

        [Fact]
        public void Interpolate_EmptyTarget_ReturnsEmpty()
        {
            var result = ChannelInterpolator.Interpolate(
                new[] { 0.0, 1.0 },
                new[] { 10.0, 20.0 },
                Array.Empty<double>());

            Assert.Empty(result);
        }

        [Fact]
        public void Interpolate_MismatchedLengths_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => ChannelInterpolator.Interpolate(
                    new[] { 0.0, 1.0 },
                    new[] { 10.0 }, // one fewer
                    new[] { 0.5 }));
        }

        [Fact]
        public void Interpolate_SingleSourcePoint_ClampsEverywhere()
        {
            var srcTs = new[] { 1.0 };
            var srcVals = new[] { 42.0 };
            var targetTs = new[] { 0.0, 1.0, 2.0 };

            var result = ChannelInterpolator.Interpolate(srcTs, srcVals, targetTs);

            // Single source → all clamp to that value
            Assert.Equal(42.0, result[0], 6);
            Assert.Equal(42.0, result[1], 6);
            Assert.Equal(42.0, result[2], 6);
        }

        // ── Interpolate: multi-rate real-world scenario ────────────────

        [Fact]
        public void Interpolate_100HzTo50Hz_AlignsProperly()
        {
            // 100Hz source: 11 samples over 0.1s
            var srcTs = new double[11];
            var srcVals = new double[11];
            for (int i = 0; i < 11; i++)
            {
                srcTs[i] = i * 0.01;
                srcVals[i] = i * 10.0; // linear ramp
            }

            // 50Hz target: 6 samples over 0.1s
            var targetTs = new[] { 0.0, 0.02, 0.04, 0.06, 0.08, 0.10 };

            var result = ChannelInterpolator.Interpolate(srcTs, srcVals, targetTs);

            Assert.Equal(0.0, result[0], 6);
            Assert.Equal(20.0, result[1], 6);
            Assert.Equal(40.0, result[2], 6);
            Assert.Equal(60.0, result[3], 6);
            Assert.Equal(80.0, result[4], 6);
            Assert.Equal(100.0, result[5], 6);
        }

        [Fact]
        public void Interpolate_7HzTo50Hz_UpsamplesCorrectly()
        {
            // 7Hz source: 8 samples over 1.0s
            var srcTs = ChannelInterpolator.EstimateChannelTimestamps(8, 0.0, 1.0);
            var srcVals = new double[8];
            for (int i = 0; i < 8; i++)
                srcVals[i] = i * 10.0;

            // 50Hz target
            var targetTs = ChannelInterpolator.CreateTimeGrid(0.0, 1.0, 50);

            var result = ChannelInterpolator.Interpolate(srcTs, srcVals, targetTs);

            // First and last values should match source edges
            Assert.Equal(0.0, result[0], 6);
            Assert.Equal(70.0, result[^1], 6);

            // Middle values should be smoothly interpolated (monotonically increasing)
            for (int i = 1; i < result.Length; i++)
            {
                Assert.True(result[i] >= result[i - 1],
                    $"Result should be monotonically increasing: result[{i}]={result[i]} < result[{i - 1}]={result[i - 1]}");
            }
        }

        // ── InterpolateMultiColumn ─────────────────────────────────────

        [Fact]
        public void InterpolateMultiColumn_InterpolatesEachColumn()
        {
            var srcTs = new[] { 0.0, 1.0, 2.0 };
            var col0 = new[] { 10.0, 20.0, 30.0 };
            var col1 = new[] { 100.0, 200.0, 300.0 };
            var sourceColumns = new[] { col0, col1 };

            var targetTs = new[] { 0.5, 1.5 };

            var result = ChannelInterpolator.InterpolateMultiColumn(srcTs, sourceColumns, targetTs);

            Assert.Equal(2, result.Length); // 2 columns
            Assert.Equal(15.0, result[0][0], 6);
            Assert.Equal(25.0, result[0][1], 6);
            Assert.Equal(150.0, result[1][0], 6);
            Assert.Equal(250.0, result[1][1], 6);
        }

        [Fact]
        public void InterpolateMultiColumn_WithScaleFactor_AppliedToAll()
        {
            var srcTs = new[] { 0.0, 1.0 };
            var col0 = new[] { 10.0, 20.0 };
            var col1 = new[] { 30.0, 40.0 };
            var sourceColumns = new[] { col0, col1 };

            var targetTs = new[] { 0.5 };

            var result = ChannelInterpolator.InterpolateMultiColumn(srcTs, sourceColumns, targetTs, scaleFactor: 2.0);

            Assert.Equal(30.0, result[0][0], 6); // (10+20)/2 * 2
            Assert.Equal(70.0, result[1][0], 6); // (30+40)/2 * 2
        }

        [Fact]
        public void InterpolateMultiColumn_FourTireTemps_CorrectShape()
        {
            // Simulate 4 tire temperature columns at 7Hz over 1s
            var srcTs = ChannelInterpolator.EstimateChannelTimestamps(8, 0.0, 1.0);
            var columns = new double[4][];
            for (int c = 0; c < 4; c++)
            {
                columns[c] = new double[8];
                for (int i = 0; i < 8; i++)
                    columns[c][i] = 80.0 + c * 5.0; // constant per tire
            }

            var targetTs = ChannelInterpolator.CreateTimeGrid(0.0, 1.0, 50);
            var result = ChannelInterpolator.InterpolateMultiColumn(srcTs, columns, targetTs);

            Assert.Equal(4, result.Length);
            // Constant source → constant output
            Assert.All(result[0], v => Assert.Equal(80.0, v, 6));
            Assert.All(result[1], v => Assert.Equal(85.0, v, 6));
            Assert.All(result[2], v => Assert.Equal(90.0, v, 6));
            Assert.All(result[3], v => Assert.Equal(95.0, v, 6));
        }
    }
}
