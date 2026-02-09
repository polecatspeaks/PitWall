using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PitWall.Agent.Models;

namespace PitWall.Agent.Services.LLM
{
    public class OllamaLLMService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly AgentOptions _options;
        private readonly ILogger<OllamaLLMService> _logger;
        private bool _isAvailable;

        public bool IsEnabled => _options.EnableLLM;
        public bool IsAvailable => _isAvailable;

        public OllamaLLMService(
            HttpClient httpClient,
            AgentOptions options,
            ILogger<OllamaLLMService> logger)
        {
            _httpClient = httpClient;
            _options = options;
            _logger = logger;

            _httpClient.BaseAddress = new Uri(_options.LLMEndpoint);
            _httpClient.Timeout = TimeSpan.FromMilliseconds(_options.LLMTimeoutMs);

            _ = TestConnectionAsync();
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Testing connection to Ollama at {Endpoint}", _options.LLMEndpoint);

                var response = await _httpClient.GetAsync("/api/tags");
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Ollama server connected successfully");
                    _isAvailable = true;
                    return true;
                }

                _logger.LogWarning("Ollama server returned {Status}", response.StatusCode);
                _isAvailable = false;
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Ollama server");
                _isAvailable = false;
                return false;
            }
        }

        public async Task<AgentResponse> QueryAsync(string query, RaceContext context)
        {
            if (!IsAvailable)
            {
                return new AgentResponse
                {
                    Answer = "LLM service not available",
                    Source = "LLM",
                    Success = false,
                    Error = "Ollama server not connected"
                };
            }

            var startTime = DateTime.UtcNow;

            try
            {
                var systemPrompt = LLMContextBuilder.BuildSystemPrompt(context);
                var fullPrompt = $"{systemPrompt}\n\nDriver Question: {query}";

                var request = new
                {
                    model = _options.LLMModel,
                    prompt = fullPrompt,
                    stream = false,
                    options = new
                    {
                        temperature = 0.7,
                        max_tokens = 500
                    }
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/generate", content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaResponse>(responseJson);

                var responseTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogInformation("LLM response received in {Ms}ms", responseTime);

                return new AgentResponse
                {
                    Answer = result?.response ?? "No response from LLM",
                    Source = "LLM",
                    Confidence = 0.7,
                    ResponseTimeMs = responseTime,
                    Success = true
                };
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("LLM request timed out after {Timeout}ms", _options.LLMTimeoutMs);

                return new AgentResponse
                {
                    Answer = "LLM request timed out",
                    Source = "LLM",
                    Success = false,
                    Error = "Request timed out",
                    ResponseTimeMs = _options.LLMTimeoutMs
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying Ollama");
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

        private class OllamaResponse
        {
            public string response { get; set; } = string.Empty;
            public bool done { get; set; }
        }
    }
}
