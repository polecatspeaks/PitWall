using System;
using System.Collections.Generic;
using PitWall.Core.Models;
using PitWall.Core.Storage;
using Xunit;

namespace PitWall.Tests
{
    public class DuckDbTelemetryWriterTests
    {
        private class MockConnector : IDuckDbConnector
        {
            public bool SchemaEnsured { get; private set; }
            public List<TelemetrySample> Inserted { get; } = new List<TelemetrySample>();

            public void EnsureSchema()
            {
                SchemaEnsured = true;
            }

            public void InsertSamples(string sessionId, IEnumerable<TelemetrySample> samples)
            {
                Inserted.AddRange(samples);
            }
        }

        [Fact]
        public void Constructor_EnsuresSchema()
        {
            var mock = new MockConnector();
            var writer = new DuckDbTelemetryWriter(mock);
            Assert.True(mock.SchemaEnsured);
        }

        [Fact]
        public void WriteSamples_InvokesConnectorInsert()
        {
            var mock = new MockConnector();
            var writer = new DuckDbTelemetryWriter(mock);
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 100, new double[] { 80,80,80,80 }, 50, 0, 0.5, 0)
            };

            writer.WriteSamples("session-1", samples);

            Assert.Single(mock.Inserted);
        }
    }
}
