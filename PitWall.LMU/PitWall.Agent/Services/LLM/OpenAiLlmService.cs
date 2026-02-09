using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PitWall.Agent.Models;

namespace PitWall.Agent.Services.LLM
{
    public class OpenAiLlmService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly AgentOptions _options;
        private readonly ILogger<OpenAiLlmService> _logger;
        private bool _isAvailable;

        public bool IsEnabled => _options.EnableLLM;
        public bool IsAvailable => _isAvailable;

        public OpenAiLlmService(HttpClient httpClient, AgentOptions options, ILogger<OpenAiLlmService> logger)
        {
            _httpClient = httpClient;
            _options = options;
            _logger = logger;

            _httpClient.BaseAddress = new Uri(_options.OpenAIEndpoint);
            _httpClient.Timeout = TimeSpan.FromMilliseconds(_options.LLMTimeoutMs);

            if (!string.IsNullOrWhiteSpace(_options.OpenAIApiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _options.OpenAIApiKey);
                _isAvailable = true;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(_options.OpenAIApiKey))
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
                _logger.LogWarning(ex, "OpenAI connection test failed");
                _isAvailable = false;
                return false;
            }
        }

        public async Task<AgentResponse> QueryAsync(string query, RaceContext context)
        {
            if (string.IsNullOrWhiteSpace(_options.OpenAIApiKey))
            {
                return new AgentResponse
                {
                    Answer = "OpenAI API key not configured",
                    Source = "LLM",
                    Success = false,
                    Error = "Missing OpenAI API key"
                };
            }

            var startTime = DateTime.UtcNow;

            try
            {
                var systemPrompt = LLMContextBuilder.BuildSystemPrompt(context);

                var request = new
                {
                    model = _options.OpenAIModel,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = query }
                    },
                    temperature = 0.7,
                    max_tokens = 500
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/v1/chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                var answer = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "No response from OpenAI";

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
                    Answer = "OpenAI request timed out",
                    Source = "LLM",
                    Success = false,
                    Error = "Request timed out",
                    ResponseTimeMs = _options.LLMTimeoutMs
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying OpenAI");
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
