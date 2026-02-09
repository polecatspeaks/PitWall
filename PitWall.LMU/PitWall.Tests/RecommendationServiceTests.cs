using System;
using System.Collections.Generic;
using PitWall.Api.Services;
using PitWall.Core.Models;
using PitWall.Core.Storage;
using Xunit;

namespace PitWall.Tests
{
    public class RecommendationServiceTests
    {
        [Fact]
        public void GetRecommendation_WithOverheatedTyres_ReturnsWarning()
        {
            // Arrange
            var service = new RecommendationService();
            var writer = new InMemoryTelemetryWriter();
            var sessionId = "test-session-1";
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 100, new double[] { 115, 110, 112, 111 }, 50, 0, 0.5, 0)
            };
            writer.WriteSamples(sessionId, samples);

            // Act
            var response = service.GetRecommendation(sessionId, writer);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(sessionId, response.SessionId);
            Assert.Contains("overheat", response.Recommendation, StringComparison.OrdinalIgnoreCase);
            Assert.True(response.Confidence > 0);
        }

        [Fact]
        public void GetRecommendation_WithLowFuel_ReturnsPitRecommendation()
        {
            // Arrange
            var service = new RecommendationService();
            var writer = new InMemoryTelemetryWriter();
            var sessionId = "test-session-fuel";
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(DateTime.UtcNow, 200, new double[] { 80, 80, 80, 80 }, 5, 0, 0.8, 0)
            };
            writer.WriteSamples(sessionId, samples);

            // Act
            var response = service.GetRecommendation(sessionId, writer);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(sessionId, response.SessionId);
            Assert.Contains("lap", response.Recommendation, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetRecommendation_WithNoData_ReturnsEmptyMessage()
        {
            // Arrange
            var service = new RecommendationService();
            var writer = new InMemoryTelemetryWriter();
            var sessionId = "nonexistent-session";

            // Act
            var response = service.GetRecommendation(sessionId, writer);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(sessionId, response.SessionId);
            Assert.Contains("No telemetry", response.Recommendation);
            Assert.Equal(0.0, response.Confidence);
        }

        [Fact]
        public void GetRecommendation_WithNullSessionId_ThrowsArgumentException()
        {
            // Arrange
            var service = new RecommendationService();
            var writer = new InMemoryTelemetryWriter();
            string? sessionId = null;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => service.GetRecommendation(sessionId!, writer));
        }

        [Fact]
        public void GetRecommendation_WithNullWriter_ThrowsArgumentNullException()
        {
            // Arrange
            var service = new RecommendationService();
            ITelemetryWriter? writer = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => service.GetRecommendation("session-1", writer!));
        }
    }
}
