using System.Text.Json;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public static class RecommendationParser
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static RecommendationDto Parse(string json)
        {
            var dto = JsonSerializer.Deserialize<RecommendationDto>(json, Options)
                      ?? new RecommendationDto();

            if (string.IsNullOrWhiteSpace(dto.Recommendation))
            {
                dto.Recommendation = string.Empty;
            }

            return dto;
        }
    }
}
