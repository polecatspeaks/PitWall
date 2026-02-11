using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PitWall.UI.Models;
using PitWall.UI.Services;
using PitWall.UI.ViewModels;
using Xunit;

namespace PitWall.UI.Tests
{
    public class SettingsViewModelTestsNew
    {
        [Fact]
        public void Constructor_InitializesWithDefaultValues()
        {
            // Act
            var vm = new SettingsViewModel();

            // Assert
            Assert.False(vm.EnableLlm);
            Assert.Equal("Ollama", vm.LlmProvider);
            Assert.Equal(string.Empty, vm.LlmEndpoint);
            Assert.Equal(string.Empty, vm.LlmModel);
            Assert.Equal(5000, vm.LlmTimeoutMs);
            Assert.False(vm.RequirePitForLlm);
            Assert.False(vm.EnableLlmDiscovery);
            Assert.Equal(2000, vm.LlmDiscoveryTimeoutMs);
            Assert.Equal(11434, vm.LlmDiscoveryPort);
            Assert.Equal(50, vm.LlmDiscoveryMaxConcurrency);
            Assert.Null(vm.LlmDiscoverySubnetPrefix);
            Assert.Equal(string.Empty, vm.OpenAiEndpoint);
            Assert.Equal(string.Empty, vm.OpenAiModel);
            Assert.Equal(string.Empty, vm.OpenAiApiKey);
            Assert.False(vm.OpenAiApiKeyConfigured);
            Assert.Equal(string.Empty, vm.AnthropicEndpoint);
            Assert.Equal(string.Empty, vm.AnthropicModel);
            Assert.Equal(string.Empty, vm.AnthropicApiKey);
            Assert.False(vm.AnthropicApiKeyConfigured);
            Assert.False(vm.IsSaving);
            Assert.False(vm.IsLoading);
            Assert.Null(vm.StatusMessage);
            Assert.False(vm.IsDiscovering);
            Assert.False(vm.IsRestarting);
        }

        [Fact]
        public void Constructor_InitializesCollections()
        {
            // Act
            var vm = new SettingsViewModel();

            // Assert
            Assert.NotNull(vm.DiscoveredEndpoints);
            Assert.Empty(vm.DiscoveredEndpoints);
            Assert.NotNull(vm.LlmProviders);
            Assert.Equal(3, vm.LlmProviders.Count);
            Assert.Contains("Ollama", vm.LlmProviders);
            Assert.Contains("OpenAI", vm.LlmProviders);
            Assert.Contains("Anthropic", vm.LlmProviders);
        }

        #region LoadSettingsAsync Tests

        [Fact]
        public async Task LoadSettingsAsync_LoadsAllSettings()
        {
            // Arrange
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.GetConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentConfigDto
                {
                    EnableLLM = true,
                    LLMProvider = "Ollama",
                    LLMEndpoint = "http://localhost:11434",
                    LLMModel = "llama2",
                    LLMTimeoutMs = 10000,
                    RequirePitForLlm = true,
                    EnableLLMDiscovery = true,
                    LLMDiscoveryTimeoutMs = 3000,
                    LLMDiscoveryPort = 11434,
                    LLMDiscoveryMaxConcurrency = 100,
                    LLMDiscoverySubnetPrefix = "192.168.1",
                    OpenAIEndpoint = "https://api.openai.com",
                    OpenAIModel = "gpt-4",
                    OpenAiApiKeyConfigured = true,
                    AnthropicEndpoint = "https://api.anthropic.com",
                    AnthropicModel = "claude-3",
                    AnthropicApiKeyConfigured = true
                });

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());

            // Act
            await vm.LoadSettingsCommand.ExecuteAsync(null);

