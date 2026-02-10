using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PitWall.UI.Models;
using PitWall.UI.Services;

namespace PitWall.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
	private const double TyreOverheatThreshold = 110.0;
	private const double CriticalFuelLevel = 2.0;
	private const double WheelLockBrakeThreshold = 0.9;
	private const double WheelLockSpeedKph = 120.0;
	private const double WheelLockThrottleMax = 0.05;
	private const double BrakeOverlapThreshold = 0.6;
	private const double ThrottleOverlapThreshold = 0.2;
	private const double HeavyBrakeSteerThreshold = 0.6;
	private readonly IRecommendationClient _recommendationClient;
	private readonly ITelemetryStreamClient _telemetryStreamClient;
	private readonly IAgentQueryClient _agentQueryClient;
	private readonly IAgentConfigClient _agentConfigClient;
	private CancellationTokenSource? _cts;

	public MainWindowViewModel()
		: this(new NullRecommendationClient(), new NullTelemetryStreamClient(), new NullAgentQueryClient(), new NullAgentConfigClient())
	{
	}

	public MainWindowViewModel(
		IRecommendationClient recommendationClient,
		ITelemetryStreamClient telemetryStreamClient,
		IAgentQueryClient agentQueryClient,
		IAgentConfigClient agentConfigClient)
	{
		_recommendationClient = recommendationClient;
		_telemetryStreamClient = telemetryStreamClient;
		_agentQueryClient = agentQueryClient;
		_agentConfigClient = agentConfigClient;
		SendAiQueryCommand = new AsyncRelayCommand(() => SendAiQueryAsync(CancellationToken.None));
		LoadSettingsCommand = new AsyncRelayCommand(() => LoadSettingsAsync(CancellationToken.None));
		SaveSettingsCommand = new AsyncRelayCommand(() => SaveSettingsAsync(CancellationToken.None));
		RunDiscoveryCommand = new AsyncRelayCommand(() => RunDiscoveryAsync(CancellationToken.None));
	}

	[ObservableProperty]
	private string fuelLiters = "-- L";

	[ObservableProperty]
	private string fuelLaps = "-- LAPS";

	[ObservableProperty]
	private string speedDisplay = "-- KPH";

	[ObservableProperty]
	private string lapDisplay = "--/--";

	[ObservableProperty]
	private string timingLastLap = "LAST --";

	[ObservableProperty]
	private string timingBestLap = "BEST --";

	[ObservableProperty]
	private string timingDelta = "DELTA --";

	[ObservableProperty]
	private string tiresLine1 = "FL --  --  |  FR --  --";

	[ObservableProperty]
	private string tiresLine2 = "RL --  --  |  RR --  --";

	[ObservableProperty]
	private string strategyMessage = "Awaiting strategy...";

	[ObservableProperty]
	private string strategyConfidence = "CONF --";

	[ObservableProperty]
	private string alertsDisplay = "None";

	[ObservableProperty]
	private string aiInput = string.Empty;

	[ObservableProperty]
	private bool enableLlm;

	[ObservableProperty]
	private string llmProvider = "Ollama";

	[ObservableProperty]
	private string llmEndpoint = string.Empty;

	[ObservableProperty]
	private string llmModel = string.Empty;

	[ObservableProperty]
	private string llmTimeoutMs = string.Empty;

	[ObservableProperty]
	private bool requirePitForLlm;

	[ObservableProperty]
	private bool enableLlmDiscovery;

	[ObservableProperty]
	private string llmDiscoveryTimeoutMs = string.Empty;

	[ObservableProperty]
	private string llmDiscoveryPort = string.Empty;

	[ObservableProperty]
	private string llmDiscoveryMaxConcurrency = string.Empty;

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
	private string discoveryResultsMessage = string.Empty;

	public ObservableCollection<AiMessage> AiMessages { get; } = new();

	public IAsyncRelayCommand SendAiQueryCommand { get; }
	public IAsyncRelayCommand LoadSettingsCommand { get; }
	public IAsyncRelayCommand SaveSettingsCommand { get; }
	public IAsyncRelayCommand RunDiscoveryCommand { get; }

	public IReadOnlyList<string> LlmProviders { get; } = new[] { "Ollama", "OpenAI", "Anthropic" };

	public async Task SendAiQueryAsync(CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(AiInput))
		{
			return;
		}

		var userMessage = new AiMessage
		{
			Role = "User",
			Text = AiInput.Trim()
		};

		AiMessages.Add(userMessage);
		AiInput = string.Empty;

		try
		{
			var response = await _agentQueryClient.SendQueryAsync(userMessage.Text, cancellationToken);
			AiMessages.Add(new AiMessage
			{
				Role = "Assistant",
				Text = response.Answer,
				Source = response.Source
			});
		}
		catch (Exception ex)
		{
			AiMessages.Add(new AiMessage
			{
				Role = "Assistant",
				Text = $"Error: {ex.Message}",
				Source = "System"
			});
		}
	}

	public Task StartAsync(string sessionId, CancellationToken cancellationToken)
	{
		_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

		_ = Task.Run(() =>
			_telemetryStreamClient.ConnectAsync(
				int.TryParse(sessionId, out var parsed) ? parsed : 1,
				UpdateTelemetry,
				_cts.Token),
			_cts.Token);

		_ = Task.Run(() => PollRecommendationsAsync(sessionId, _cts.Token), _cts.Token);

		return Task.CompletedTask;
	}

	public async Task LoadSettingsAsync(CancellationToken cancellationToken)
	{
		try
		{
			var config = await _agentConfigClient.GetConfigAsync(cancellationToken);
			if (config == null)
			{
				return; // Keep defaults
			}

			EnableLlm = config.EnableLLM;
			LlmProvider = string.IsNullOrWhiteSpace(config.LLMProvider) ? "Ollama" : config.LLMProvider;
			LlmEndpoint = config.LLMEndpoint ?? string.Empty;
			LlmModel = config.LLMModel ?? string.Empty;
			LlmTimeoutMs = config.LLMTimeoutMs > 0 ? config.LLMTimeoutMs.ToString() : string.Empty;
			RequirePitForLlm = config.RequirePitForLlm;
			EnableLlmDiscovery = config.EnableLLMDiscovery;
			LlmDiscoveryTimeoutMs = config.LLMDiscoveryTimeoutMs > 0 ? config.LLMDiscoveryTimeoutMs.ToString() : string.Empty;
			LlmDiscoveryPort = config.LLMDiscoveryPort > 0 ? config.LLMDiscoveryPort.ToString() : string.Empty;
			LlmDiscoveryMaxConcurrency = config.LLMDiscoveryMaxConcurrency > 0 ? config.LLMDiscoveryMaxConcurrency.ToString() : string.Empty;
			LlmDiscoverySubnetPrefix = config.LLMDiscoverySubnetPrefix;
			OpenAiEndpoint = config.OpenAIEndpoint ?? string.Empty;
			OpenAiModel = config.OpenAIModel ?? string.Empty;
			OpenAiApiKeyConfigured = config.OpenAiApiKeyConfigured;
			AnthropicEndpoint = config.AnthropicEndpoint ?? string.Empty;
			AnthropicModel = config.AnthropicModel ?? string.Empty;
			AnthropicApiKeyConfigured = config.AnthropicApiKeyConfigured;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failed to load settings: {ex.Message}");
			// Keep defaults
		}
	}

	public async Task SaveSettingsAsync(CancellationToken cancellationToken)
	{
		try
		{
			var update = new AgentConfigUpdateDto
			{
				EnableLLM = EnableLlm,
				LLMProvider = LlmProvider,
				LLMEndpoint = LlmEndpoint,
				LLMModel = LlmModel,
				LLMTimeoutMs = int.TryParse(LlmTimeoutMs, out var timeout) ? timeout : 30000,
				RequirePitForLlm = RequirePitForLlm,
				EnableLLMDiscovery = EnableLlmDiscovery,
				LLMDiscoveryTimeoutMs = int.TryParse(LlmDiscoveryTimeoutMs, out var discoveryTimeout) ? discoveryTimeout : 5000,
				LLMDiscoveryPort = int.TryParse(LlmDiscoveryPort, out var port) ? port : 11434,
				LLMDiscoveryMaxConcurrency = int.TryParse(LlmDiscoveryMaxConcurrency, out var concurrency) ? concurrency : 10,
				LLMDiscoverySubnetPrefix = LlmDiscoverySubnetPrefix,
				OpenAiEndpoint = OpenAiEndpoint,
				OpenAiModel = OpenAiModel,
				OpenAiApiKey = string.IsNullOrWhiteSpace(OpenAiApiKey) ? null : OpenAiApiKey,
				AnthropicEndpoint = AnthropicEndpoint,
				AnthropicModel = AnthropicModel,
				AnthropicApiKey = string.IsNullOrWhiteSpace(AnthropicApiKey) ? null : AnthropicApiKey
			};

			var config = await _agentConfigClient.UpdateConfigAsync(update, cancellationToken);
			OpenAiApiKey = string.Empty;
			AnthropicApiKey = string.Empty;
			OpenAiApiKeyConfigured = config.OpenAiApiKeyConfigured;
			AnthropicApiKeyConfigured = config.AnthropicApiKeyConfigured;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failed to save settings: {ex.Message}");
			// Could show error to user in future
		}
	}

	public async Task RunDiscoveryAsync(CancellationToken cancellationToken)
	{
		try
		{
			DiscoveryResultsMessage = "Scanning network...";
			var result = await _agentConfigClient.RunDiscoveryAsync(cancellationToken);

			if (result.Endpoints == null || result.Endpoints.Count == 0)
			{
				DiscoveryResultsMessage = "No LLM endpoints found on network";
			}
			else
			{
				DiscoveryResultsMessage = $"Found {result.Endpoints.Count} endpoint(s): {string.Join(", ", result.Endpoints)}";
			}
		}
		catch (Exception ex)
		{
			DiscoveryResultsMessage = $"Discovery failed: {ex.Message}";
			Console.WriteLine($"Discovery error: {ex.Message}");
		}
	}

	public void Stop()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = null;
	}

	public void UpdateTelemetry(TelemetrySampleDto telemetry)
	{
		FuelLiters = $"{telemetry.FuelLiters:0.0} L";
		SpeedDisplay = $"{telemetry.SpeedKph:0.0} KPH";
		LapDisplay = FormatLapDisplay(telemetry.CurrentLap, telemetry.TotalLaps);
		TimingLastLap = $"LAST {FormatLapTime(telemetry.LastLapTime)}";
		TimingBestLap = $"BEST {FormatLapTime(telemetry.BestLapTime)}";
		TimingDelta = $"DELTA {FormatLapDelta(telemetry.LastLapTime, telemetry.BestLapTime)}";
		AlertsDisplay = BuildAlertsDisplay(telemetry);

		if (telemetry.TyreTempsC.Length >= 4)
		{
			TiresLine1 = $"FL {telemetry.TyreTempsC[0]:0}C  --  |  FR {telemetry.TyreTempsC[1]:0}C  --";
			TiresLine2 = $"RL {telemetry.TyreTempsC[2]:0}C  --  |  RR {telemetry.TyreTempsC[3]:0}C  --";
		}
		else
		{
			TiresLine1 = "FL --  --  |  FR --  --";
			TiresLine2 = "RL --  --  |  RR --  --";
		}
	}

	private static string BuildAlertsDisplay(TelemetrySampleDto telemetry)
	{
		var alerts = new List<string>();
		if (telemetry.FuelLiters <= CriticalFuelLevel)
		{
			alerts.Add("FUEL CRITICAL");
		}

		if (telemetry.TyreTempsC.Any(t => t >= TyreOverheatThreshold))
		{
			alerts.Add("TIRE OVERHEAT");
		}

		if (telemetry.Brake >= WheelLockBrakeThreshold
			&& telemetry.SpeedKph >= WheelLockSpeedKph
			&& telemetry.Throttle <= WheelLockThrottleMax)
		{
			alerts.Add("WHEEL LOCK RISK");
		}

		if (telemetry.Brake >= BrakeOverlapThreshold && telemetry.Throttle >= ThrottleOverlapThreshold)
		{
			alerts.Add("BRAKE/THROTTLE OVERLAP");
		}

		if (telemetry.Brake >= BrakeOverlapThreshold && Math.Abs(telemetry.Steering) >= HeavyBrakeSteerThreshold)
		{
			alerts.Add("HEAVY BRAKE + STEER");
		}

		return alerts.Count == 0 ? "None" : string.Join(" | ", alerts);
	}

	private static string FormatLapDisplay(int? currentLap, int? totalLaps)
	{
		if (currentLap.HasValue && totalLaps.HasValue && totalLaps > 0)
		{
			return $"{currentLap}/{totalLaps}";
		}

		return "--/--";
	}

	private static string FormatLapTime(double? seconds)
	{
		if (!seconds.HasValue || seconds <= 0)
		{
			return "--";
		}

		var time = TimeSpan.FromSeconds(seconds.Value);
		return $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds:000}";
	}

	private static string FormatLapDelta(double? lastSeconds, double? bestSeconds)
	{
		if (!lastSeconds.HasValue || !bestSeconds.HasValue || lastSeconds <= 0 || bestSeconds <= 0)
		{
			return "--";
		}

		var delta = lastSeconds.Value - bestSeconds.Value;
		var sign = delta >= 0 ? "+" : "";
		return $"{sign}{delta:0.000}";
	}

	public void UpdateRecommendation(RecommendationDto recommendation)
	{
		StrategyMessage = string.IsNullOrWhiteSpace(recommendation.Recommendation)
			? "Awaiting strategy..."
			: recommendation.Recommendation;

		StrategyConfidence = $"CONF {recommendation.Confidence:0.00}";
	}

	private async Task PollRecommendationsAsync(string sessionId, CancellationToken cancellationToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
		while (await timer.WaitForNextTickAsync(cancellationToken))
		{
			var recommendation = await _recommendationClient.GetRecommendationAsync(sessionId, cancellationToken);
			UpdateRecommendation(recommendation);
		}
	}

	private sealed class NullRecommendationClient : IRecommendationClient
	{
		public Task<RecommendationDto> GetRecommendationAsync(string sessionId, CancellationToken cancellationToken)
		{
			return Task.FromResult(new RecommendationDto());
		}
	}

	private sealed class NullTelemetryStreamClient : ITelemetryStreamClient
	{
		public Task ConnectAsync(int sessionId, Action<TelemetrySampleDto> onMessage, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}

	private sealed class NullAgentQueryClient : IAgentQueryClient
	{
		public Task<AgentResponseDto> SendQueryAsync(string query, CancellationToken cancellationToken)
		{
			return Task.FromResult(new AgentResponseDto
			{
				Answer = "",
				Source = "",
				Success = false
			});
		}
	}

	private sealed class NullAgentConfigClient : IAgentConfigClient
	{
		public Task<AgentConfigDto> GetConfigAsync(CancellationToken cancellationToken)
		{
			return Task.FromResult(new AgentConfigDto());
		}

		public Task<AgentConfigDto> UpdateConfigAsync(AgentConfigUpdateDto update, CancellationToken cancellationToken)
		{
			return Task.FromResult(new AgentConfigDto());
		}

		public Task<DiscoveryResultDto> RunDiscoveryAsync(CancellationToken cancellationToken)
		{
			return Task.FromResult(new DiscoveryResultDto());
		}
	}
}
