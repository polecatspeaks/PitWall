using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public class RecommendationClient : IRecommendationClient
    {
        private readonly HttpClient _httpClient;

        public RecommendationClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<RecommendationDto> GetRecommendationAsync(string sessionId, CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync($"/api/recommend?sessionId={Uri.EscapeDataString(sessionId)}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return RecommendationParser.Parse(json);
        }
    }
}
