using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Models;
using PitWall.UI.Services;
using PitWall.UI.ViewModels;
using Xunit;

namespace PitWall.UI.Tests
{
    public class SettingsViewModelTests
    {
        [Fact]
        public async Task LoadSettingsAsync_PopulatesFields()
        {
            var client = new StubConfigClient(new AgentConfigDto
            {
                EnableLLM = true,
                LLMProvider = "Ollama",
                RequirePitForLlm = true
            });

            var vm = new MainWindowViewModel(
                new StubRecommendationClient(),
                new StubTelemetryStreamClient(),
                new StubAgentQueryClient(),
                client);

            await vm.LoadSettingsAsync(CancellationToken.None);

            Assert.True(vm.EnableLlm);
            Assert.Equal("Ollama", vm.LlmProvider);
            Assert.True(vm.RequirePitForLlm);
        }

        [Fact]
        public async Task SaveSettingsAsync_CallsUpdate()
        {
            var client = new TrackingConfigClient();

            var vm = new MainWindowViewModel(
                new StubRecommendationClient(),
                new StubTelemetryStreamClient(),
                new StubAgentQueryClient(),
                client);

            vm.EnableLlm = false;
            vm.LlmProvider = "OpenAI";
            vm.RequirePitForLlm = false;

            await vm.SaveSettingsAsync(CancellationToken.None);

            Assert.True(client.Called);
            Assert.Equal("OpenAI", client.LastUpdate?.LLMProvider);
        }

        private sealed class StubConfigClient : IAgentConfigClient
        {
            private readonly AgentConfigDto _config;

            public StubConfigClient(AgentConfigDto config)
            {
                _config = config;
            }

            public Task<AgentConfigDto> GetConfigAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(_config);
            }

            public Task<AgentConfigDto> UpdateConfigAsync(AgentConfigUpdateDto update, CancellationToken cancellationToken)
            {
                return Task.FromResult(_config);
            }

            public Task<DiscoveryResultDto> RunDiscoveryAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new DiscoveryResultDto());
            }
        }

        private sealed class TrackingConfigClient : IAgentConfigClient
        {
            public bool Called { get; private set; }
            public AgentConfigUpdateDto? LastUpdate { get; private set; }

            public Task<AgentConfigDto> GetConfigAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new AgentConfigDto());
            }

            public Task<AgentConfigDto> UpdateConfigAsync(AgentConfigUpdateDto update, CancellationToken cancellationToken)
            {
                Called = true;
                LastUpdate = update;
                return Task.FromResult(new AgentConfigDto());
            }

            public Task<DiscoveryResultDto> RunDiscoveryAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new DiscoveryResultDto());
            }
        }

        private sealed class StubRecommendationClient : IRecommendationClient
        {
            public Task<RecommendationDto> GetRecommendationAsync(string sessionId, CancellationToken cancellationToken)
            {
                return Task.FromResult(new RecommendationDto());
            }
        }

        private sealed class StubTelemetryStreamClient : ITelemetryStreamClient
        {
            public Task ConnectAsync(int sessionId, System.Action<TelemetrySampleDto> onMessage, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class StubAgentQueryClient : IAgentQueryClient
        {
            public Task<AgentResponseDto> SendQueryAsync(string query, CancellationToken cancellationToken)
            {
                return Task.FromResult(new AgentResponseDto());
            }
        }
    }
}
