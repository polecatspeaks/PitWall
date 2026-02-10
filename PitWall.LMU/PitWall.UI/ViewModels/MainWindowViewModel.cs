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
	private readonly ISessionClient _sessionClient;
	private readonly TelemetryBuffer _telemetryBuffer;
	private CancellationTokenSource? _cts;
	private CancellationTokenSource? _telemetryCts;

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
			new NullAgentConfigClient(),
			new NullSessionClient())
	{
	}

	public MainWindowViewModel(
		IRecommendationClient recommendationClient,
		ITelemetryStreamClient telemetryStreamClient,
		IAgentQueryClient agentQueryClient,
		IAgentConfigClient agentConfigClient,
		ISessionClient sessionClient)
	{
		_recommendationClient = recommendationClient;
		_telemetryStreamClient = telemetryStreamClient;
		_sessionClient = sessionClient;
		_telemetryBuffer = new TelemetryBuffer(10000);

		// Initialize domain ViewModels (pass buffer to TelemetryAnalysis)
		Dashboard = new DashboardViewModel();
		TelemetryAnalysis = new TelemetryAnalysisViewModel(_telemetryBuffer);
		Strategy = new StrategyViewModel();
		AiAssistant = new AiAssistantViewModel(agentQueryClient);
		Settings = new SettingsViewModel(agentConfigClient);

		LoadSessionsCommand = new AsyncRelayCommand(() => LoadSessionsAsync(CancellationToken.None));
		ToggleReplayCommand = new RelayCommand(ToggleReplay);
		RestartReplayCommand = new RelayCommand(RestartReplay);
		ApplyReplaySettingsCommand = new RelayCommand(ApplyReplaySettings);
	}

	[ObservableProperty]
	private int selectedTabIndex;

	[ObservableProperty]
	private string currentLapDisplay = "15/30";

	[ObservableProperty]
	private string positionDisplay = "P3";

	[ObservableProperty]
	private string gapDisplay = "+2.3s";

	[ObservableProperty]
	private bool replayEnabled = true;

	[ObservableProperty]
	private int selectedSessionId = 1;

	[ObservableProperty]
	private int replayStartRow;

	[ObservableProperty]
	private int replayEndRow = -1;

	[ObservableProperty]
	private int replayIntervalMs = 100;

	[ObservableProperty]
	private bool isReplayPlaying = true;

	[ObservableProperty]
	private string replayStatusMessage = "Replay ready";

	public ObservableCollection<int> AvailableSessions { get; } = new();

	public IAsyncRelayCommand LoadSessionsCommand { get; }
	public IRelayCommand ToggleReplayCommand { get; }
	public IRelayCommand RestartReplayCommand { get; }
	public IRelayCommand ApplyReplaySettingsCommand { get; }

	public IReadOnlyList<int> ReplayIntervalsMs { get; } = new[] { 50, 100, 250, 500, 1000 };

	public Task StartAsync(string sessionId, CancellationToken cancellationToken)
	{
		_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		if (int.TryParse(sessionId, out var parsed) && parsed > 0)
		{
			SelectedSessionId = parsed;
		}

		_ = LoadSessionsAsync(CancellationToken.None);
		StartReplay();
		_ = Task.Run(() => PollRecommendationsAsync(_cts.Token), _cts.Token);

		// Load settings on startup
		_ = Settings.LoadSettingsCommand.ExecuteAsync(null);

		return Task.CompletedTask;
	}

	private async Task LoadSessionsAsync(CancellationToken cancellationToken)
	{
		try
		{
			ReplayStatusMessage = "Loading sessions...";
			var count = await _sessionClient.GetSessionCountAsync(cancellationToken);
			AvailableSessions.Clear();

			if (count <= 0)
			{
				ReplayStatusMessage = "No sessions found in telemetry database.";
				return;
			}

			for (var i = 1; i <= count; i++)
			{
				AvailableSessions.Add(i);
			}

			if (!AvailableSessions.Contains(SelectedSessionId))
			{
				SelectedSessionId = AvailableSessions[0];
			}

			ReplayStatusMessage = $"Loaded {count} session(s).";
		}
		catch (Exception ex)
		{
			ReplayStatusMessage = $"Failed to load sessions: {ex.Message}";
		}
	}

	public void Stop()
	{
		_cts?.Cancel();
		_cts?.Dispose();
		_cts = null;
		StopReplay();
	}

	private void StartReplay()
	{
		if (_cts == null || !ReplayEnabled || !IsReplayPlaying)
		{
			return;
		}

		StopReplay();
		_telemetryCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

		var sessionId = SelectedSessionId > 0 ? SelectedSessionId : 1;
		var startRow = Math.Max(0, ReplayStartRow);
		var endRow = ReplayEndRow;
		var intervalMs = Math.Max(0, ReplayIntervalMs);

		ReplayStatusMessage = $"Replaying session {sessionId}...";
		_ = Task.Run(() =>
			_telemetryStreamClient.ConnectAsync(sessionId, startRow, endRow, intervalMs, UpdateTelemetry, _telemetryCts.Token),
			_telemetryCts.Token);
	}

	private void StopReplay()
	{
		_telemetryCts?.Cancel();
		_telemetryCts?.Dispose();
		_telemetryCts = null;
	}

	private void ToggleReplay()
	{
		IsReplayPlaying = !IsReplayPlaying;
		ReplayStatusMessage = IsReplayPlaying ? "Replay running" : "Replay paused";
	}

	private void RestartReplay()
	{
		ReplayStartRow = 0;
		ReplayEndRow = -1;
		StartReplay();
	}

	private void ApplyReplaySettings()
	{
		StartReplay();
	}

	private void UpdateTelemetry(TelemetrySampleDto telemetry)
	{
		// Add to buffer
		_telemetryBuffer.Add(telemetry);

		// Update domain ViewModels
		Dashboard.UpdateTelemetry(telemetry);
		
		// Update telemetry analysis available laps periodically (every 10 samples to reduce overhead)
		if (_telemetryBuffer.Count % 10 == 0)
		{
			TelemetryAnalysis.RefreshAvailableLaps();
			
			// Auto-load the most recent completed lap for analysis
			var availableLaps = _telemetryBuffer.GetAvailableLaps();
			if (availableLaps.Length > 0)
			{
				var latestCompletedLap = availableLaps[^1];
				if (telemetry.LapNumber > latestCompletedLap && TelemetryAnalysis.CurrentLap != latestCompletedLap)
				{
					TelemetryAnalysis.LoadCurrentLapData(latestCompletedLap);
				}
			}
		}
	}

	private async Task PollRecommendationsAsync(CancellationToken cancellationToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
		while (await timer.WaitForNextTickAsync(cancellationToken))
		{
			try
			{
				var recommendation = await _recommendationClient.GetRecommendationAsync(SelectedSessionId.ToString(), cancellationToken);
				Dashboard.UpdateRecommendation(recommendation);
				Strategy.UpdateFromRecommendation(recommendation);
			}
			catch
			{
				// Silently continue on errors
			}
		}
	}

	partial void OnSelectedSessionIdChanged(int value)
	{
		if (ReplayEnabled && IsReplayPlaying)
		{
			StartReplay();
		}
	}

	partial void OnReplayEnabledChanged(bool value)
	{
		if (value)
		{
			StartReplay();
		}
		else
		{
			StopReplay();
			ReplayStatusMessage = "Replay disabled";
		}
	}

	partial void OnIsReplayPlayingChanged(bool value)
	{
		if (value)
		{
			StartReplay();
		}
		else
		{
			StopReplay();
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
		public Task ConnectAsync(int sessionId, int startRow, int endRow, int intervalMs, Action<TelemetrySampleDto> onMessage, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}

	private sealed class NullSessionClient : ISessionClient
	{
		public Task<int> GetSessionCountAsync(CancellationToken cancellationToken)
		{
			return Task.FromResult(0);
		}
	}
}
