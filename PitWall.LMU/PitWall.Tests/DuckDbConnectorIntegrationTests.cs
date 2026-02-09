using System;
using System.Collections.Generic;
using System.IO;
using PitWall.Core.Models;
using PitWall.Core.Storage;
using Xunit;

namespace PitWall.Tests
{
    public class DuckDbConnectorIntegrationTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly DuckDbConnector _connector;

        public DuckDbConnectorIntegrationTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test-pitwall-{Guid.NewGuid()}.db");
            _connector = new DuckDbConnector(_testDbPath);
        }

        public void Dispose()
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }

        [Fact]
        public void DuckDbConnector_CreatesSchemaOnEnsureSchema()
        {
            _connector.EnsureSchema();
            // If this completes without exception, schema was created successfully.
            Assert.True(File.Exists(_testDbPath));
        }

        [Fact]
        public void DuckDbConnector_InsertsSamplesSuccessfully()
        {
            _connector.EnsureSchema();

            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 100, new double[] { 80, 80, 80, 80 }, 50, 0, 0.5, 0),
                new TelemetrySample(DateTime.UtcNow.AddSeconds(1), 105, new double[] { 82, 81, 80, 83 }, 49, 0.1, 0.5, 0)
            };

            _connector.InsertSamples("test-session-123", samples);

            // If this completes without exception, inserts succeeded.
            Assert.True(File.Exists(_testDbPath));
        }

        [Fact]
        public void DuckDbConnector_HandlesEmptySampleList()
        {
            _connector.EnsureSchema();
            var samples = new List<TelemetrySample>();

            // Should not throw; empty inserts are safe.
            _connector.InsertSamples("test-session-empty", samples);

            Assert.True(File.Exists(_testDbPath));
        }
    }
}
