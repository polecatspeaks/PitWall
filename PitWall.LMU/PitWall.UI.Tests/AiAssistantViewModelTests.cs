using System;
using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Models;
using PitWall.UI.Services;
using PitWall.UI.ViewModels;
using Xunit;

namespace PitWall.UI.Tests
{
    public class AiAssistantViewModelTests
    {
        [Fact]
        public async Task SendAiQueryAsync_AddsUserAndAssistantMessages()
        {
            var client = new StubAgentQueryClient(new AgentResponseDto
            {
                Answer = "You have 3.2 laps of fuel",
                Source = "RulesEngine",
                Success = true
            });

            var vm = new MainWindowViewModel(
                new StubRecommendationClient(),
                new StubTelemetryStreamClient(),
                client,
                new StubConfigClient(),
                new StubSessionClient());

            vm.AiInput = "Fuel status?";
            await vm.SendAiQueryAsync(CancellationToken.None);

            Assert.Equal(2, vm.AiMessages.Count);
            Assert.Equal("User", vm.AiMessages[0].Role);
            Assert.Equal("Assistant", vm.AiMessages[1].Role);
            Assert.Contains("fuel", vm.AiMessages[1].Text.ToLowerInvariant());
        }

        [Fact]
        public async Task SendAiQueryAsync_IgnoresEmptyInput()
        {
            var vm = new MainWindowViewModel(
                new StubRecommendationClient(),
                new StubTelemetryStreamClient(),
                new StubAgentQueryClient(new AgentResponseDto()),
                new StubConfigClient(),
                new StubSessionClient());

            vm.AiInput = " ";
            await vm.SendAiQueryAsync(CancellationToken.None);

            Assert.Empty(vm.AiMessages);
        }

        private sealed class StubAgentQueryClient : IAgentQueryClient
        {
            private readonly AgentResponseDto _response;

            public StubAgentQueryClient(AgentResponseDto response)
            {
                _response = response;
            }

            public Task<AgentResponseDto> SendQueryAsync(string query, CancellationToken cancellationToken)
            {
                return Task.FromResult(_response);
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
            public Task ConnectAsync(int sessionId, int startRow, int endRow, int intervalMs, System.Action<TelemetrySampleDto> onMessage, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class StubSessionClient : ISessionClient
        {
            public Task<int> GetSessionCountAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(2);
            }
        }

        private sealed class StubConfigClient : IAgentConfigClient
        {
            public Task<AgentConfigDto> GetConfigAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new AgentConfigDto());
            }

            public Task<AgentConfigDto> UpdateConfigAsync(AgentConfigUpdateDto update, CancellationToken cancellationToken)
            {
                return Task.FromResult(new AgentConfigDto());
            }

            public Task<IReadOnlyList<string>> DiscoverEndpointsAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
            }

            public Task<AgentHealthDto> GetHealthAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new AgentHealthDto());
            }

            public Task<AgentLlmTestDto> TestLlmAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(new AgentLlmTestDto());
            }
        }
    }
}
