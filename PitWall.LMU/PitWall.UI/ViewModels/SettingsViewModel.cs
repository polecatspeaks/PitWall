using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.UI.Models;
using PitWall.UI.Services;

namespace PitWall.UI.ViewModels;

/// <summary>
/// ViewModel for agent configuration settings including LLM providers,
/// discovery settings, and cloud provider credentials.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
	private readonly IAgentConfigClient _agentConfigClient;
	private readonly IStackRestartService _stackRestartService;
	private readonly ILogger<SettingsViewModel> _logger;

	public SettingsViewModel()
		: this(new NullAgentConfigClient(), new NullStackRestartService())
	{
	}

	public SettingsViewModel(
		IAgentConfigClient agentConfigClient,
		IStackRestartService stackRestartService,
		ILogger<SettingsViewModel>? logger = null)
	{
		_agentConfigClient = agentConfigClient;
		_stackRestartService = stackRestartService;
		_logger = logger ?? NullLogger<SettingsViewModel>.Instance;
	}

	public SettingsViewModel(IAgentConfigClient agentConfigClient, ILogger<SettingsViewModel>? logger = null)
		: this(agentConfigClient, new NullStackRestartService(), logger)
	{
	}

	[ObservableProperty]
	private bool enableLlm;

	[ObservableProperty]
	private string llmProvider = "Ollama";

	[ObservableProperty]
	private string llmEndpoint = string.Empty;

	[ObservableProperty]
	private string llmModel = string.Empty;

	[ObservableProperty]
	private int llmTimeoutMs = 5000;

	[ObservableProperty]
	private bool requirePitForLlm;

	[ObservableProperty]
	private bool enableLlmDiscovery;

	[ObservableProperty]
	private int llmDiscoveryTimeoutMs = 2000;

	[ObservableProperty]
	private int llmDiscoveryPort = 11434;

	[ObservableProperty]
	private int llmDiscoveryMaxConcurrency = 50;

	[ObservableProperty]
	private string? llmDiscoverySubnetPrefix;

	[ObservableProperty]
	private string openAiEndpoint = string.Empty;

	[ObservableProperty]
	private string openAiModel = string.Empty;

	[ObservableProperty]
	private string openAiApiKey = string.Empty;

	[ObservableProperty]
	private bool openAiApiKeyConfigured;

	[ObservableProperty]
	private string anthropicEndpoint = string.Empty;

	[ObservableProperty]
	private string anthropicModel = string.Empty;

	[ObservableProperty]
	private string anthropicApiKey = string.Empty;

	[ObservableProperty]
	private bool anthropicApiKeyConfigured;

	[ObservableProperty]
	private bool isSaving;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private string? statusMessage;

	[ObservableProperty]
	private bool isDiscovering;

	[ObservableProperty]
	private bool isRestarting;

	[ObservableProperty]
	private string llmStatusSummary = "LLM: Disabled | Provider: Ollama | Endpoint: Not set";

	public ObservableCollection<string> DiscoveredEndpoints { get; } = new();

	public IReadOnlyList<string> LlmProviders { get; } = new[] { "Ollama", "OpenAI", "Anthropic" };

	partial void OnEnableLlmChanged(bool value)
	{
		UpdateLlmStatusSummary();
	}

	partial void OnLlmProviderChanged(string value)
	{
		UpdateLlmStatusSummary();
	}

	partial void OnLlmEndpointChanged(string value)
	{
		UpdateLlmStatusSummary();
	}

	[RelayCommand]
	private async Task LoadSettingsAsync()
	{
		IsLoading = true;
		StatusMessage = null;
		_logger.LogDebug("Loading agent settings.");

		try
		{
			var config = await _agentConfigClient.GetConfigAsync(CancellationToken.None);
			
			EnableLlm = config.EnableLLM;
			LlmProvider = string.IsNullOrWhiteSpace(config.LLMProvider) ? "Ollama" : config.LLMProvider;
			LlmEndpoint = config.LLMEndpoint ?? string.Empty;
			LlmModel = config.LLMModel ?? string.Empty;
			LlmTimeoutMs = config.LLMTimeoutMs;
			RequirePitForLlm = config.RequirePitForLlm;
			EnableLlmDiscovery = config.EnableLLMDiscovery;
			LlmDiscoveryTimeoutMs = config.LLMDiscoveryTimeoutMs;
			LlmDiscoveryPort = config.LLMDiscoveryPort;
			LlmDiscoveryMaxConcurrency = config.LLMDiscoveryMaxConcurrency;
			LlmDiscoverySubnetPrefix = config.LLMDiscoverySubnetPrefix;
			OpenAiEndpoint = config.OpenAIEndpoint ?? string.Empty;
			OpenAiModel = config.OpenAIModel ?? string.Empty;
			OpenAiApiKeyConfigured = config.OpenAiApiKeyConfigured;
			AnthropicEndpoint = config.AnthropicEndpoint ?? string.Empty;
			AnthropicModel = config.AnthropicModel ?? string.Empty;
			AnthropicApiKeyConfigured = config.AnthropicApiKeyConfigured;

			UpdateLlmStatusSummary();

			StatusMessage = "Settings loaded successfully";
			_logger.LogDebug("Agent settings loaded.");
		}
		catch (Exception ex)
		{
			StatusMessage = $"Error loading settings: {ex.Message}";
			_logger.LogError(ex, "Failed to load agent settings.");
		}
		finally
		{
			IsLoading = false;
		}
	}

	private void UpdateLlmStatusSummary()
	{
		var enabled = EnableLlm ? "Enabled" : "Disabled";
		var provider = string.IsNullOrWhiteSpace(LlmProvider) ? "Unknown" : LlmProvider;
		var endpoint = string.IsNullOrWhiteSpace(LlmEndpoint) ? "Not set" : LlmEndpoint;
		LlmStatusSummary = $"LLM: {enabled} | Provider: {provider} | Endpoint: {endpoint}";
	}

	[RelayCommand]
	private async Task SaveSettingsAsync()
	{
		IsSaving = true;
		StatusMessage = null;
		_logger.LogDebug("Saving agent settings.");

		try
		{
			var update = new AgentConfigUpdateDto
			{
				EnableLLM = EnableLlm,
				LLMProvider = LlmProvider,
				LLMEndpoint = LlmEndpoint,
				LLMModel = LlmModel,
				LLMTimeoutMs = LlmTimeoutMs,
				RequirePitForLlm = RequirePitForLlm,
				EnableLLMDiscovery = EnableLlmDiscovery,
				LLMDiscoveryTimeoutMs = LlmDiscoveryTimeoutMs,
				LLMDiscoveryPort = LlmDiscoveryPort,
				LLMDiscoveryMaxConcurrency = LlmDiscoveryMaxConcurrency,
				LLMDiscoverySubnetPrefix = LlmDiscoverySubnetPrefix,
				OpenAiEndpoint = OpenAiEndpoint,
				OpenAiModel = OpenAiModel,
				OpenAiApiKey = string.IsNullOrWhiteSpace(OpenAiApiKey) ? null : OpenAiApiKey,
				AnthropicEndpoint = AnthropicEndpoint,
				AnthropicModel = AnthropicModel,
				AnthropicApiKey = string.IsNullOrWhiteSpace(AnthropicApiKey) ? null : AnthropicApiKey
			};

			var config = await _agentConfigClient.UpdateConfigAsync(update, CancellationToken.None);
			
			OpenAiApiKey = string.Empty;
			AnthropicApiKey = string.Empty;
			OpenAiApiKeyConfigured = config.OpenAiApiKeyConfigured;
			AnthropicApiKeyConfigured = config.AnthropicApiKeyConfigured;

			StatusMessage = "Settings saved successfully";
			_logger.LogDebug("Agent settings saved.");
		}
		catch (Exception ex)
		{
			StatusMessage = $"Error saving settings: {ex.Message}";
			_logger.LogError(ex, "Failed to save agent settings.");
		}
		finally
		{
			IsSaving = false;
		}
	}

	[RelayCommand]
	private async Task DiscoverLlmEndpointsAsync()
	{
		IsDiscovering = true;
		StatusMessage = "Discovering LLM endpoints on your network...";
		DiscoveredEndpoints.Clear();
		_logger.LogDebug("Starting LLM endpoint discovery.");

		try
		{
			var endpoints = await _agentConfigClient.DiscoverEndpointsAsync(CancellationToken.None);
			
			if (endpoints.Count == 0)
			{
				StatusMessage = "⚠️ No LLM endpoints found on the network. Check your subnet prefix, port, and ensure discovery is enabled.";
				_logger.LogWarning("No LLM endpoints discovered.");
			}
			else
			{
				foreach (var endpoint in endpoints)
				{
					DiscoveredEndpoints.Add(endpoint);
				}
				StatusMessage = $"✓ Discovery complete! Found {endpoints.Count} endpoint(s). Click 'Use' to select one.";
				_logger.LogDebug("Discovered {EndpointCount} LLM endpoints.", endpoints.Count);
			}
		}
		catch (Exception ex)
		{
			StatusMessage = $"❌ Discovery failed: {ex.Message}";
			_logger.LogError(ex, "LLM endpoint discovery failed.");
		}
		finally
		{
			IsDiscovering = false;
		}
	}

	[RelayCommand]
	private void SelectEndpoint(string endpoint)
	{
		LlmEndpoint = endpoint;
		StatusMessage = $"✓ Selected endpoint: {endpoint}";
		_logger.LogDebug("Selected LLM endpoint {Endpoint}.", endpoint);
	}

	[RelayCommand]
	private async Task RestartStackAsync()
	{
		if (IsRestarting)
		{
			return;
		}

		IsRestarting = true;
		StatusMessage = "Restarting PitWall stack...";
		_logger.LogInformation("Restarting PitWall stack from settings.");

		try
		{
			var result = _stackRestartService.Restart();
			StatusMessage = result.Message;
			if (result.Success)
			{
				await Task.Delay(300);
				var suppressExit = string.Equals(
					Environment.GetEnvironmentVariable("PITWALL_SUPPRESS_EXIT"),
					"1",
					StringComparison.OrdinalIgnoreCase);
				if (!suppressExit)
				{
					Environment.Exit(0);
				}
			}
		}
		catch (Exception ex)
		{
			StatusMessage = $"Restart failed: {ex.Message}";
			_logger.LogError(ex, "PitWall stack restart failed.");
		}
		finally
		{
			IsRestarting = false;
		}
	}
}

// Null implementation for design-time support
internal sealed class NullAgentConfigClient : IAgentConfigClient
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
		return Task.FromResult<IReadOnlyList<string>>(new[] { "http://localhost:11434", "http://192.168.1.100:11434" });
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
