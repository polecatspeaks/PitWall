using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

	public SettingsViewModel()
		: this(new NullAgentConfigClient())
	{
	}

	public SettingsViewModel(IAgentConfigClient agentConfigClient)
	{
		_agentConfigClient = agentConfigClient;
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

	public ObservableCollection<string> DiscoveredEndpoints { get; } = new();

	public IReadOnlyList<string> LlmProviders { get; } = new[] { "Ollama", "OpenAI", "Anthropic" };

	[RelayCommand]
	private async Task LoadSettingsAsync()
	{
		IsLoading = true;
		StatusMessage = null;

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

			StatusMessage = "Settings loaded successfully";
		}
		catch (Exception ex)
		{
			StatusMessage = $"Error loading settings: {ex.Message}";
		}
		finally
		{
			IsLoading = false;
		}
	}

	[RelayCommand]
	private async Task SaveSettingsAsync()
	{
		IsSaving = true;
		StatusMessage = null;

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
		}
		catch (Exception ex)
		{
			StatusMessage = $"Error saving settings: {ex.Message}";
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

		try
		{
			var endpoints = await _agentConfigClient.DiscoverEndpointsAsync(CancellationToken.None);
			
			if (endpoints.Count == 0)
			{
				StatusMessage = "⚠️ No LLM endpoints found on the network. Check your subnet prefix, port, and ensure discovery is enabled.";
			}
			else
			{
				foreach (var endpoint in endpoints)
				{
					DiscoveredEndpoints.Add(endpoint);
				}
				StatusMessage = $"✓ Discovery complete! Found {endpoints.Count} endpoint(s). Click 'Use' to select one.";
			}
		}
		catch (Exception ex)
		{
			StatusMessage = $"❌ Discovery failed: {ex.Message}";
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
}
