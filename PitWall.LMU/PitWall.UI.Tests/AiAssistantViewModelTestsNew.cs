using System;
using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Models;
using PitWall.UI.Services;
using PitWall.UI.ViewModels;
using Xunit;

namespace PitWall.UI.Tests
{
    public class AiAssistantViewModelTestsNew
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_Parameterless_InitializesDefaults()
        {
            var vm = new AiAssistantViewModel();

            Assert.Equal(string.Empty, vm.InputText);
            Assert.False(vm.IsProcessing);
            Assert.False(vm.ShowContext);
            Assert.Equal(string.Empty, vm.RaceContext);
            Assert.Equal("Ready", vm.StatusMessage);
            Assert.NotNull(vm.Messages);
            Assert.Empty(vm.Messages);
        }

        [Fact]
        public void HasStatus_WhenStatusMessageSet_ReturnsTrue()
        {
            var vm = new AiAssistantViewModel();

            Assert.True(vm.HasStatus);
        }

        [Fact]
        public void HasStatus_WhenStatusMessageCleared_ReturnsFalse()
        {
            var vm = new AiAssistantViewModel();
            vm.StatusMessage = string.Empty;

            Assert.False(vm.HasStatus);
        }

        [Fact]
        public void HasStatus_WhenStatusMessageNull_ReturnsFalse()
        {
            var vm = new AiAssistantViewModel();
            vm.StatusMessage = null!;

            Assert.False(vm.HasStatus);
        }

        #endregion

        #region SendQueryAsync Tests

        [Fact]
        public async Task SendQueryAsync_EmptyInput_DoesNotSendMessage()
        {
            var vm = new AiAssistantViewModel();
            vm.InputText = "";

            await vm.SendQueryCommand.ExecuteAsync(null);

            Assert.Empty(vm.Messages);
        }

        [Fact]
        public async Task SendQueryAsync_WhitespaceInput_DoesNotSendMessage()
        {
            var vm = new AiAssistantViewModel();
            vm.InputText = "   ";

            await vm.SendQueryCommand.ExecuteAsync(null);

            Assert.Empty(vm.Messages);
        }

        [Fact]
        public async Task SendQueryAsync_WhenProcessing_DoesNotSendMessage()
        {
            var vm = new AiAssistantViewModel();
            vm.InputText = "test query";
            vm.IsProcessing = true;

            await vm.SendQueryCommand.ExecuteAsync(null);

            Assert.Empty(vm.Messages);
        }

        [Fact]
        public async Task SendQueryAsync_ValidInput_AddsUserAndAssistantMessages()
        {
            var client = new StubQueryClient(new AgentResponseDto
            {
                Answer = "Fuel is at 50%",
                Source = "RulesEngine",
                Success = true,
                Confidence = 0.95
            });
            var vm = new AiAssistantViewModel(client);
            vm.InputText = "How much fuel?";

            await vm.SendQueryCommand.ExecuteAsync(null);

            Assert.Equal(2, vm.Messages.Count);
            Assert.Equal("User", vm.Messages[0].Role);
            Assert.Equal("How much fuel?", vm.Messages[0].Text);
            Assert.Equal("Assistant", vm.Messages[1].Role);
            Assert.Equal("Fuel is at 50%", vm.Messages[1].Text);
            Assert.Equal("RulesEngine", vm.Messages[1].Source);
            Assert.Equal(0.95, vm.Messages[1].Confidence);
        }

        [Fact]
        public async Task SendQueryAsync_ClearsInputAfterSending()
        {
            var client = new StubQueryClient(new AgentResponseDto { Answer = "ok", Success = true });
            var vm = new AiAssistantViewModel(client);
            vm.InputText = "test query";

            await vm.SendQueryCommand.ExecuteAsync(null);

            Assert.Equal(string.Empty, vm.InputText);
        }

        [Fact]
        public async Task SendQueryAsync_ResetsProcessingAfterComplete()
        {
            var client = new StubQueryClient(new AgentResponseDto { Answer = "ok", Success = true });
            var vm = new AiAssistantViewModel(client);
            vm.InputText = "test";

            await vm.SendQueryCommand.ExecuteAsync(null);

            Assert.False(vm.IsProcessing);
        }

