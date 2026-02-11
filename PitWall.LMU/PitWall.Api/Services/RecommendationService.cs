using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PitWall.Core.Models;
using PitWall.Core.Storage;
using PitWall.Strategy;

namespace PitWall.Api.Services
{
    /// <summary>
    /// Service that wires telemetry data with StrategyEngine to generate recommendations.
    /// </summary>
    public interface IRecommendationService
    {
        RecommendationResponse GetRecommendation(string sessionId, ITelemetryWriter writer);
    }

    public class RecommendationService : IRecommendationService
    {
        private readonly StrategyEngine _engine;
        private readonly ILogger<RecommendationService> _logger;

        public RecommendationService(ILogger<RecommendationService> logger, ILogger<StrategyEngine> strategyLogger)
        {
            _logger = logger;
            _engine = new StrategyEngine(strategyLogger);
        }

        public RecommendationResponse GetRecommendation(string sessionId, ITelemetryWriter writer)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("Session ID is required.", nameof(sessionId));

            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            // Retrieve the latest sample(s) for this session
            var samples = writer.GetSamples(sessionId);

            if (samples == null || samples.Count == 0)
            {
                _logger.LogDebug("No samples available for session {SessionId}.", sessionId);
                return new RecommendationResponse
                {
                    Recommendation = "No telemetry data available",
                    Confidence = 0.0,
                    SessionId = sessionId
                };
            }

            // Use the most recent sample
            var latestSample = samples[samples.Count - 1];
            _logger.LogDebug("Evaluating recommendation for session {SessionId}.", sessionId);

            // Evaluate using StrategyEngine
            var evaluation = _engine.EvaluateWithConfidence(latestSample);

            return new RecommendationResponse
            {
                Recommendation = evaluation.Recommendation,
                Confidence = evaluation.Confidence,
                SessionId = sessionId,
                Timestamp = latestSample.Timestamp,
                SpeedKph = latestSample.SpeedKph
            };
        }
    }
}
