using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PitWall.Api.Services;
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
        private readonly WebApplicationFactory<Program> _factory;
        private readonly InMemoryTelemetryWriter _writer;

        public ApiRecommendationTests()
        {
            _writer = new InMemoryTelemetryWriter();
            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll(typeof(ITelemetryWriter));
                        services.AddSingleton<ITelemetryWriter>(_writer);
                    });
                });
        }

        public void Dispose()
        {
            _factory.Dispose();
        }

        [Fact]
        public async Task GetRecommend_ReturnsTyreWarningForOverheatSample()
        {
            // Arrange: Create a telemetry sample with overheated tyres
            var sessionId = "session-test-123";
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 100, new double[] { 115, 110, 112, 111 }, 50, 0, 0.5, 0)
            };
            _writer.WriteSamples(sessionId, samples);
            var client = _factory.CreateClient();

            // Act: Call the API recommendation endpoint
            var response = await client.GetAsync($"/api/recommend?sessionId={sessionId}");
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<RecommendationResponse>();

            // Assert
            Assert.NotNull(payload);
            Assert.Equal(sessionId, payload!.SessionId);
            Assert.Contains("Tyre overheat", payload.Recommendation ?? string.Empty);
        }

        [Fact]
        public async Task GetRecommend_ReturnsFuelProjection()
        {
            // Similar structure: test fuel projection rule
            var sessionId = "session-fuel-test";
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 200, new double[] { 80, 80, 80, 80 }, 5, 0, 0.8, 0)
            };
            _writer.WriteSamples(sessionId, samples);
            var client = _factory.CreateClient();

            var response = await client.GetAsync($"/api/recommend?sessionId={sessionId}");
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<RecommendationResponse>();

            Assert.NotNull(payload);
            Assert.Equal(sessionId, payload!.SessionId);
            Assert.Contains("Plan pit stop", payload.Recommendation ?? string.Empty);
        }
    }
}
