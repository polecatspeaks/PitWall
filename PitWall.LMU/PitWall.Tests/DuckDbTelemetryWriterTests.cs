using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
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
            public string DatabasePath { get; set; } = string.Empty;
            public string? LastSessionId { get; private set; }

            public void EnsureSchema()
            {
                SchemaEnsured = true;
            }

            public void InsertSamples(string sessionId, IEnumerable<TelemetrySample> samples)
            {
                LastSessionId = sessionId;
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
        public void Constructor_ThrowsArgumentNullException_WhenConnectorIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new DuckDbTelemetryWriter(null!));
        }

        [Fact]
        public void Constructor_AcceptsNullLogger()
        {
            var mock = new MockConnector();
            var writer = new DuckDbTelemetryWriter(mock, null);
            Assert.NotNull(writer);
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
            Assert.Equal("session-1", mock.LastSessionId);
        }

        [Fact]
        public void WriteSamples_ThrowsArgumentNullException_WhenSamplesIsNull()
        {
            var mock = new MockConnector();
            var writer = new DuckDbTelemetryWriter(mock);

            Assert.Throws<ArgumentNullException>(() => writer.WriteSamples("session-1", null!));
        }

        [Fact]
        public void WriteSamples_HandlesMultipleSamples()
        {
            var mock = new MockConnector();
            var writer = new DuckDbTelemetryWriter(mock);
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 100, new double[] { 80, 80, 80, 80 }, 50, 0, 0.5, 0),
                new TelemetrySample(DateTime.UtcNow.AddSeconds(1), 105, new double[] { 82, 81, 80, 83 }, 49, 0.1, 0.55, 0.05),
                new TelemetrySample(DateTime.UtcNow.AddSeconds(2), 110, new double[] { 84, 83, 82, 85 }, 48, 0.2, 0.6, 0.1)
            };

            writer.WriteSamples("session-2", samples);

            Assert.Equal(3, mock.Inserted.Count);
            Assert.Equal("session-2", mock.LastSessionId);
        }

        [Fact]
        public void WriteSamples_HandlesEmptyList()
        {
            var mock = new MockConnector();
            var writer = new DuckDbTelemetryWriter(mock);
            var samples = new List<TelemetrySample>();

            writer.WriteSamples("session-3", samples);

            Assert.Empty(mock.Inserted);
        }

        [Fact]
        public void GetSamples_ThrowsArgumentException_WhenSessionIdIsEmpty()
        {
            var mock = new MockConnector();
            var writer = new DuckDbTelemetryWriter(mock);

            Assert.Throws<ArgumentException>(() => writer.GetSamples(string.Empty));
        }

        [Fact]
        public void GetSamples_ThrowsArgumentException_WhenSessionIdIsWhitespace()
        {
            var mock = new MockConnector();
            var writer = new DuckDbTelemetryWriter(mock);

            Assert.Throws<ArgumentException>(() => writer.GetSamples("   "));
        }

        [Fact]
        public void GetSamples_ReturnsEmptyList_WhenSessionIdIsNotNumeric()
        {
            var mock = new MockConnector();
            mock.DatabasePath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.db");
            var writer = new DuckDbTelemetryWriter(mock);

            var result = writer.GetSamples("not-a-number");

            Assert.Empty(result);
        }

        [Fact]
        public void WriteSamples_PassesCorrectSessionIdToConnector()
        {
            var mock = new MockConnector();
            var writer = new DuckDbTelemetryWriter(mock);
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 100, new double[] { 80, 80, 80, 80 }, 50, 0, 0.5, 0)
            };

            writer.WriteSamples("test-session-123", samples);

            Assert.Equal("test-session-123", mock.LastSessionId);
        }

        [Fact]
        public void WriteSamples_PreservesSampleData()
        {
            var mock = new MockConnector();
            var writer = new DuckDbTelemetryWriter(mock);
            var timestamp = DateTime.UtcNow;
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(timestamp, 123.45, new double[] { 85.5, 86.5, 87.5, 88.5 }, 45.5, 0.75, 0.85, 0.15)
            };

            writer.WriteSamples("session-data", samples);

            Assert.Single(mock.Inserted);
            var inserted = mock.Inserted[0];
            Assert.Equal(timestamp, inserted.Timestamp);
            Assert.Equal(123.45, inserted.SpeedKph);
            Assert.Equal(45.5, inserted.FuelLiters);
            Assert.Equal(0.75, inserted.Brake);
            Assert.Equal(0.85, inserted.Throttle);
            Assert.Equal(0.15, inserted.Steering);
            Assert.Equal(4, inserted.TyreTempsC.Length);
            Assert.Equal(85.5, inserted.TyreTempsC[0]);
            Assert.Equal(86.5, inserted.TyreTempsC[1]);
            Assert.Equal(87.5, inserted.TyreTempsC[2]);
            Assert.Equal(88.5, inserted.TyreTempsC[3]);
        }
    }
}
