using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public class AgentQueryClient : IAgentQueryClient
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly ILogger<AgentQueryClient> _logger;

        public AgentQueryClient(HttpClient httpClient, ILogger<AgentQueryClient>? logger = null)
        {
            _httpClient = httpClient;
            _logger = logger ?? NullLogger<AgentQueryClient>.Instance;
        }

        public async Task<AgentResponseDto> SendQueryAsync(string query, CancellationToken cancellationToken)
        {
            var request = new AgentRequestDto
            {
                Query = query
            };

            try
            {
                _logger.LogDebug("Sending agent query.");
                var json = JsonSerializer.Serialize(request, Options);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/agent/query", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<AgentResponseDto>(responseJson, Options);

                _logger.LogDebug("Agent query completed.");
                return result ?? new AgentResponseDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent query failed.");
                throw;
            }
        }
    }
}
