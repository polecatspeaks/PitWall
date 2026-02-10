using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PitWall.UI.Models;
using PitWall.UI.Services;

namespace PitWall.UI.ViewModels;

/// <summary>
/// Main orchestrator ViewModel for the PitWall application.
/// Manages WebSocket connections, telemetry streaming, and delegates
/// to domain-specific ViewModels.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
	private readonly IRecommendationClient _recommendationClient;
	private readonly ITelemetryStreamClient _telemetryStreamClient;
	private readonly TelemetryBuffer _telemetryBuffer;
	private CancellationTokenSource? _cts;

	// Domain-specific ViewModels
	public DashboardViewModel Dashboard { get; }
	public TelemetryAnalysisViewModel TelemetryAnalysis { get; }
	public StrategyViewModel Strategy { get; }
	public AiAssistantViewModel AiAssistant { get; }
	public SettingsViewModel Settings { get; }

	public MainWindowViewModel()
		: this(
			new NullRecommendationClient(), 
			new NullTelemetryStreamClient(), 
			new NullAgentQueryClient(), 
			new NullAgentConfigClient())
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
		_telemetryBuffer = new TelemetryBuffer(10000);

		// Initialize domain ViewModels
		Dashboard = new DashboardViewModel();
		TelemetryAnalysis = new TelemetryAnalysisViewModel();
		Strategy = new StrategyViewModel();
		AiAssistant = new AiAssistantViewModel(agentQueryClient);
		Settings = new SettingsViewModel(agentConfigClient);
	}

	[ObservableProperty]
	private int selectedTabIndex;

	[ObservableProperty]
	private string currentLapDisplay = "15/30";

	[ObservableProperty]
	private string positionDisplay = "P3";

	[ObservableProperty]
	private string gapDisplay = "+2.3s";

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

		// Load settings on startup
		_ = Settings.LoadSettingsCommand.ExecuteAsync(null);

		return Task.CompletedTask;
	}

	public void Stop()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = null;
	}

	private void UpdateTelemetry(TelemetrySampleDto telemetry)
	{
		// Add to buffer
		_telemetryBuffer.Add(telemetry);

		// Update domain ViewModels
		Dashboard.UpdateTelemetry(telemetry);
		
		// TODO: Update TelemetryAnalysis with latest data
		// TODO: Update race context for AI Assistant
	}

	private async Task PollRecommendationsAsync(string sessionId, CancellationToken cancellationToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
		while (await timer.WaitForNextTickAsync(cancellationToken))
		{
			try
			{
				var recommendation = await _recommendationClient.GetRecommendationAsync(sessionId, cancellationToken);
				Dashboard.UpdateRecommendation(recommendation);
				Strategy.UpdateFromRecommendation(recommendation);
			}
			catch
			{
				// Silently continue on errors
			}
		}
	}

	// Null implementations for design-time support
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
}
