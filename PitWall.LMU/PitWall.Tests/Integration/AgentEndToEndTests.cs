using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PitWall.Agent.Models;
using PitWall.Agent.Services;
using PitWall.Agent.Services.LLM;
using PitWall.Core.Models;
using PitWall.Core.Storage;
using Xunit;

namespace PitWall.Tests.Integration
{
    /// <summary>
    /// End-to-end verification of all 5 agent endpoints.
    /// Covers happy paths, error states, and round-trip persistence.
    /// Acceptance criteria for issue #24.
    /// </summary>
    public class AgentEndToEndTests
    {
        #region /agent/health

        [Fact]
        public async Task Health_LlmEnabled_ReportsAvailableWithProviderDetails()
        {
            var llmService = new TestLlmService(isEnabled: true, isAvailable: true);
            using var factory = CreateFactory(enableLlm: false, llmService: llmService);
            using var client = factory.CreateClient();

            // Enable LLM via config PUT (since AgentOptions is read eagerly in Main)
            var update = new AgentConfigUpdate
            {
                EnableLLM = true,
                LLMProvider = "TestProvider",
                LLMModel = "test-model",
                LLMEndpoint = "http://localhost:11434"
            };
            var putResponse = await client.PutAsJsonAsync("/agent/config", update);
            putResponse.EnsureSuccessStatusCode();

            var response = await client.GetAsync("/agent/health");
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Assert.True(root.GetProperty("llmEnabled").GetBoolean());
            Assert.True(root.GetProperty("llmAvailable").GetBoolean());
            Assert.Equal("TestProvider", root.GetProperty("provider").GetString());
            Assert.Equal("test-model", root.GetProperty("model").GetString());
            Assert.False(string.IsNullOrEmpty(root.GetProperty("endpoint").GetString()));
        }

        [Fact]
        public async Task Health_LlmDisabled_ReportsUnavailable()
        {
            using var factory = CreateFactory(enableLlm: false);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/agent/health");
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Assert.False(root.GetProperty("llmEnabled").GetBoolean());
        }

        #endregion

        #region /agent/llm/test

        [Fact]
        public async Task LlmTest_WhenAvailable_ReturnsSuccess()
        {
            var llmService = new TestLlmService(isEnabled: true, isAvailable: true);
            using var factory = CreateFactory(enableLlm: true, llmService: llmService);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/agent/llm/test");
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Assert.True(root.GetProperty("llmEnabled").GetBoolean());
            Assert.True(root.GetProperty("available").GetBoolean());
        }

        [Fact]
        public async Task LlmTest_WhenUnavailable_ReturnsFalse()
        {
            var llmService = new TestLlmService(isEnabled: true, isAvailable: false);
            using var factory = CreateFactory(enableLlm: true, llmService: llmService);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/agent/llm/test");
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Assert.True(root.GetProperty("llmEnabled").GetBoolean());
            Assert.False(root.GetProperty("available").GetBoolean());
        }

        [Fact]
        public async Task LlmTest_WhenDisabled_ReportsDisabled()
        {
            using var factory = CreateFactory(enableLlm: false);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/agent/llm/test");
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Assert.False(root.GetProperty("available").GetBoolean());
        }

        #endregion

        #region /agent/llm/discover

        [Fact]
        public async Task LlmDiscover_ReturnsEndpoints()
        {
            var discovery = new TestDiscoveryService(new[]
            {
                "http://192.168.1.50:11434",
                "http://192.168.1.51:11434"
            });
            using var factory = CreateFactory(enableLlm: false, discoveryService: discovery);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/agent/llm/discover");
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            var endpoints = root.GetProperty("endpoints");
            Assert.Equal(2, endpoints.GetArrayLength());
            Assert.Equal("http://192.168.1.50:11434", endpoints[0].GetString());
        }

        [Fact]
        public async Task LlmDiscover_WhenNoEndpoints_ReturnsEmptyArray()
        {
            var discovery = new TestDiscoveryService(Array.Empty<string>());
            using var factory = CreateFactory(enableLlm: false, discoveryService: discovery);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/agent/llm/discover");
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Assert.Equal(0, root.GetProperty("endpoints").GetArrayLength());
        }

        [Fact]
        public async Task LlmDiscover_WhenServiceFails_ReturnsErrorMessage()
        {
            var discovery = new TestDiscoveryService(error: "Network timeout scanning subnet");
            using var factory = CreateFactory(enableLlm: false, discoveryService: discovery);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/agent/llm/discover");
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Assert.Equal(0, root.GetProperty("endpoints").GetArrayLength());
            Assert.Contains("Network timeout", root.GetProperty("error").GetString());
        }

