using System;
using System.Collections.Generic;
using System.Linq;
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
    public class AgentConfigClient : IAgentConfigClient
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly HttpClient _httpClient;
        private readonly ILogger<AgentConfigClient> _logger;

        public AgentConfigClient(HttpClient httpClient, ILogger<AgentConfigClient>? logger = null)
        {
            _httpClient = httpClient;
            _logger = logger ?? NullLogger<AgentConfigClient>.Instance;
        }

        public async Task<AgentConfigDto> GetConfigAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Loading agent config from {BaseAddress}", _httpClient.BaseAddress);
                var response = await _httpClient.GetAsync("/agent/config", cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<AgentConfigDto>(json, Options);

                _logger.LogDebug("Agent config loaded.");
                return result ?? new AgentConfigDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load agent config.");
                throw;
            }
        }

        public async Task<AgentConfigDto> UpdateConfigAsync(AgentConfigUpdateDto update, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Saving agent config.");
                var json = JsonSerializer.Serialize(update, Options);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync("/agent/config", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<AgentConfigDto>(responseJson, Options);

                _logger.LogDebug("Agent config saved.");
                return result ?? new AgentConfigDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save agent config.");
                throw;
            }
        }

        public async Task<IReadOnlyList<string>> DiscoverEndpointsAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Starting LLM endpoint discovery.");
                var response = await _httpClient.GetAsync("/agent/llm/discover", cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<DiscoveryResponse>(json, Options);
                var endpoints = result?.Endpoints ?? Array.Empty<string>();
                _logger.LogDebug("LLM endpoint discovery finished with {Count} endpoints.", endpoints.Length);
                return endpoints;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM endpoint discovery failed.");
                throw;
            }
        }

        public async Task<AgentHealthDto> CheckHealthAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Checking agent health.");
                var response = await _httpClient.GetAsync("/agent/health", cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<AgentHealthDto>(json, Options);

                _logger.LogDebug("Agent health check completed.");
                return result ?? new AgentHealthDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent health check failed.");
                throw;
            }
        }

        public async Task<LlmTestDto> TestLlmAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Testing LLM connection.");
                var response = await _httpClient.GetAsync("/agent/llm/test", cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<LlmTestDto>(json, Options);

                _logger.LogDebug("LLM connection test completed.");
                return result ?? new LlmTestDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM connection test failed.");
                throw;
            }
        }

        private record DiscoveryResponse(string[] Endpoints);
    }
}
