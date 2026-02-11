using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public class RecommendationClient : IRecommendationClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RecommendationClient> _logger;

        public RecommendationClient(HttpClient httpClient, ILogger<RecommendationClient>? logger = null)
        {
            _httpClient = httpClient;
            _logger = logger ?? NullLogger<RecommendationClient>.Instance;
        }

        public async Task<RecommendationDto> GetRecommendationAsync(string sessionId, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Requesting recommendation for session {SessionId}", sessionId);
                var response = await _httpClient.GetAsync($"/api/recommend?sessionId={Uri.EscapeDataString(sessionId)}", cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = RecommendationParser.Parse(json);
                _logger.LogDebug("Recommendation received for session {SessionId}", sessionId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch recommendation for session {SessionId}", sessionId);
                throw;
            }
        }
    }
}