        [Fact]
        public async Task SendQueryAsync_NullAnswer_ShowsUnavailableMessage()
        {
            var client = new StubQueryClient(new AgentResponseDto { Answer = null!, Success = true });
            var vm = new AiAssistantViewModel(client);
            vm.InputText = "test";

            await vm.SendQueryCommand.ExecuteAsync(null);

            Assert.Equal(2, vm.Messages.Count);
            Assert.Contains("unavailable", vm.Messages[1].Text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SendQueryAsync_EmptyAnswer_ShowsUnavailableMessage()
        {
            var client = new StubQueryClient(new AgentResponseDto { Answer = "  ", Success = true });
            var vm = new AiAssistantViewModel(client);
            vm.InputText = "test";

            await vm.SendQueryCommand.ExecuteAsync(null);

            Assert.Contains("unavailable", vm.Messages[1].Text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SendQueryAsync_SuccessWithConfidence_ShowsConfidenceInStatus()
        {
            var client = new StubQueryClient(new AgentResponseDto
            {
                Answer = "ok",
                Source = "LLM",
                Success = true,
                Confidence = 0.85
            });
            var vm = new AiAssistantViewModel(client);
            vm.InputText = "test";

            await vm.SendQueryCommand.ExecuteAsync(null);

            Assert.Contains("LLM", vm.StatusMessage);
            Assert.Contains("85", vm.StatusMessage);
        }

        [Fact]
        public async Task SendQueryAsync_SuccessWithoutConfidence_ShowsSourceInStatus()
        {
            var client = new StubQueryClient(new AgentResponseDto
            {
                Answer = "ok",
                Source = "RulesEngine",
                Success = true,
                Confidence = 0
            });
            var vm = new AiAssistantViewModel(client);
            vm.InputText = "test";

            await vm.SendQueryCommand.ExecuteAsync(null);

            Assert.Contains("RulesEngine", vm.StatusMessage);
        }

        [Fact]
        public async Task SendQueryAsync_FailedResponse_ShowsFailedStatus()
        {
            var client = new StubQueryClient(new AgentResponseDto
            {
                Answer = "error",
                Success = false
            });
            var vm = new AiAssistantViewModel(client);
            vm.InputText = "test";

            await vm.SendQueryCommand.ExecuteAsync(null);

            Assert.Contains("failed", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SendQueryAsync_Exception_AddsSystemErrorMessage()
        {
            var client = new ThrowingQueryClient();
            var vm = new AiAssistantViewModel(client);
            vm.InputText = "test";

            await vm.SendQueryCommand.ExecuteAsync(null);

            Assert.Equal(2, vm.Messages.Count);
            Assert.Equal("System", vm.Messages[1].Role);
            Assert.Contains("Error", vm.Messages[1].Text);
        }

        [Fact]
        public async Task SendQueryAsync_NullSource_ShowsUnknownInStatus()
        {
            var client = new StubQueryClient(new AgentResponseDto
            {
                Answer = "ok",
                Source = null,
                Success = true,
                Confidence = 0.5
            });
            var vm = new AiAssistantViewModel(client);
            vm.InputText = "test";

            await vm.SendQueryCommand.ExecuteAsync(null);

            Assert.Contains("Unknown", vm.StatusMessage);
        }

        #endregion

        #region SendQuickQueryAsync Tests

        [Fact]
        public async Task SendQuickQueryAsync_SetsInputAndSends()
        {
            var client = new StubQueryClient(new AgentResponseDto { Answer = "ok", Success = true });
            var vm = new AiAssistantViewModel(client);

            await vm.SendQuickQueryCommand.ExecuteAsync("Pit strategy?");

            Assert.Equal(2, vm.Messages.Count);
            Assert.Equal("Pit strategy?", vm.Messages[0].Text);
        }

        #endregion

        #region Other Commands Tests

        [Fact]
        public void ToggleContextDisplay_TogglesShowContext()
        {
            var vm = new AiAssistantViewModel();

            Assert.False(vm.ShowContext);
            vm.ToggleContextDisplayCommand.Execute(null);
            Assert.True(vm.ShowContext);
            vm.ToggleContextDisplayCommand.Execute(null);
            Assert.False(vm.ShowContext);
        }

        [Fact]
        public async Task ClearHistory_RemovesAllMessages()
        {
            var client = new StubQueryClient(new AgentResponseDto { Answer = "ok", Success = true });
            var vm = new AiAssistantViewModel(client);
            vm.InputText = "test";
            await vm.SendQueryCommand.ExecuteAsync(null);
            Assert.NotEmpty(vm.Messages);

            vm.ClearHistoryCommand.Execute(null);

            Assert.Empty(vm.Messages);
        }

        [Fact]
        public void UpdateRaceContext_SetsRaceContext()
        {
            var vm = new AiAssistantViewModel();

            vm.UpdateRaceContext("Lap 5/20, Fuel 60%");

            Assert.Equal("Lap 5/20, Fuel 60%", vm.RaceContext);
        }

        #endregion

        #region AiMessageViewModel Tests

        [Fact]
        public void AiMessageViewModel_Defaults()
        {
            var msg = new AiMessageViewModel();

            Assert.Equal(string.Empty, msg.Role);
            Assert.Equal(string.Empty, msg.Text);
            Assert.Null(msg.Source);
            Assert.Equal(0.0, msg.Confidence);
            Assert.Null(msg.Context);
            Assert.False(msg.IsContextExpanded);
        }

        [Fact]
        public void AiMessageViewModel_DisplayTimestamp_FormatsCorrectly()
        {
            var msg = new AiMessageViewModel
            {
                Timestamp = new DateTime(2025, 1, 15, 14, 30, 45)
            };

            Assert.Equal("14:30:45", msg.DisplayTimestamp);
        }

        [Fact]
        public void AiMessageViewModel_DisplaySource_NullReturnsUnknown()
        {
            var msg = new AiMessageViewModel { Source = null! };

            Assert.Equal("Unknown", msg.DisplaySource);
        }

        [Fact]
        public void AiMessageViewModel_DisplaySource_ReturnsValue()
        {
            var msg = new AiMessageViewModel { Source = "LLM" };

            Assert.Equal("LLM", msg.DisplaySource);
        }

        [Fact]
        public void AiMessageViewModel_ConfidenceDisplay_ZeroReturnsEmpty()
        {
            var msg = new AiMessageViewModel { Confidence = 0 };

            Assert.Equal(string.Empty, msg.ConfidenceDisplay);
        }

        [Fact]
        public void AiMessageViewModel_ConfidenceDisplay_ReturnsPercentage()
        {
            var msg = new AiMessageViewModel { Confidence = 0.85 };

            Assert.Equal("85%", msg.ConfidenceDisplay);
        }

        [Fact]
        public void AiMessageViewModel_HasConfidence_TrueWhenAboveZero()
        {
            var msg = new AiMessageViewModel { Confidence = 0.5 };

            Assert.True(msg.HasConfidence);
        }

        [Fact]
        public void AiMessageViewModel_HasConfidence_FalseWhenZero()
        {
            var msg = new AiMessageViewModel { Confidence = 0 };

            Assert.False(msg.HasConfidence);
        }

        [Fact]
        public void AiMessageViewModel_IsUserMessage_TrueForUser()
        {
            var msg = new AiMessageViewModel { Role = "User" };

            Assert.True(msg.IsUserMessage);
            Assert.False(msg.IsAssistantMessage);
        }

        [Fact]
        public void AiMessageViewModel_IsAssistantMessage_TrueForAssistant()
        {
            var msg = new AiMessageViewModel { Role = "Assistant" };

            Assert.True(msg.IsAssistantMessage);
            Assert.False(msg.IsUserMessage);
        }

        [Fact]
        public void AiMessageViewModel_HasContext_TrueWhenContextSet()
        {
            var msg = new AiMessageViewModel { Context = "some context data" };

            Assert.True(msg.HasContext);
        }

        [Fact]
        public void AiMessageViewModel_HasContext_FalseWhenNull()
        {
            var msg = new AiMessageViewModel { Context = null };

            Assert.False(msg.HasContext);
        }

        [Fact]
        public void AiMessageViewModel_HasContext_FalseWhenWhitespace()
        {
            var msg = new AiMessageViewModel { Context = "   " };

            Assert.False(msg.HasContext);
        }

        #endregion

        #region NullAgentQueryClient Tests (Production)

        [Fact]
        public async Task NullAgentQueryClient_Production_SendQueryAsync_ReturnsDesignTimeResponse()
        {
            // Use FQN to disambiguate from test-assembly duplicate in AtlasViewModelTests
            var client = new PitWall.UI.ViewModels.NullAgentQueryClient();

            var result = await client.SendQueryAsync("test", CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Design-time response", result.Answer);
            Assert.Equal("Null", result.Source);
            Assert.True(result.Success);
        }

        #endregion

        #region Test Doubles

        private sealed class StubQueryClient : IAgentQueryClient
        {
            private readonly AgentResponseDto _response;
            public StubQueryClient(AgentResponseDto response) => _response = response;

            public Task<AgentResponseDto> SendQueryAsync(string query, CancellationToken cancellationToken)
                => Task.FromResult(_response);
        }

        private sealed class ThrowingQueryClient : IAgentQueryClient
        {
            public Task<AgentResponseDto> SendQueryAsync(string query, CancellationToken cancellationToken)
                => throw new InvalidOperationException("Network error");
        }

        #endregion
    }
}
