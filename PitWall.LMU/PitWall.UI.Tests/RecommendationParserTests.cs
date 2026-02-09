using PitWall.UI.Models;
using PitWall.UI.Services;
using Xunit;

namespace PitWall.UI.Tests
{
    public class RecommendationParserTests
    {
        [Fact]
        public void Parse_ValidPayload_MapsFields()
        {
            var json = "{\"sessionId\":\"s1\",\"recommendation\":\"Box this lap\",\"confidence\":0.85,\"timestamp\":\"2026-02-09T12:00:00Z\",\"speedKph\":240.5}";

            var result = RecommendationParser.Parse(json);

            Assert.Equal("s1", result.SessionId);
            Assert.Equal("Box this lap", result.Recommendation);
            Assert.Equal(0.85, result.Confidence);
            Assert.Equal(240.5, result.SpeedKph);
        }

        [Fact]
        public void Parse_MissingFields_UsesDefaults()
        {
            var json = "{}";

            var result = RecommendationParser.Parse(json);

            Assert.Equal(string.Empty, result.Recommendation);
            Assert.Equal(0.0, result.Confidence);
            Assert.Null(result.SpeedKph);
        }
    }
}