        #endregion

        #region /agent/query

        [Fact]
        public async Task Query_FuelQuestion_RulesEngineAnswers()
        {
            var writer = new InMemoryTelemetryWriter();
            writer.WriteSamples("session-1", new List<TelemetrySample>
            {
                new(DateTime.UtcNow, 100, new double[] { 90, 90, 90, 90 }, 2.0, 0.1, 0.2, 0.0)
            });

            using var factory = CreateFactory(enableLlm: false, writer: writer);
            using var client = factory.CreateClient();

            var request = new AgentRequest
            {
                Query = "How much fuel do I have?",
                Context = new Dictionary<string, object> { ["sessionId"] = "session-1" }
            };

            var response = await client.PostAsJsonAsync("/agent/query", request);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<AgentResponse>();

            Assert.NotNull(payload);
            Assert.True(payload!.Success);
            Assert.Equal("RulesEngine", payload.Source);
            Assert.True(payload.ResponseTimeMs >= 0);
        }

        [Fact]
        public async Task Query_ComplexQuestion_FallsBackWithClearMessage()
        {
            using var factory = CreateFactory(enableLlm: false);
            using var client = factory.CreateClient();

            var request = new AgentRequest
            {
                Query = "Why is my car understeering in sector 3?",
                Context = new Dictionary<string, object>()
            };

            var response = await client.PostAsJsonAsync("/agent/query", request);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<AgentResponse>();

            Assert.NotNull(payload);
            Assert.False(payload!.Success);
            Assert.Equal("Fallback", payload.Source);
            Assert.False(string.IsNullOrEmpty(payload.Answer));
        }

        [Fact]
        public async Task Query_WithLlm_ReturnsLlmResponse()
        {
            var llmService = new TestLlmService(isEnabled: true, isAvailable: true, queryAnswer: "Reduce front wing angle by 1 click.");
            using var factory = CreateFactory(enableLlm: true, llmService: llmService);
            using var client = factory.CreateClient();

            var request = new AgentRequest
            {
                Query = "Why is my car understeering in sector 3?",
                Context = new Dictionary<string, object>()
            };

            var response = await client.PostAsJsonAsync("/agent/query", request);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<AgentResponse>();

            Assert.NotNull(payload);
            Assert.True(payload!.Success);
            Assert.Equal("LLM", payload.Source);
            Assert.Contains("front wing", payload.Answer);
        }

        [Fact]
        public async Task Query_NullBody_ReturnsBadRequest()
        {
            using var factory = CreateFactory(enableLlm: false);
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/agent/query", (AgentRequest?)null);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(doc.RootElement.TryGetProperty("errors", out _));
        }