            // Assert
            Assert.True(vm.EnableLlm);
            Assert.Equal("Ollama", vm.LlmProvider);
            Assert.Equal("http://localhost:11434", vm.LlmEndpoint);
            Assert.Equal("llama2", vm.LlmModel);
            Assert.Equal(10000, vm.LlmTimeoutMs);
            Assert.True(vm.RequirePitForLlm);
            Assert.True(vm.EnableLlmDiscovery);
            Assert.Equal(3000, vm.LlmDiscoveryTimeoutMs);
            Assert.Equal(11434, vm.LlmDiscoveryPort);
            Assert.Equal(100, vm.LlmDiscoveryMaxConcurrency);
            Assert.Equal("192.168.1", vm.LlmDiscoverySubnetPrefix);
            Assert.Equal("https://api.openai.com", vm.OpenAiEndpoint);
            Assert.Equal("gpt-4", vm.OpenAiModel);
            Assert.True(vm.OpenAiApiKeyConfigured);
            Assert.Equal("https://api.anthropic.com", vm.AnthropicEndpoint);
            Assert.Equal("claude-3", vm.AnthropicModel);
            Assert.True(vm.AnthropicApiKeyConfigured);
            Assert.Contains("successfully", vm.StatusMessage);
        }

        [Fact]
        public async Task LoadSettingsAsync_NullLlmProvider_DefaultsToOllama()
        {
            // Arrange
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.GetConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentConfigDto
                {
                    LLMProvider = null
                });

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());

            // Act
            await vm.LoadSettingsCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal("Ollama", vm.LlmProvider);
        }

        [Fact]
        public async Task LoadSettingsAsync_EmptyLlmProvider_DefaultsToOllama()
        {
            // Arrange
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.GetConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentConfigDto
                {
                    LLMProvider = ""
                });

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());

            // Act
            await vm.LoadSettingsCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal("Ollama", vm.LlmProvider);
        }

        [Fact]
        public async Task LoadSettingsAsync_SetsLoadingFlag()
        {
            // Arrange
            var mockClient = new Mock<IAgentConfigClient>();
            var tcs = new TaskCompletionSource<AgentConfigDto>();
            mockClient.Setup(c => c.GetConfigAsync(It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());

            // Act
            var task = vm.LoadSettingsCommand.ExecuteAsync(null);
            
            // Assert - loading should be true during load
            Assert.True(vm.IsLoading);

            // Complete the task
            tcs.SetResult(new AgentConfigDto());
            await task;
            
            Assert.False(vm.IsLoading);
        }

        [Fact]
        public async Task LoadSettingsAsync_HandlesException()
        {
            // Arrange
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.GetConfigAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Network error"));

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());

            // Act
            await vm.LoadSettingsCommand.ExecuteAsync(null);

            // Assert
            Assert.False(vm.IsLoading);
            Assert.Contains("Error loading settings", vm.StatusMessage);
            Assert.Contains("Network error", vm.StatusMessage);
        }

        [Fact]
        public async Task LoadSettingsAsync_ClearsStatusMessage()
        {
            // Arrange
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.GetConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentConfigDto());

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());
            vm.StatusMessage = "Old message";

            // Act
            await vm.LoadSettingsCommand.ExecuteAsync(null);

            // Assert
            Assert.NotNull(vm.StatusMessage);
            Assert.DoesNotContain("Old message", vm.StatusMessage);
        }

        #endregion

        #region SaveSettingsAsync Tests

        [Fact]
        public async Task SaveSettingsAsync_SavesAllSettings()
        {
            // Arrange
            AgentConfigUpdateDto? capturedUpdate = null;
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.UpdateConfigAsync(It.IsAny<AgentConfigUpdateDto>(), It.IsAny<CancellationToken>()))
                .Callback<AgentConfigUpdateDto, CancellationToken>((update, ct) => capturedUpdate = update)
                .ReturnsAsync(new AgentConfigDto());

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());
            vm.EnableLlm = true;
            vm.LlmProvider = "OpenAI";
            vm.LlmEndpoint = "https://api.openai.com";
            vm.LlmModel = "gpt-4";
            vm.LlmTimeoutMs = 15000;
            vm.RequirePitForLlm = false;
            vm.EnableLlmDiscovery = true;
            vm.LlmDiscoveryTimeoutMs = 5000;
            vm.LlmDiscoveryPort = 8080;
            vm.LlmDiscoveryMaxConcurrency = 200;
            vm.LlmDiscoverySubnetPrefix = "10.0.0";
            vm.OpenAiEndpoint = "https://api.openai.com";
            vm.OpenAiModel = "gpt-4";
            vm.OpenAiApiKey = "test-key";
            vm.AnthropicEndpoint = "https://api.anthropic.com";
            vm.AnthropicModel = "claude-3";
            vm.AnthropicApiKey = "test-anthropic-key";

            // Act
            await vm.SaveSettingsCommand.ExecuteAsync(null);

            // Assert
            Assert.NotNull(capturedUpdate);
            Assert.True(capturedUpdate!.EnableLLM);
            Assert.Equal("OpenAI", capturedUpdate.LLMProvider);
            Assert.Equal("https://api.openai.com", capturedUpdate.LLMEndpoint);
            Assert.Equal("gpt-4", capturedUpdate.LLMModel);
            Assert.Equal(15000, capturedUpdate.LLMTimeoutMs);
            Assert.False(capturedUpdate.RequirePitForLlm);
            Assert.True(capturedUpdate.EnableLLMDiscovery);
            Assert.Equal(5000, capturedUpdate.LLMDiscoveryTimeoutMs);
            Assert.Equal(8080, capturedUpdate.LLMDiscoveryPort);
            Assert.Equal(200, capturedUpdate.LLMDiscoveryMaxConcurrency);
            Assert.Equal("10.0.0", capturedUpdate.LLMDiscoverySubnetPrefix);
            Assert.Equal("https://api.openai.com", capturedUpdate.OpenAiEndpoint);
            Assert.Equal("gpt-4", capturedUpdate.OpenAiModel);
            Assert.Equal("test-key", capturedUpdate.OpenAiApiKey);
            Assert.Equal("https://api.anthropic.com", capturedUpdate.AnthropicEndpoint);
            Assert.Equal("claude-3", capturedUpdate.AnthropicModel);
            Assert.Equal("test-anthropic-key", capturedUpdate.AnthropicApiKey);
        }

        [Fact]
        public async Task SaveSettingsAsync_EmptyApiKeys_SendsNull()
        {
            // Arrange
            AgentConfigUpdateDto? capturedUpdate = null;
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.UpdateConfigAsync(It.IsAny<AgentConfigUpdateDto>(), It.IsAny<CancellationToken>()))
                .Callback<AgentConfigUpdateDto, CancellationToken>((update, ct) => capturedUpdate = update)
                .ReturnsAsync(new AgentConfigDto());

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());
            vm.OpenAiApiKey = "";
            vm.AnthropicApiKey = "   ";

            // Act
            await vm.SaveSettingsCommand.ExecuteAsync(null);

            // Assert
            Assert.NotNull(capturedUpdate);
            Assert.Null(capturedUpdate!.OpenAiApiKey);
            Assert.Null(capturedUpdate.AnthropicApiKey);
        }

        [Fact]
        public async Task SaveSettingsAsync_ClearsApiKeysAfterSave()
        {
            // Arrange
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.UpdateConfigAsync(It.IsAny<AgentConfigUpdateDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentConfigDto
                {
                    OpenAiApiKeyConfigured = true,
                    AnthropicApiKeyConfigured = true
                });

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());
            vm.OpenAiApiKey = "test-key";
            vm.AnthropicApiKey = "test-anthropic-key";

            // Act
            await vm.SaveSettingsCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal(string.Empty, vm.OpenAiApiKey);
            Assert.Equal(string.Empty, vm.AnthropicApiKey);
            Assert.True(vm.OpenAiApiKeyConfigured);
            Assert.True(vm.AnthropicApiKeyConfigured);
        }

        [Fact]
        public async Task SaveSettingsAsync_SetsSavingFlag()
        {
            // Arrange
            var mockClient = new Mock<IAgentConfigClient>();
            var tcs = new TaskCompletionSource<AgentConfigDto>();
            mockClient.Setup(c => c.UpdateConfigAsync(It.IsAny<AgentConfigUpdateDto>(), It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());

            // Act
            var task = vm.SaveSettingsCommand.ExecuteAsync(null);

            // Assert - saving should be true during save
            Assert.True(vm.IsSaving);

            // Complete the task
            tcs.SetResult(new AgentConfigDto());
            await task;

            Assert.False(vm.IsSaving);
        }

        [Fact]
        public async Task SaveSettingsAsync_HandlesException()
        {
            // Arrange
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.UpdateConfigAsync(It.IsAny<AgentConfigUpdateDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Save failed"));

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());

            // Act
            await vm.SaveSettingsCommand.ExecuteAsync(null);

            // Assert
            Assert.False(vm.IsSaving);
            Assert.Contains("Error saving settings", vm.StatusMessage);
            Assert.Contains("Save failed", vm.StatusMessage);
        }

        [Fact]
        public async Task SaveSettingsAsync_ShowsSuccessMessage()
        {
            // Arrange
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.UpdateConfigAsync(It.IsAny<AgentConfigUpdateDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentConfigDto());

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());

            // Act
            await vm.SaveSettingsCommand.ExecuteAsync(null);

            // Assert
            Assert.Contains("successfully", vm.StatusMessage);
        }

        #endregion

        #region DiscoverLlmEndpointsAsync Tests

        [Fact]
        public async Task DiscoverLlmEndpointsAsync_FindsEndpoints()
        {
            // Arrange
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.DiscoverEndpointsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "http://192.168.1.100:11434", "http://192.168.1.101:11434" });

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());

            // Act
            await vm.DiscoverLlmEndpointsCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal(2, vm.DiscoveredEndpoints.Count);
            Assert.Contains("http://192.168.1.100:11434", vm.DiscoveredEndpoints);
            Assert.Contains("http://192.168.1.101:11434", vm.DiscoveredEndpoints);
            Assert.Contains("Found 2 endpoint", vm.StatusMessage);
        }

        [Fact]
        public async Task DiscoverLlmEndpointsAsync_NoEndpointsFound_ShowsWarning()
        {
            // Arrange
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.DiscoverEndpointsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());

            // Act
            await vm.DiscoverLlmEndpointsCommand.ExecuteAsync(null);

            // Assert
            Assert.Empty(vm.DiscoveredEndpoints);
            Assert.Contains("No LLM endpoints found", vm.StatusMessage);
        }

        [Fact]
        public async Task DiscoverLlmEndpointsAsync_SetsDiscoveringFlag()
        {
            // Arrange
            var mockClient = new Mock<IAgentConfigClient>();
            var tcs = new TaskCompletionSource<IReadOnlyList<string>>();
            mockClient.Setup(c => c.DiscoverEndpointsAsync(It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());

            // Act
            var task = vm.DiscoverLlmEndpointsCommand.ExecuteAsync(null);

            // Assert - discovering should be true during discovery
            Assert.True(vm.IsDiscovering);

            // Complete the task
            tcs.SetResult(new List<string>());
            await task;

            Assert.False(vm.IsDiscovering);
        }

        [Fact]
        public async Task DiscoverLlmEndpointsAsync_ClearsExistingEndpoints()
        {
            // Arrange
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.DiscoverEndpointsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string> { "http://new-endpoint:11434" });

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());
            vm.DiscoveredEndpoints.Add("http://old-endpoint:11434");

            // Act
            await vm.DiscoverLlmEndpointsCommand.ExecuteAsync(null);

            // Assert
            Assert.Single(vm.DiscoveredEndpoints);
            Assert.Contains("http://new-endpoint:11434", vm.DiscoveredEndpoints);
            Assert.DoesNotContain("http://old-endpoint:11434", vm.DiscoveredEndpoints);
        }

        [Fact]
        public async Task DiscoverLlmEndpointsAsync_HandlesException()
        {
            // Arrange
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.DiscoverEndpointsAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Discovery failed"));

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService());

            // Act
            await vm.DiscoverLlmEndpointsCommand.ExecuteAsync(null);

            // Assert
            Assert.False(vm.IsDiscovering);
            Assert.Contains("Discovery failed", vm.StatusMessage);
        }

        #endregion

        #region SelectEndpoint Tests

        [Fact]
        public void SelectEndpoint_SetsEndpoint()
        {
            // Arrange
            var vm = new SettingsViewModel();

            // Act
            vm.SelectEndpointCommand.Execute("http://192.168.1.100:11434");

            // Assert
            Assert.Equal("http://192.168.1.100:11434", vm.LlmEndpoint);
            Assert.Contains("Selected endpoint", vm.StatusMessage);
        }

        [Fact]
        public void SelectEndpoint_UpdatesStatusMessage()
        {
            // Arrange
            var vm = new SettingsViewModel();
            var endpoint = "http://test-endpoint:11434";

            // Act
            vm.SelectEndpointCommand.Execute(endpoint);

            // Assert
            Assert.Contains(endpoint, vm.StatusMessage);
        }

        #endregion

        #region RestartStackAsync Tests

        [Fact]
        public async Task RestartStackAsync_SuccessfulRestart_ExitsApplication()
        {
            // Arrange
            var mockService = new Mock<IStackRestartService>();
            mockService.Setup(s => s.Restart())
                .Returns(new StackRestartResult(true, "Restarting..."));

            var vm = new SettingsViewModel(new NullAgentConfigClient(), mockService.Object);

            // Note: We can't test Environment.Exit in unit tests, but we can verify the flag is set
            // Act & Assert - this will attempt to exit, so we just verify the service was called
            var task = vm.RestartStackCommand.ExecuteAsync(null);
            
            // Wait a bit for the command to start
            await Task.Delay(100);
            
            mockService.Verify(s => s.Restart(), Times.Once);
        }

        [Fact]
        public async Task RestartStackAsync_FailedRestart_ShowsError()
        {
            // Arrange
            var mockService = new Mock<IStackRestartService>();
            mockService.Setup(s => s.Restart())
                .Returns(new StackRestartResult(false, "Restart failed"));

            var vm = new SettingsViewModel(new NullAgentConfigClient(), mockService.Object);

            // Act
            await vm.RestartStackCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal("Restart failed", vm.StatusMessage);
            Assert.False(vm.IsRestarting);
        }

        [Fact]
        public async Task RestartStackAsync_Exception_ShowsError()
        {
            // Arrange
            var mockService = new Mock<IStackRestartService>();
            mockService.Setup(s => s.Restart())
                .Throws(new Exception("Unexpected error"));

            var vm = new SettingsViewModel(new NullAgentConfigClient(), mockService.Object);

            // Act
            await vm.RestartStackCommand.ExecuteAsync(null);

            // Assert
            Assert.Contains("Restart failed", vm.StatusMessage);
            Assert.Contains("Unexpected error", vm.StatusMessage);
            Assert.False(vm.IsRestarting);
        }

        [Fact]
        public async Task RestartStackAsync_AlreadyRestarting_DoesNotRestart()
        {
            // Arrange
            var mockService = new Mock<IStackRestartService>();
            var tcs = new TaskCompletionSource<bool>();
            mockService.Setup(s => s.Restart())
                .Returns(() =>
                {
                    tcs.Task.Wait();
                    return new StackRestartResult(false, "Done");
                });

            var vm = new SettingsViewModel(new NullAgentConfigClient(), mockService.Object);

            // Act
            var task1 = vm.RestartStackCommand.ExecuteAsync(null);
            await Task.Delay(50); // Let first restart start
            var task2 = vm.RestartStackCommand.ExecuteAsync(null);
            
            // Complete first restart
            tcs.SetResult(true);
            await task1;
            await task2;

            // Assert - restart should only be called once
            mockService.Verify(s => s.Restart(), Times.Once);
        }

        #endregion

        #region Property Tests

        [Fact]
        public void Properties_CanBeSetAndGet()
        {
            // Arrange
            var vm = new SettingsViewModel();

            // Act
            vm.EnableLlm = true;
            vm.LlmProvider = "OpenAI";
            vm.LlmEndpoint = "http://test:8080";
            vm.LlmModel = "gpt-4";
            vm.LlmTimeoutMs = 20000;
            vm.RequirePitForLlm = true;
            vm.EnableLlmDiscovery = true;
            vm.LlmDiscoveryTimeoutMs = 5000;
            vm.LlmDiscoveryPort = 8080;
            vm.LlmDiscoveryMaxConcurrency = 100;
            vm.LlmDiscoverySubnetPrefix = "10.0.0";

            // Assert
            Assert.True(vm.EnableLlm);
            Assert.Equal("OpenAI", vm.LlmProvider);
            Assert.Equal("http://test:8080", vm.LlmEndpoint);
            Assert.Equal("gpt-4", vm.LlmModel);
            Assert.Equal(20000, vm.LlmTimeoutMs);
            Assert.True(vm.RequirePitForLlm);
            Assert.True(vm.EnableLlmDiscovery);
            Assert.Equal(5000, vm.LlmDiscoveryTimeoutMs);
            Assert.Equal(8080, vm.LlmDiscoveryPort);
            Assert.Equal(100, vm.LlmDiscoveryMaxConcurrency);
            Assert.Equal("10.0.0", vm.LlmDiscoverySubnetPrefix);
        }

        [Fact]
        public void ApiKeyProperties_CanBeSetAndGet()
        {
            // Arrange
            var vm = new SettingsViewModel();

            // Act
            vm.OpenAiApiKey = "openai-key";
            vm.AnthropicApiKey = "anthropic-key";
            vm.OpenAiApiKeyConfigured = true;
            vm.AnthropicApiKeyConfigured = true;

            // Assert
            Assert.Equal("openai-key", vm.OpenAiApiKey);
            Assert.Equal("anthropic-key", vm.AnthropicApiKey);
            Assert.True(vm.OpenAiApiKeyConfigured);
            Assert.True(vm.AnthropicApiKeyConfigured);
        }

        #endregion

        #region Null Agent Config Client Tests

        [Fact]
        public async Task NullAgentConfigClient_GetConfigAsync_ReturnsEmptyConfig()
        {
            // Arrange
            var client = new NullAgentConfigClient();

            // Act
            var config = await client.GetConfigAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(config);
        }

        [Fact]
        public async Task NullAgentConfigClient_UpdateConfigAsync_ReturnsEmptyConfig()
        {
            // Arrange
            var client = new NullAgentConfigClient();
            var update = new AgentConfigUpdateDto();

            // Act
            var config = await client.UpdateConfigAsync(update, CancellationToken.None);

            // Assert
            Assert.NotNull(config);
        }

        #endregion

        #region Logging Tests

        [Fact]
        public async Task LoadSettingsAsync_WithLogger_LogsDebugMessages()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<SettingsViewModel>>();
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.GetConfigAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentConfigDto());

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService(), mockLogger.Object);

            // Act
            await vm.LoadSettingsCommand.ExecuteAsync(null);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Loading")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task LoadSettingsAsync_WithError_LogsError()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<SettingsViewModel>>();
            var mockClient = new Mock<IAgentConfigClient>();
            mockClient.Setup(c => c.GetConfigAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Test error"));

            var vm = new SettingsViewModel(mockClient.Object, new NullStackRestartService(), mockLogger.Object);

            // Act
            await vm.LoadSettingsCommand.ExecuteAsync(null);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion
    }

    internal class NullStackRestartService : IStackRestartService
    {
        public StackRestartResult Restart()
        {
            return new StackRestartResult(true, "Restart initiated");
        }
    }
}
