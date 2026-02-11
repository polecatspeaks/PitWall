using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
	private readonly TrackMapService _trackMapService;
	private readonly ILogger<MainWindowViewModel> _logger;
	private readonly ILoggerFactory? _loggerFactory;
	private CancellationTokenSource? _cts;
	private CancellationTokenSource? _telemetryCts;

	// Domain-specific ViewModels
	public DashboardViewModel Dashboard { get; }
	public TelemetryAnalysisViewModel TelemetryAnalysis { get; }
	public StrategyViewModel Strategy { get; }
	public AiAssistantViewModel AiAssistant { get; }
	public SettingsViewModel Settings { get; }
	public TrackMapViewModel TrackMap { get; }

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
		ISessionClient sessionClient,
		ILogger<MainWindowViewModel>? logger = null,
		ILoggerFactory? loggerFactory = null)
	{
		_recommendationClient = recommendationClient;
		_telemetryStreamClient = telemetryStreamClient;
		_sessionClient = sessionClient;
		_telemetryBuffer = new TelemetryBuffer(10000);
		_logger = logger ?? NullLogger<MainWindowViewModel>.Instance;
		_loggerFactory = loggerFactory;

		// Initialize domain ViewModels (pass buffer to TelemetryAnalysis)
		Dashboard = new DashboardViewModel();
		TelemetryAnalysis = new TelemetryAnalysisViewModel(_telemetryBuffer);
		Strategy = new StrategyViewModel();
		AiAssistant = new AiAssistantViewModel(agentQueryClient);
		Settings = new SettingsViewModel(
			agentConfigClient,
			new StackRestartService(),
			_loggerFactory?.CreateLogger<SettingsViewModel>());
		TrackMap = new TrackMapViewModel();
		_trackMapService = new TrackMapService(_telemetryBuffer, new TrackMetadataStore());

		LoadSessionsCommand = new AsyncRelayCommand(() => LoadSessionsAsync(CancellationToken.None));
		ToggleReplayCommand = new RelayCommand(ToggleReplay);
		RestartReplayCommand = new RelayCommand(RestartReplay);
		ApplyReplaySettingsCommand = new RelayCommand(ApplyReplaySettings);
		SaveSessionMetadataCommand = new AsyncRelayCommand(SaveSessionMetadataAsync);
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
	private SessionSummaryDto? selectedSession;

	[ObservableProperty]
	private DateTime? sessionFilterFrom;

	[ObservableProperty]
	private DateTime? sessionFilterTo;

	[ObservableProperty]
	private string sessionFilterTrack = string.Empty;

	[ObservableProperty]
	private string sessionFilterCar = string.Empty;

	[ObservableProperty]
	private string sessionMetadataTrack = string.Empty;

	[ObservableProperty]
	private string sessionMetadataCar = string.Empty;

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

	[ObservableProperty]
	private string lastSampleReceived = "Last sample: --";

	public ObservableCollection<SessionSummaryDto> AvailableSessions { get; } = new();
	public ObservableCollection<SessionSummaryDto> FilteredSessions { get; } = new();

	public IAsyncRelayCommand LoadSessionsCommand { get; }
	public IRelayCommand ToggleReplayCommand { get; }
	public IRelayCommand RestartReplayCommand { get; }
	public IRelayCommand ApplyReplaySettingsCommand { get; }
	public IAsyncRelayCommand SaveSessionMetadataCommand { get; }

	public IReadOnlyList<int> ReplayIntervalsMs { get; } = new[] { 50, 100, 250, 500, 1000 };

	public Task StartAsync(string sessionId, CancellationToken cancellationToken)
	{
		_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		if (int.TryParse(sessionId, out var parsed) && parsed > 0)
		{
			SelectedSessionId = parsed;
		}

		_logger.LogInformation("UI start requested. Session {SessionId}", SelectedSessionId);

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
			_logger.LogDebug("Loading sessions from API.");
			var sessions = await _sessionClient.GetSessionSummariesAsync(cancellationToken);
			AvailableSessions.Clear();
			FilteredSessions.Clear();

			if (sessions.Count == 0)
			{
				ReplayStatusMessage = "No sessions found in telemetry database.";
				return;
			}

			foreach (var session in sessions
				.OrderByDescending(summary => summary.StartTimeUtc ?? DateTimeOffset.MinValue)
				.ThenBy(summary => summary.SessionId))
			{
				AvailableSessions.Add(session);
			}

			ApplySessionFilters();

			if (SelectedSession == null || !FilteredSessions.Any(session => session.SessionId == SelectedSession.SessionId))
			{
				SelectedSession = FilteredSessions.FirstOrDefault();
			}

			ReplayStatusMessage = $"Loaded {sessions.Count} session(s).";
			_logger.LogDebug("Loaded {SessionCount} sessions.", sessions.Count);
		}
		catch (Exception ex)
		{
			ReplayStatusMessage = $"Failed to load sessions: {ex.Message}";
			_logger.LogError(ex, "Failed to load sessions.");
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
		_logger.LogInformation("Starting replay. Session {SessionId}, start {StartRow}, end {EndRow}, interval {IntervalMs}.", sessionId, startRow, endRow, intervalMs);
		_ = Task.Run(
			() => RunTelemetryStreamAsync(sessionId, startRow, endRow, intervalMs, _telemetryCts.Token),
			_telemetryCts.Token);
	}

	private async Task RunTelemetryStreamAsync(int sessionId, int startRow, int endRow, int intervalMs, CancellationToken cancellationToken)
	{
		try
		{
			await _telemetryStreamClient.ConnectAsync(sessionId, startRow, endRow, intervalMs, UpdateTelemetry, cancellationToken);
			ReplayStatusMessage = "Replay finished";
		}
		catch (OperationCanceledException)
		{
			ReplayStatusMessage = "Replay stopped";
		}
		catch (Exception ex)
		{
			ReplayStatusMessage = $"Replay failed: {ex.Message}";
			_logger.LogError(ex, "Replay failed for session {SessionId}.", sessionId);
		}
	}

	private void StopReplay()
	{
		_telemetryCts?.Cancel();
		_telemetryCts?.Dispose();
		_telemetryCts = null;
		_logger.LogDebug("Replay stopped.");
	}

	private void ToggleReplay()
	{
		IsReplayPlaying = !IsReplayPlaying;
		ReplayStatusMessage = IsReplayPlaying ? "Replay running" : "Replay paused";
		_logger.LogDebug("Replay toggled. Playing={IsPlaying}", IsReplayPlaying);
	}

	private void RestartReplay()
	{
		ReplayStartRow = 0;
		ReplayEndRow = -1;
		_logger.LogDebug("Replay reset to default range.");
		StartReplay();
	}

	private void ApplyReplaySettings()
	{
		_logger.LogDebug("Applying replay settings.");
		StartReplay();
	}

	private void UpdateTelemetry(TelemetrySampleDto telemetry)
	{
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => ApplyTelemetry(telemetry));
			return;
		}

		ApplyTelemetry(telemetry);
	}

	private void ApplyTelemetry(TelemetrySampleDto telemetry)
	{
		// Add to buffer
		_telemetryBuffer.Add(telemetry);
		LastSampleReceived = $"Last sample: {DateTime.Now:HH:mm:ss}";

		// TEMP DEBUG: Show brake value in console and test output
		Console.WriteLine($"[ApplyTelemetry] Brake: {telemetry.BrakePosition:F3}, Throttle: {telemetry.ThrottlePosition:F3}, Steering: {telemetry.SteeringAngle:F3}");

		// Update domain ViewModels
		Dashboard.UpdateTelemetry(telemetry);

		var trackName = SelectedSession?.Track ?? SessionMetadataTrack;
		var frame = _trackMapService.Update(telemetry, trackName);
		TrackMap.UpdateFrame(frame);
		if (frame.SegmentStatus != null)
		{
			Dashboard.UpdateTrackContext(frame.SegmentStatus);
			TelemetryAnalysis.UpdateTrackContext(frame.SegmentStatus);
		}
		
		// Update telemetry analysis available laps periodically (every 10 samples to reduce overhead)
		if (_telemetryBuffer.Count % 10 == 0)
		{
			TelemetryAnalysis.RefreshAvailableLaps();
			
			// Auto-load the most recent completed lap for analysis
			var availableLaps = _telemetryBuffer.GetAvailableLaps();
			if (availableLaps.Length > 0)
			{
				var latestLap = availableLaps[^1];
				var hasCurrentLap = availableLaps.Contains(TelemetryAnalysis.CurrentLap);

				if (!hasCurrentLap || TelemetryAnalysis.SpeedData.Count == 0)
				{
					TelemetryAnalysis.LoadCurrentLapData(latestLap);
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
			catch (Exception ex)
			{
				_logger.LogDebug(ex, "Recommendation polling failed.");
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

	partial void OnSelectedSessionChanged(SessionSummaryDto? value)
	{
		if (value == null)
			return;

		SelectedSessionId = value.SessionId;
		SessionMetadataTrack = value.Track;
		SessionMetadataCar = value.Car;
		_trackMapService.Reset(value.Track);
	}

	partial void OnSessionFilterFromChanged(DateTime? value)
	{
		ApplySessionFilters();
	}

	partial void OnSessionFilterToChanged(DateTime? value)
	{
		ApplySessionFilters();
	}

	partial void OnSessionFilterTrackChanged(string value)
	{
		ApplySessionFilters();
	}

	partial void OnSessionFilterCarChanged(string value)
	{
		ApplySessionFilters();
	}

	private void ApplySessionFilters()
	{
		FilteredSessions.Clear();

		var trackFilter = SessionFilterTrack?.Trim();
		var carFilter = SessionFilterCar?.Trim();
		var fromDate = SessionFilterFrom?.Date;
		var toDate = SessionFilterTo?.Date;

		foreach (var session in AvailableSessions)
		{
			if (!MatchesTrack(session, trackFilter))
				continue;
			if (!MatchesCar(session, carFilter))
				continue;
			if (!MatchesDate(session, fromDate, toDate))
				continue;

			FilteredSessions.Add(session);
		}

		if (SelectedSession == null || !FilteredSessions.Any(session => session.SessionId == SelectedSession.SessionId))
		{
			SelectedSession = FilteredSessions.FirstOrDefault();
		}
	}

	private static bool MatchesTrack(SessionSummaryDto session, string? trackFilter)
	{
		if (string.IsNullOrWhiteSpace(trackFilter))
			return true;

		return session.Track.Contains(trackFilter, StringComparison.OrdinalIgnoreCase);
	}

	private static bool MatchesCar(SessionSummaryDto session, string? carFilter)
	{
		if (string.IsNullOrWhiteSpace(carFilter))
			return true;

		return session.Car.Contains(carFilter, StringComparison.OrdinalIgnoreCase);
	}

	private static bool MatchesDate(SessionSummaryDto session, DateTime? fromDate, DateTime? toDate)
	{
		if (!fromDate.HasValue && !toDate.HasValue)
			return true;

		if (!session.StartTimeUtc.HasValue)
			return false;

		var localDate = session.StartTimeUtc.Value.ToLocalTime().Date;
		if (fromDate.HasValue && localDate < fromDate.Value)
			return false;
		if (toDate.HasValue && localDate > toDate.Value)
			return false;

		return true;
	}

	private async Task SaveSessionMetadataAsync()
	{
		if (SelectedSession == null)
			return;

		try
		{
			var update = new SessionMetadataUpdateDto
			{
				Track = SessionMetadataTrack,
				Car = SessionMetadataCar
			};

			var summary = await _sessionClient.UpdateSessionMetadataAsync(SelectedSession.SessionId, update, CancellationToken.None);
			if (summary != null)
			{
				UpdateSessionSummary(summary);
				ReplayStatusMessage = $"Saved metadata for session {summary.SessionId}.";
			}
		}
		catch (Exception ex)
		{
			ReplayStatusMessage = $"Failed to save session metadata: {ex.Message}";
			_logger.LogError(ex, "Failed to save session metadata.");
		}
	}

	private void UpdateSessionSummary(SessionSummaryDto summary)
	{
		var index = AvailableSessions.ToList().FindIndex(session => session.SessionId == summary.SessionId);
		if (index >= 0)
		{
			AvailableSessions[index] = summary;
		}
		ApplySessionFilters();
		SelectedSession = FilteredSessions.FirstOrDefault(session => session.SessionId == summary.SessionId) ?? SelectedSession;
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

		public Task<IReadOnlyList<SessionSummaryDto>> GetSessionSummariesAsync(CancellationToken cancellationToken)
		{
			return Task.FromResult<IReadOnlyList<SessionSummaryDto>>(Array.Empty<SessionSummaryDto>());
		}

		public Task<SessionSummaryDto?> UpdateSessionMetadataAsync(int sessionId, SessionMetadataUpdateDto update, CancellationToken cancellationToken)
		{
			return Task.FromResult<SessionSummaryDto?>(null);
		}
	}
}
