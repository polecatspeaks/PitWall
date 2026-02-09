using System;

namespace PitWall.Api.Services
{
    /// <summary>
    /// Response model for strategy recommendations.
    /// </summary>
    public class RecommendationResponse
    {
        public string? SessionId { get; set; }
        public string? Recommendation { get; set; }
        public double Confidence { get; set; }
        public DateTime? Timestamp { get; set; }
        public double? SpeedKph { get; set; }
    }
}
