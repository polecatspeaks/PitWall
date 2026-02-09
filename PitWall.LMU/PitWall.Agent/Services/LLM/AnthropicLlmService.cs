using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PitWall.Agent.Models;

namespace PitWall.Agent.Services.LLM
{
    public class AnthropicLlmService : ILLMService
    {
        private const string AnthropicVersion = "2023-06-01";

        private readonly HttpClient _httpClient;
        private readonly AgentOptions _options;
        private readonly ILogger<AnthropicLlmService> _logger;
        private bool _isAvailable;

        public bool IsEnabled => _options.EnableLLM;
        public bool IsAvailable => _isAvailable;

        public AnthropicLlmService(HttpClient httpClient, AgentOptions options, ILogger<AnthropicLlmService> logger)
        {
            _httpClient = httpClient;
            _options = options;
            _logger = logger;

            _httpClient.BaseAddress = new Uri(_options.AnthropicEndpoint);
            _httpClient.Timeout = TimeSpan.FromMilliseconds(_options.LLMTimeoutMs);

            if (!string.IsNullOrWhiteSpace(_options.AnthropicApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.AnthropicApiKey);
                _httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
                _isAvailable = true;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(_options.AnthropicApiKey))
            {
                _isAvailable = false;
                return false;
            }

            try
            {
                var response = await _httpClient.GetAsync("/v1/models");
                _isAvailable = response.IsSuccessStatusCode;
                return _isAvailable;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Anthropic connection test failed");
                _isAvailable = false;
                return false;
            }
        }

        public async Task<AgentResponse> QueryAsync(string query, RaceContext context)
        {
            if (string.IsNullOrWhiteSpace(_options.AnthropicApiKey))
            {
                return new AgentResponse
                {
                    Answer = "Anthropic API key not configured",
                    Source = "LLM",
                    Success = false,
                    Error = "Missing Anthropic API key"
                };
            }

            var startTime = DateTime.UtcNow;

            try
            {
                var systemPrompt = LLMContextBuilder.BuildSystemPrompt(context);

                var request = new
                {
                    model = _options.AnthropicModel,
                    max_tokens = 500,
                    system = systemPrompt,
                    messages = new[]
                    {
                        new { role = "user", content = query }
                    }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/v1/messages", content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                var answer = doc.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString() ?? "No response from Anthropic";

                _isAvailable = true;

                return new AgentResponse
                {
                    Answer = answer,
                    Source = "LLM",
                    Confidence = 0.7,
                    ResponseTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    Success = true
                };
            }
            catch (TaskCanceledException)
            {
                return new AgentResponse
                {
                    Answer = "Anthropic request timed out",
                    Source = "LLM",
                    Success = false,
                    Error = "Request timed out",
                    ResponseTimeMs = _options.LLMTimeoutMs
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying Anthropic");
                _isAvailable = false;

                return new AgentResponse
                {
                    Answer = "Error querying LLM",
                    Source = "LLM",
                    Success = false,
                    Error = ex.Message,
                    ResponseTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds
                };
            }
        }
    }
}
