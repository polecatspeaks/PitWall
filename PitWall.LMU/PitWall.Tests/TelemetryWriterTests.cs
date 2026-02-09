using System;
using System.Collections.Generic;
using System.Linq;
using PitWall.Core.Models;
using PitWall.Core.Storage;
using Xunit;

namespace PitWall.Tests
{
    public class TelemetryWriterTests
    {
        [Fact]
        public void WriteSamples_StoresSamplesSuccessfully()
        {
            var writer = new InMemoryTelemetryWriter();
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 100, new double[] { 80, 80, 80, 80 }, 50, 0, 0.5, 0),
                new TelemetrySample(DateTime.UtcNow.AddSeconds(1), 105, new double[] { 82, 81, 80, 83 }, 49, 0.1, 0.5, 0)
            };

            writer.WriteSamples("session-123", samples);

            var stored = writer.GetSamples("session-123");
            Assert.NotEmpty(stored);
            Assert.Equal(2, stored.Count);
        }

        [Fact]
        public void WriteSamples_ReturnsZeroForEmptySession()
        {
            var writer = new InMemoryTelemetryWriter();

            var stored = writer.GetSamples("nonexistent");

            Assert.Empty(stored);
        }
    }
}
