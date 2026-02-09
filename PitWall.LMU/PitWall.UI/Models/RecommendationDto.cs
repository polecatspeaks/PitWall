using System;

namespace PitWall.UI.Models
{
    public class RecommendationDto
    {
        public string? SessionId { get; set; }
        public string? Recommendation { get; set; }
        public double Confidence { get; set; }
        public DateTime? Timestamp { get; set; }
        public double? SpeedKph { get; set; }
    }
}
