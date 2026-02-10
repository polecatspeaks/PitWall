using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public class AgentConfigClient : IAgentConfigClient
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly HttpClient _httpClient;

        public AgentConfigClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<AgentConfigDto> GetConfigAsync(CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync("/agent/config", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<AgentConfigDto>(json, Options);

            return result ?? new AgentConfigDto();
        }

        public async Task<AgentConfigDto> UpdateConfigAsync(AgentConfigUpdateDto update, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(update, Options);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync("/agent/config", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<AgentConfigDto>(responseJson, Options);

            return result ?? new AgentConfigDto();
        }

        public async Task<DiscoveryResultDto> RunDiscoveryAsync(CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync("/agent/llm/discover", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<DiscoveryResultDto>(json, Options);

            return result ?? new DiscoveryResultDto();
        }
    }
}
