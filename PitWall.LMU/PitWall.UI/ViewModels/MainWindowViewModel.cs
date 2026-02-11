using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
	private readonly CarSpecStore _carSpecStore;
	private readonly ILogger<MainWindowViewModel> _logger;
	private readonly ILoggerFactory? _loggerFactory;
	private CancellationTokenSource? _cts;
	private CancellationTokenSource? _telemetryCts;
	private readonly object _telemetryUpdateLock = new();
	private TelemetrySampleDto? _pendingTelemetry;
	private int _telemetryUpdateScheduled;
	private long _lastUiUpdateTicks;
	private const double UiUpdateIntervalSeconds = 0.05;
	private int _lastLapNumber = int.MinValue;
	private bool _usePreloadedReplay;
	private int _preloadedSessionId = -1;
	private List<TelemetrySampleDto> _preloadedSamples = new();
	private int _playbackIndex;
	private int _playbackStartIndex;
	private int _playbackEndIndex;
	private double _playbackRate = 1.0;
	private const int SeekStepSamples = 1000;

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
		_carSpecStore = new CarSpecStore();

		LoadSessionsCommand = new AsyncRelayCommand(() => LoadSessionsAsync(CancellationToken.None));
		ToggleReplayCommand = new RelayCommand(ToggleReplay);
		RestartReplayCommand = new RelayCommand(RestartReplay);
		ApplyReplaySettingsCommand = new RelayCommand(ApplyReplaySettings);
		SaveSessionMetadataCommand = new AsyncRelayCommand(SaveSessionMetadataAsync);
		PlayReplayCommand = new RelayCommand(PlayReplay);
		PauseReplayCommand = new RelayCommand(PauseReplay);
		StopReplayCommand = new RelayCommand(StopReplayPlayback);
		FastForwardCommand = new RelayCommand(() => SeekBy(SeekStepSamples));
		RewindCommand = new RelayCommand(() => SeekBy(-SeekStepSamples));
		HalfSpeedForwardCommand = new RelayCommand(() => SetPlaybackRate(0.5));
		QuarterSpeedForwardCommand = new RelayCommand(() => SetPlaybackRate(0.25));
		HalfSpeedReverseCommand = new RelayCommand(() => SetPlaybackRate(-0.5));
		QuarterSpeedReverseCommand = new RelayCommand(() => SetPlaybackRate(-0.25));
	}

	[ObservableProperty]
	private int selectedTabIndex;

	[ObservableProperty]
	private string currentLapDisplay = "--/--";

	[ObservableProperty]
	private string positionDisplay = "P--";

	[ObservableProperty]
	private string gapDisplay = "--";

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
	private string sessionMetadataTrackId = string.Empty;

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
	private bool isReplayPaused;

	[ObservableProperty]
	private double replaySpeed = 1.0;

	[ObservableProperty]
	private int replaySampleIndex;

	[ObservableProperty]
	private int replayTotalSamples;

	[ObservableProperty]
	private double replayProgressPercent;

	[ObservableProperty]
	private bool isPreloading;

	[ObservableProperty]
	private string preloadStatusMessage = "Ready";

	[ObservableProperty]
	private string lastSampleReceived = "Last sample: --";

	public ObservableCollection<SessionSummaryDto> AvailableSessions { get; } = new();
	public ObservableCollection<SessionSummaryDto> FilteredSessions { get; } = new();

	public IAsyncRelayCommand LoadSessionsCommand { get; }
	public IRelayCommand ToggleReplayCommand { get; }
	public IRelayCommand RestartReplayCommand { get; }
	public IRelayCommand ApplyReplaySettingsCommand { get; }
	public IAsyncRelayCommand SaveSessionMetadataCommand { get; }
	public IRelayCommand PlayReplayCommand { get; }
	public IRelayCommand PauseReplayCommand { get; }
	public IRelayCommand StopReplayCommand { get; }
	public IRelayCommand FastForwardCommand { get; }
	public IRelayCommand RewindCommand { get; }
	public IRelayCommand HalfSpeedForwardCommand { get; }
	public IRelayCommand QuarterSpeedForwardCommand { get; }
	public IRelayCommand HalfSpeedReverseCommand { get; }
	public IRelayCommand QuarterSpeedReverseCommand { get; }

	public IReadOnlyList<int> ReplayIntervalsMs { get; } = new[] { 0, 10, 50, 100, 250, 500, 1000 };

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
		IsReplayPaused = false;
		_logger.LogInformation("Starting replay. Session {SessionId}, start {StartRow}, end {EndRow}, interval {IntervalMs}.", sessionId, startRow, endRow, intervalMs);
		_ = Task.Run(
			() => RunPreloadedReplayAsync(sessionId, startRow, endRow, intervalMs, _telemetryCts.Token),
			_telemetryCts.Token);
	}

	private async Task RunPreloadedReplayAsync(int sessionId, int startRow, int endRow, int intervalMs, CancellationToken cancellationToken)
	{
		try
		{
			await EnsurePreloadedAsync(sessionId, cancellationToken);
			if (_preloadedSamples.Count == 0)
			{
				ReplayStatusMessage = "Replay has no samples";
				return;
			}

			var lastIndex = _preloadedSamples.Count - 1;
			var startIndex = Math.Clamp(startRow, 0, lastIndex);
			var endIndex = endRow >= 0 ? Math.Min(endRow, lastIndex) : lastIndex;
			if (endIndex < startIndex)
			{
				ReplayStatusMessage = "Replay range is empty";
				return;
			}

			_playbackStartIndex = startIndex;
			_playbackEndIndex = endIndex;
			_playbackIndex = Math.Clamp(_playbackIndex, _playbackStartIndex, _playbackEndIndex);
			SetReplayProgress(_playbackIndex);

			_playbackIndex = _playbackRate >= 0 ? _playbackStartIndex : _playbackEndIndex;
			SetReplayProgress(_playbackIndex);

			while (_playbackIndex >= _playbackStartIndex && _playbackIndex <= _playbackEndIndex)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!IsReplayPlaying)
				{
					IsReplayPaused = true;
					await Task.Delay(50, cancellationToken);
					continue;
				}
				IsReplayPaused = false;

				UpdateTelemetry(_preloadedSamples[_playbackIndex]);
				SetReplayProgress(_playbackIndex);

				if (intervalMs > 0)
				{
					var delayMs = (int)Math.Max(1, intervalMs / Math.Abs(_playbackRate));
					await Task.Delay(delayMs, cancellationToken);
				}

				var step = _playbackRate >= 0 ? 1 : -1;
				_playbackIndex += step;
			}
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

	private async Task EnsurePreloadedAsync(int sessionId, CancellationToken cancellationToken)
	{
		if (_preloadedSessionId == sessionId && _preloadedSamples.Count > 0)
		{
			_usePreloadedReplay = true;
			return;
		}

		ReplayStatusMessage = $"Preloading session {sessionId}...";
		IsPreloading = true;
		PreloadStatusMessage = "Starting preload...";
		var samples = await LoadAllSamplesAsync(sessionId, cancellationToken);
		_preloadedSamples = samples;
		_preloadedSessionId = sessionId;
		_usePreloadedReplay = true;
		_lastLapNumber = int.MinValue;

		_telemetryBuffer.ReplaceAll(samples);
		ReplayTotalSamples = samples.Count;
		SetReplayProgress(0);
		await Dispatcher.UIThread.InvokeAsync(() =>
		{
			TelemetryAnalysis.RefreshAvailableLaps();
			var laps = _telemetryBuffer.GetAvailableLaps();
			if (laps.Length > 0)
			{
				TelemetryAnalysis.LoadCurrentLapData(laps[^1]);
			}
		});
		IsPreloading = false;

		ReplayStatusMessage = samples.Count == 0
			? "Replay has no samples"
			: $"Preloaded {samples.Count} samples";
		PreloadStatusMessage = ReplayStatusMessage;
	}

	private async Task<List<TelemetrySampleDto>> LoadAllSamplesAsync(int sessionId, CancellationToken cancellationToken)
	{
		const int batchSize = 1000;
		var samples = new List<TelemetrySampleDto>(batchSize * 4);
		var startRow = 0;
		while (true)
		{
			var endRow = startRow + batchSize - 1;
			var batch = await _sessionClient.GetSessionSamplesAsync(sessionId, startRow, endRow, cancellationToken);
			if (batch.Count == 0)
			{
				break;
			}

			samples.AddRange(batch);
			var loadedCount = samples.Count;
			await Dispatcher.UIThread.InvokeAsync(() =>
			{
				PreloadStatusMessage = $"Preloading... {loadedCount} samples";
			});
			if (batch.Count < batchSize)
			{
				break;
			}

			startRow += batchSize;
		}
		return samples;
	}

	private void StopReplay()
	{
		_telemetryCts?.Cancel();
		_telemetryCts?.Dispose();
		_telemetryCts = null;
		IsReplayPaused = false;
		_logger.LogDebug("Replay stopped.");
	}

	private void ToggleReplay()
	{
		IsReplayPlaying = !IsReplayPlaying;
		ReplayStatusMessage = IsReplayPlaying ? "Replay running" : "Replay paused";
		_logger.LogDebug("Replay toggled. Playing={IsPlaying}", IsReplayPlaying);
	}

	private void PlayReplay()
	{
		IsReplayPlaying = true;
		ReplayStatusMessage = "Replay running";
		if (_telemetryCts == null)
		{
			StartReplay();
		}
	}

	private void PauseReplay()
	{
		IsReplayPlaying = false;
		IsReplayPaused = true;
		ReplayStatusMessage = "Replay paused";
	}

	private void StopReplayPlayback()
	{
		IsReplayPlaying = false;
		IsReplayPaused = false;
		StopReplay();
		_playbackIndex = _playbackStartIndex;
		SetReplayProgress(_playbackIndex);
		ReplayStatusMessage = "Replay stopped";
	}

	private void SeekBy(int delta)
	{
		if (_preloadedSamples.Count == 0)
		{
			return;
		}

		var target = Math.Clamp(_playbackIndex + delta, _playbackStartIndex, _playbackEndIndex);
		_playbackIndex = target;
		SetReplayProgress(_playbackIndex);
		UpdateTelemetry(_preloadedSamples[_playbackIndex]);
		ReplayStatusMessage = $"Seeked to sample {_playbackIndex}";
	}

	private void SetPlaybackRate(double rate)
	{
		if (rate == 0)
		{
			return;
		}

		_playbackRate = rate;
		ReplaySpeed = rate;
		ReplayStatusMessage = $"Speed set to {rate}x";
	}

	private void SetReplayProgress(int index)
	{
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => SetReplayProgress(index));
			return;
		}

		ReplaySampleIndex = index;
		ReplayTotalSamples = Math.Max(ReplayTotalSamples, _preloadedSamples.Count);
		var denominator = Math.Max(1, ReplayTotalSamples - 1);
		ReplayProgressPercent = Math.Clamp((double)index / denominator * 100.0, 0.0, 100.0);
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
		lock (_telemetryUpdateLock)
		{
			_pendingTelemetry = telemetry;
		}

		if (Interlocked.CompareExchange(ref _telemetryUpdateScheduled, 1, 0) != 0)
		{
			return;
		}

		Dispatcher.UIThread.Post(ProcessPendingTelemetry);
	}

	private void ProcessPendingTelemetry()
	{
		if (_telemetryCts?.IsCancellationRequested == true)
		{
			Interlocked.Exchange(ref _telemetryUpdateScheduled, 0);
			return;
		}

		var now = Stopwatch.GetTimestamp();
		var last = Interlocked.Read(ref _lastUiUpdateTicks);
		var elapsedSeconds = (now - last) / (double)Stopwatch.Frequency;
		if (elapsedSeconds < UiUpdateIntervalSeconds)
		{
			var delayMs = (int)Math.Ceiling((UiUpdateIntervalSeconds - elapsedSeconds) * 1000);
			_ = Task.Delay(delayMs).ContinueWith(_ => Dispatcher.UIThread.Post(ProcessPendingTelemetry));
			return;
		}

		TelemetrySampleDto? sample;
		lock (_telemetryUpdateLock)
		{
			sample = _pendingTelemetry;
			_pendingTelemetry = null;
		}

		if (sample != null)
		{
			ApplyTelemetry(sample);
			Interlocked.Exchange(ref _lastUiUpdateTicks, now);
		}

		Interlocked.Exchange(ref _telemetryUpdateScheduled, 0);

		lock (_telemetryUpdateLock)
		{
			if (_pendingTelemetry != null && Interlocked.CompareExchange(ref _telemetryUpdateScheduled, 1, 0) == 0)
			{
				Dispatcher.UIThread.Post(ProcessPendingTelemetry);
			}
		}
	}

	private void ApplyTelemetry(TelemetrySampleDto telemetry)
	{
		// Add to buffer (skip during preloaded replay to avoid duplicates)
		if (!_usePreloadedReplay)
		{
			_telemetryBuffer.Add(telemetry);
		}
		LastSampleReceived = $"Last sample: {DateTime.Now:HH:mm:ss}";

		// NOTE: Keep UI updates light to avoid blocking the UI thread under fast replays.

		// Update domain ViewModels
		Dashboard.UpdateTelemetry(telemetry);
		CurrentLapDisplay = telemetry.LapNumber > 0 ? $"{telemetry.LapNumber}/--" : "--/--";

		var trackKey = SelectedSession?.TrackId
			?? SessionMetadataTrackId
			?? SelectedSession?.Track
			?? SessionMetadataTrack;
		var frame = _trackMapService.Update(telemetry, trackKey);
		TrackMap.UpdateFrame(frame);
		if (frame.SegmentStatus != null)
		{
			Dashboard.UpdateTrackContext(frame.SegmentStatus);
			TelemetryAnalysis.UpdateTrackContext(frame.SegmentStatus);
		}
		
		var lapChanged = telemetry.LapNumber != _lastLapNumber;
		if (lapChanged)
		{
			_lastLapNumber = telemetry.LapNumber;
		}

		// Update telemetry analysis available laps periodically and on lap changes.
		if (lapChanged || _telemetryBuffer.Count % 10 == 0)
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

	public void UpdateHoverSample(TelemetrySampleDto? telemetry)
	{
		if (telemetry == null)
		{
			return;
		}

		var trackKey = SelectedSession?.TrackId
			?? SessionMetadataTrackId
			?? SelectedSession?.Track
			?? SessionMetadataTrack;
		var frame = _trackMapService.Update(telemetry, trackKey);
		TrackMap.UpdateFrame(frame);
		if (frame.SegmentStatus != null)
		{
			TelemetryAnalysis.UpdateTrackContext(frame.SegmentStatus);
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
		SessionMetadataTrackId = value.TrackId ?? string.Empty;
		SessionMetadataCar = value.Car;
		var trackKey = value.TrackId ?? value.Track;
		_trackMapService.Reset(trackKey);
		UpdateCarSpec(value.Car);
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

		return session.Track.Contains(trackFilter, StringComparison.OrdinalIgnoreCase)
			|| (!string.IsNullOrWhiteSpace(session.TrackId)
				&& session.TrackId.Contains(trackFilter, StringComparison.OrdinalIgnoreCase));
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
				TrackId = string.IsNullOrWhiteSpace(SessionMetadataTrackId) ? null : SessionMetadataTrackId,
				Car = SessionMetadataCar
			};

			var summary = await _sessionClient.UpdateSessionMetadataAsync(SelectedSession.SessionId, update, CancellationToken.None);
			if (summary != null)
			{
				UpdateSessionSummary(summary);
				ReplayStatusMessage = $"Saved metadata for session {summary.SessionId}.";
			}

			UpdateCarSpec(SessionMetadataCar);
		}
		catch (Exception ex)
		{
			ReplayStatusMessage = $"Failed to save session metadata: {ex.Message}";
			_logger.LogError(ex, "Failed to save session metadata.");
		}
	}

	private void UpdateCarSpec(string? carName)
	{
		var spec = _carSpecStore.GetByName(carName);
		Dashboard.UpdateCarSpec(spec, carName);
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
			IsReplayPaused = false;
			if (_telemetryCts == null)
			{
				StartReplay();
			}
			return;
		}

		IsReplayPaused = true;
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

		public Task<IReadOnlyList<TelemetrySampleDto>> GetSessionSamplesAsync(int sessionId, int startRow, int endRow, CancellationToken cancellationToken)
		{
			return Task.FromResult<IReadOnlyList<TelemetrySampleDto>>(Array.Empty<TelemetrySampleDto>());
		}
	}
}
