using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using PitWall.Core.Models;
using PitWall.Core.Storage;
using Xunit;

namespace PitWall.Tests.Integration
{
    /// <summary>
    /// Integration tests for the /api/recommend endpoint.
    /// These tests verify that the API correctly wires StrategyEngine
    /// and returns recommendations based on telemetry data.
    /// </summary>
    public class ApiRecommendationTests : IDisposable
    {
        private HttpClient _client;
        private ITelemetryWriter _writer;

        public ApiRecommendationTests()
        {
            // NOTE: We use InMemoryTelemetryWriter for testing.
            // In a real scenario, you would use a test host or WebApplicationFactory.
            _writer = new InMemoryTelemetryWriter();
        }

        public void Dispose()
        {
            _client?.Dispose();
        }

        [Fact(Skip = "Requires WebApplicationFactory setup. Will implement after API scaffold.")]
        public async Task GetRecommend_ReturnsTyreWarningForOverheatSample()
        {
            // Arrange: Create a telemetry sample with overheated tyres
            var sessionId = "session-test-123";
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 100, new double[] { 115, 110, 112, 111 }, 50, 0, 0.5, 0)
            };
            _writer.WriteSamples(sessionId, samples);

            // Act: Call the API recommendation endpoint
            // GET /api/recommend?sessionId={sessionId}
            // Expected response: { "recommendation": "...", "confidence": 0.95, ... }

            // When implemented, this test should verify:
            // 1. The endpoint receives the session ID
            // 2. Fetches telemetry from writer
            // 3. Calls StrategyEngine.Evaluate()
            // 4. Returns JSON recommendation with tyre warning

            Assert.True(false, "Endpoint not yet implemented.");
        }

        [Fact(Skip = "Requires WebApplicationFactory setup.")]
        public async Task GetRecommend_ReturnsFuelProjection()
        {
            // Similar structure: test fuel projection rule
            var sessionId = "session-fuel-test";
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 200, new double[] { 80, 80, 80, 80 }, 5, 0, 0.8, 0)
            };
            _writer.WriteSamples(sessionId, samples);

            // When implemented, should return fuel projection: "Pit in ~1 lap"
            Assert.True(false, "Endpoint not yet implemented.");
        }
    }
}