        [Fact]
        public async Task Query_EmptyQuery_ReturnsBadRequest()
        {
            using var factory = CreateFactory(enableLlm: false);
            using var client = factory.CreateClient();

            var request = new AgentRequest { Query = "" };

            var response = await client.PostAsJsonAsync("/agent/query", request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(doc.RootElement.TryGetProperty("errors", out _));
        }

        [Fact]
        public async Task Query_LlmSuppressedWhileRacing_ReturnsSafetyMessage()
        {
            var llmService = new TestLlmService(isEnabled: true, isAvailable: true, queryAnswer: "Should not reach");

            using var factory = CreateFactory(
                enableLlm: true,
                llmService: llmService,
                extraConfig: new Dictionary<string, string?>
                {
                    ["Agent:RequirePitForLlm"] = "true"
                },
                contextProvider: new TestRaceContextProvider(new RaceContext
                {
                    CurrentLap = 5,
                    InPitLane = false
                }));
            using var client = factory.CreateClient();

            var request = new AgentRequest
            {
                Query = "Why am I understeering?",
                Context = new Dictionary<string, object>()
            };

            var response = await client.PostAsJsonAsync("/agent/query", request);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<AgentResponse>();

            Assert.NotNull(payload);
            Assert.False(payload!.Success);
            Assert.Equal("Safety", payload.Source);
            Assert.Contains("disabled while racing", payload.Answer.ToLowerInvariant());
        }

        #endregion

        #region /agent/config GET/PUT round-trip

        [Fact]
        public async Task Config_Get_ReturnsCurrentState()
        {
            using var factory = CreateFactory(enableLlm: false);
            using var client = factory.CreateClient();

            // PUT config first (AgentOptions read eagerly in Main, config overrides not applied)
            var update = new AgentConfigUpdate
            {
                EnableLLM = true,
                LLMProvider = "Ollama",
                LLMModel = "llama3.2",
                LLMEndpoint = "http://localhost:11434",
                LLMTimeoutMs = 8000
            };
            var putResponse = await client.PutAsJsonAsync("/agent/config", update);
            putResponse.EnsureSuccessStatusCode();

            // GET should reflect PUT
            var response = await client.GetAsync("/agent/config");
            response.EnsureSuccessStatusCode();

            var config = await response.Content.ReadFromJsonAsync<AgentConfigResponse>();

            Assert.NotNull(config);
            Assert.True(config!.EnableLLM);
            Assert.Equal("Ollama", config.LLMProvider);
            Assert.Equal("llama3.2", config.LLMModel);
            Assert.Equal("http://localhost:11434", config.LLMEndpoint);
            Assert.Equal(8000, config.LLMTimeoutMs);
        }

        [Fact]
        public async Task Config_PutThenGet_RoundTrips()
        {
            using var factory = CreateFactory(enableLlm: false);
            using var client = factory.CreateClient();

            // PUT new config
            var update = new AgentConfigUpdate
            {
                EnableLLM = true,
                LLMProvider = "OpenAI",
                LLMModel = "gpt-4o",
                LLMEndpoint = "https://api.openai.com",
                LLMTimeoutMs = 12000,
                OpenAiApiKey = "sk-test-key-123"
            };

            var putResponse = await client.PutAsJsonAsync("/agent/config", update);
            putResponse.EnsureSuccessStatusCode();

            // GET to verify persistence
            var getResponse = await client.GetAsync("/agent/config");
            getResponse.EnsureSuccessStatusCode();

            var config = await getResponse.Content.ReadFromJsonAsync<AgentConfigResponse>();

            Assert.NotNull(config);
            Assert.True(config!.EnableLLM);
            Assert.Equal("OpenAI", config.LLMProvider);
            Assert.Equal("gpt-4o", config.LLMModel);
            Assert.Equal("https://api.openai.com", config.LLMEndpoint);
            Assert.Equal(12000, config.LLMTimeoutMs);
            Assert.True(config.OpenAiApiKeyConfigured);
            // API key should NOT be in raw JSON
            var json = await getResponse.Content.ReadAsStringAsync();
            Assert.DoesNotContain("sk-test-key-123", json);
        }

        [Fact]
        public async Task Config_Put_InvalidTimeout_ReturnsBadRequest()
        {
            using var factory = CreateFactory(enableLlm: false);
            using var client = factory.CreateClient();

            var update = new AgentConfigUpdate
            {
                LLMTimeoutMs = -1
            };

            var response = await client.PutAsJsonAsync("/agent/config", update);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(doc.RootElement.TryGetProperty("errors", out var errors));
            Assert.True(errors.GetArrayLength() > 0);
        }

        [Fact]
        public async Task Config_Put_AnthropicConfig_Persists()
        {
            using var factory = CreateFactory(enableLlm: true);
            using var client = factory.CreateClient();

            var update = new AgentConfigUpdate
            {
                LLMProvider = "Anthropic",
                AnthropicApiKey = "sk-ant-test",
                AnthropicEndpoint = "https://api.anthropic.com",
                AnthropicModel = "claude-sonnet-4-20250514"
            };

            var putResponse = await client.PutAsJsonAsync("/agent/config", update);
            putResponse.EnsureSuccessStatusCode();

            var getResponse = await client.GetAsync("/agent/config");
            var config = await getResponse.Content.ReadFromJsonAsync<AgentConfigResponse>();

            Assert.NotNull(config);
            Assert.Equal("Anthropic", config!.LLMProvider);
            Assert.Equal("https://api.anthropic.com", config.AnthropicEndpoint);
            Assert.Equal("claude-sonnet-4-20250514", config.AnthropicModel);
            Assert.True(config.AnthropicApiKeyConfigured);
        }

        [Fact]
        public async Task Config_Put_DiscoverySettings_Persists()
        {
            using var factory = CreateFactory(enableLlm: false);
            using var client = factory.CreateClient();

            var update = new AgentConfigUpdate
            {
                EnableLLMDiscovery = true,
                LLMDiscoveryTimeoutMs = 1000,
                LLMDiscoveryPort = 8080,
                LLMDiscoveryMaxConcurrency = 16,
                LLMDiscoverySubnetPrefix = "10.0.1"
            };

            var putResponse = await client.PutAsJsonAsync("/agent/config", update);
            putResponse.EnsureSuccessStatusCode();

            var getResponse = await client.GetAsync("/agent/config");
            var config = await getResponse.Content.ReadFromJsonAsync<AgentConfigResponse>();

            Assert.NotNull(config);
            Assert.True(config!.EnableLLMDiscovery);
            Assert.Equal(1000, config.LLMDiscoveryTimeoutMs);
            Assert.Equal(8080, config.LLMDiscoveryPort);
            Assert.Equal(16, config.LLMDiscoveryMaxConcurrency);
            Assert.Equal("10.0.1", config.LLMDiscoverySubnetPrefix);
        }

        #endregion

        #region Factory & Stubs

        private static WebApplicationFactory<PitWall.Agent.Program> CreateFactory(
            bool enableLlm,
            InMemoryTelemetryWriter? writer = null,
            ILLMService? llmService = null,
            ILLMDiscoveryService? discoveryService = null,
            IRaceContextProvider? contextProvider = null,
            Dictionary<string, string?>? extraConfig = null)
        {
            return new WebApplicationFactory<PitWall.Agent.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Test");
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        var settings = new Dictionary<string, string?>
                        {
                            ["Agent:EnableLLM"] = enableLlm.ToString().ToLowerInvariant(),
                            ["Agent:LLMProvider"] = "TestProvider",
                            ["Agent:LLMEndpoint"] = "http://localhost:11434",
                            ["Agent:LLMModel"] = "test-model",
                            ["Agent:LLMTimeoutMs"] = "5000"
                        };

                        if (extraConfig != null)
                        {
                            foreach (var entry in extraConfig)
                                settings[entry.Key] = entry.Value;
                        }

                        config.AddInMemoryCollection(settings);
                    });

                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll(typeof(ITelemetryWriter));
                        services.AddSingleton<ITelemetryWriter>(writer ?? new InMemoryTelemetryWriter());

