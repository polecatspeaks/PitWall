using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PitWall.Agent.Models;
using PitWall.Agent.Services.LLM;
using PitWall.Core.Models;
using PitWall.Core.Storage;
using Xunit;

namespace PitWall.Tests
{
    public class AgentIntegrationTests
    {
        [Fact]
        public async Task AgentHealth_ReturnsLlmStatus()
        {
            using var factory = CreateFactory(enableLlm: false, writer: new InMemoryTelemetryWriter());
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/agent/health");
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("llmEnabled", out var enabled));
            Assert.False(enabled.GetBoolean());
        }

        [Fact]
        public async Task AgentQuery_UsesTelemetryContextFuel()
        {
            var writer = new InMemoryTelemetryWriter();
            var sessionId = "session-1";
            writer.WriteSamples(sessionId, new List<TelemetrySample>
            {
                new TelemetrySample(System.DateTime.UtcNow, 100, new double[] { 90, 90, 90, 90 }, 2.0, 0.1, 0.2, 0.0)
            });

            using var factory = CreateFactory(enableLlm: false, writer: writer);
            using var client = factory.CreateClient();

            var request = new AgentRequest
            {
                Query = "How much fuel do I have?",
                Context = new Dictionary<string, object>
                {
                    ["sessionId"] = sessionId
                }
            };

            var response = await client.PostAsJsonAsync("/agent/query", request);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<AgentResponse>();

            Assert.NotNull(payload);
            Assert.True(payload!.Success);
            Assert.Equal("RulesEngine", payload.Source);
            Assert.Contains("FUEL CRITICAL", payload.Answer);
        }

        [Fact]
        public async Task AgentLlmTest_ReturnsAvailability()
        {
            var llmService = new StubLlmService(isEnabled: true, isAvailable: true);
            using var factory = CreateFactory(enableLlm: true, writer: new InMemoryTelemetryWriter(), llmService: llmService);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/agent/llm/test");
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("available", out var available));
            Assert.True(available.GetBoolean());
        }

        [Fact]
        public async Task AgentLlmDiscover_ReturnsResults()
        {
            var discovery = new StubDiscoveryService(new[]
            {
                "http://192.168.1.100:11434",
                "http://192.168.1.101:11434"
            });

            using var factory = CreateFactory(enableLlm: false, writer: new InMemoryTelemetryWriter(), discoveryService: discovery);
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/agent/llm/discover");
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("endpoints", out var endpoints));
            Assert.Equal(2, endpoints.GetArrayLength());
        }

        private static WebApplicationFactory<PitWall.Agent.Program> CreateFactory(
            bool enableLlm,
            InMemoryTelemetryWriter writer,
            ILLMService? llmService = null,
            ILLMDiscoveryService? discoveryService = null)
        {
            return new WebApplicationFactory<PitWall.Agent.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Agent:EnableLLM"] = enableLlm.ToString().ToLowerInvariant(),
                            ["Agent:LLMProvider"] = "Ollama",
                            ["Agent:LLMEndpoint"] = "http://localhost:11434",
                            ["Agent:LLMModel"] = "llama3.2",
                            ["Agent:LLMTimeoutMs"] = "5000"
                        });
                    });

                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll(typeof(ITelemetryWriter));
                        services.AddSingleton<ITelemetryWriter>(writer);

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
                    });
                });
        }

        private sealed class StubDiscoveryService : ILLMDiscoveryService
        {
            private readonly IReadOnlyList<string> _endpoints;

            public StubDiscoveryService(IReadOnlyList<string> endpoints)
            {
                _endpoints = endpoints;
            }

            public Task<IReadOnlyList<string>> DiscoverAsync(System.Threading.CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_endpoints);
            }
        }

        private sealed class StubLlmService : ILLMService
        {
            public StubLlmService(bool isEnabled, bool isAvailable)
            {
                IsEnabled = isEnabled;
                IsAvailable = isAvailable;
            }

            public bool IsEnabled { get; }
            public bool IsAvailable { get; }

            public Task<bool> TestConnectionAsync()
            {
                return Task.FromResult(IsAvailable);
            }

            public Task<AgentResponse> QueryAsync(string query, RaceContext context)
            {
                return Task.FromResult(new AgentResponse
                {
                    Answer = "stub",
                    Source = "LLM",
                    Success = true
                });
            }
        }
    }
}
