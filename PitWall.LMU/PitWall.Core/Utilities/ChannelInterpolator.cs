using System;

namespace PitWall.Core.Utilities
{
    /// <summary>
    /// Provides linear interpolation for aligning multi-rate telemetry channels
    /// onto a uniform time grid. LMU records channels at different frequencies
    /// (e.g. GPS Speed at 100Hz, Throttle at 50Hz, Oil Temp at 7Hz), so raw
    /// row-index alignment produces incorrect data. This utility uses actual
    /// timestamp values and linear interpolation to produce properly aligned samples.
    /// </summary>
    public static class ChannelInterpolator
    {
        /// <summary>
        /// Creates a uniform time grid from <paramref name="startTime"/> to
        /// <paramref name="endTime"/> at the given <paramref name="frequencyHz"/>.
        /// </summary>
        /// <param name="startTime">Grid start time (seconds).</param>
        /// <param name="endTime">Grid end time (seconds, inclusive).</param>
        /// <param name="frequencyHz">Target frequency in Hz (default 50).</param>
        /// <returns>Array of uniformly-spaced time values.</returns>
        public static double[] CreateTimeGrid(double startTime, double endTime, int frequencyHz = 50)
        {
            if (frequencyHz <= 0)
                throw new ArgumentOutOfRangeException(nameof(frequencyHz), "Frequency must be positive.");
            if (endTime <= startTime)
                return Array.Empty<double>();

            double deltaT = 1.0 / frequencyHz;
            int numPoints = (int)Math.Ceiling((endTime - startTime) / deltaT) + 1;
            var grid = new double[numPoints];

            for (int i = 0; i < numPoints; i++)
            {
                grid[i] = startTime + i * deltaT;
            }

            return grid;
        }

        /// <summary>
        /// Generates timestamps for a channel's samples based on row index and native frequency.
        /// Each sample is assumed to be taken at <c>startTime + index / nativeFrequencyHz</c>.
        /// </summary>
        /// <param name="sampleCount">Number of samples in the channel.</param>
        /// <param name="startTime">Session start time (seconds, from GPS Time).</param>
        /// <param name="endTime">Session end time (seconds, from GPS Time).</param>
        /// <returns>Array of estimated timestamps for each sample.</returns>
        public static double[] EstimateChannelTimestamps(int sampleCount, double startTime, double endTime)
        {
            if (sampleCount <= 0)
                return Array.Empty<double>();
            if (sampleCount == 1)
                return new[] { startTime };

            var timestamps = new double[sampleCount];
            double duration = endTime - startTime;

            for (int i = 0; i < sampleCount; i++)
            {
                timestamps[i] = startTime + (double)i / (sampleCount - 1) * duration;
            }

            return timestamps;
        }

        /// <summary>
        /// Interpolates channel values onto a target time grid using linear interpolation.
        /// The source timestamps and values must be sorted by time (ascending).
        /// </summary>
        /// <param name="sourceTimestamps">Timestamps of source samples (ascending).</param>
        /// <param name="sourceValues">Values at each source timestamp.</param>
        /// <param name="targetTimestamps">Target time grid to interpolate onto.</param>
        /// <param name="scaleFactor">Multiplier applied after interpolation (e.g. 3.6 for m/s → km/h).</param>
        /// <returns>Interpolated values at each target timestamp.</returns>
        public static double[] Interpolate(
            double[] sourceTimestamps,
            double[] sourceValues,
            double[] targetTimestamps,
            double scaleFactor = 1.0)
        {
            if (sourceTimestamps.Length != sourceValues.Length)
                throw new ArgumentException("Source timestamps and values must have the same length.");

            if (sourceTimestamps.Length == 0 || targetTimestamps.Length == 0)
                return new double[targetTimestamps.Length];

            var result = new double[targetTimestamps.Length];

            // Use a scan pointer for efficiency since both arrays are sorted
            int srcIdx = 0;

            for (int i = 0; i < targetTimestamps.Length; i++)
            {
                double t = targetTimestamps[i];

                // Clamp to edges
                if (t <= sourceTimestamps[0])
                {
                    result[i] = sourceValues[0] * scaleFactor;
                    continue;
                }

                if (t >= sourceTimestamps[^1])
                {
                    result[i] = sourceValues[^1] * scaleFactor;
                    continue;
                }

                // Advance scan pointer to bracket the target time
                while (srcIdx < sourceTimestamps.Length - 2 && sourceTimestamps[srcIdx + 1] < t)
                {
                    srcIdx++;
                }

                // Linear interpolation: y = y0 + (y1 - y0) * (t - t0) / (t1 - t0)
                double t0 = sourceTimestamps[srcIdx];
                double t1 = sourceTimestamps[srcIdx + 1];
                double y0 = sourceValues[srcIdx];
                double y1 = sourceValues[srcIdx + 1];

                double denom = t1 - t0;
                double interpolated;
                if (denom == 0)
                {
                    interpolated = y0;
                }
                else
                {
                    double frac = (t - t0) / denom;
                    interpolated = y0 + (y1 - y0) * frac;
                }

                result[i] = interpolated * scaleFactor;
            }

            return result;
        }

        /// <summary>
        /// Interpolates multi-column channel data (e.g. 4 tire temperatures)
        /// onto a target time grid.
        /// </summary>
        /// <param name="sourceTimestamps">Timestamps of source samples (ascending).</param>
        /// <param name="sourceColumns">Array of value columns (each column has one value per timestamp).</param>
        /// <param name="targetTimestamps">Target time grid to interpolate onto.</param>
        /// <param name="scaleFactor">Multiplier applied after interpolation.</param>
        /// <returns>Array of interpolated value columns.</returns>
        public static double[][] InterpolateMultiColumn(
            double[] sourceTimestamps,
            double[][] sourceColumns,
            double[] targetTimestamps,
            double scaleFactor = 1.0)
        {
            var result = new double[sourceColumns.Length][];
            for (int col = 0; col < sourceColumns.Length; col++)
            {
                result[col] = Interpolate(sourceTimestamps, sourceColumns[col], targetTimestamps, scaleFactor);
            }
            return result;
        }
    }
}