                        if (llmService != null)
                        {
                            services.RemoveAll(typeof(ILLMService));
                            services.AddSingleton(llmService);
                        }

                        if (discoveryService != null)
                        {
                            services.RemoveAll(typeof(ILLMDiscoveryService));
                            services.AddSingleton(discoveryService);
                        }

                        if (contextProvider != null)
                        {
                            services.RemoveAll(typeof(IRaceContextProvider));
                            services.AddSingleton(contextProvider);
                        }
                    });
                });
        }

        private sealed class TestLlmService : ILLMService
        {
            private readonly string _queryAnswer;

            public TestLlmService(bool isEnabled, bool isAvailable, string queryAnswer = "test answer")
            {
                IsEnabled = isEnabled;
                IsAvailable = isAvailable;
                _queryAnswer = queryAnswer;
            }

            public bool IsEnabled { get; }
            public bool IsAvailable { get; }

            public Task<bool> TestConnectionAsync() => Task.FromResult(IsAvailable);

            public Task<AgentResponse> QueryAsync(string query, RaceContext context)
            {
                return Task.FromResult(new AgentResponse
                {
                    Answer = _queryAnswer,
                    Source = "LLM",
                    Confidence = 0.85,
                    ResponseTimeMs = 42,
                    Success = true
                });
            }
        }

        private sealed class TestDiscoveryService : ILLMDiscoveryService
        {
            private readonly IReadOnlyList<string>? _endpoints;
            private readonly string? _error;

            public TestDiscoveryService(IReadOnlyList<string> endpoints)
            {
                _endpoints = endpoints;
            }

            public TestDiscoveryService(string error)
            {
                _error = error;
            }

            public Task<IReadOnlyList<string>> DiscoverAsync(CancellationToken cancellationToken = default)
            {
                if (_error != null)
                    throw new Exception(_error);

                return Task.FromResult(_endpoints!);
            }
        }

        private sealed class TestRaceContextProvider : IRaceContextProvider
        {
            private readonly RaceContext _context;

            public TestRaceContextProvider(RaceContext context)
            {
                _context = context;
            }

            public Task<RaceContext> BuildAsync(AgentRequest request, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_context);
            }
        }

        #endregion
    }
}
