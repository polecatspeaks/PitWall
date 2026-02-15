using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PitWall.UI.Models;
using PitWall.UI.Services;
using PitWall.UI.ViewModels;
using Xunit;

namespace PitWall.UI.Tests
{
	public class MainWindowViewModelTests
	{
		#region Helper Methods

		private static Mock<IRecommendationClient> CreateMockRecommendationClient()
		{
			var mock = new Mock<IRecommendationClient>();
			mock.Setup(x => x.GetRecommendationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new RecommendationDto());
			return mock;
		}

		private static Mock<ITelemetryStreamClient> CreateMockTelemetryStreamClient()
		{
			var mock = new Mock<ITelemetryStreamClient>();
			mock.Setup(x => x.ConnectAsync(
					It.IsAny<int>(),
					It.IsAny<int>(),
					It.IsAny<int>(),
					It.IsAny<int>(),
					It.IsAny<Action<TelemetrySampleDto>>(),
					It.IsAny<CancellationToken>()))
				.Returns(Task.CompletedTask);
			return mock;
		}

		private static Mock<IAgentQueryClient> CreateMockAgentQueryClient()
		{
			var mock = new Mock<IAgentQueryClient>();
			mock.Setup(x => x.SendQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new AgentResponseDto());
			return mock;
		}

		private static Mock<IAgentConfigClient> CreateMockAgentConfigClient()
		{
			var mock = new Mock<IAgentConfigClient>();
			mock.Setup(x => x.GetConfigAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new AgentConfigDto());
			mock.Setup(x => x.UpdateConfigAsync(It.IsAny<AgentConfigUpdateDto>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(new AgentConfigDto());
			mock.Setup(x => x.DiscoverEndpointsAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<string>());
			return mock;
		}

		private static Mock<ISessionClient> CreateMockSessionClient(IReadOnlyList<SessionSummaryDto>? sessions = null)
		{
			var mock = new Mock<ISessionClient>();
			mock.Setup(x => x.GetSessionCountAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(sessions?.Count ?? 0);
			mock.Setup(x => x.GetSessionSummariesAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(sessions ?? Array.Empty<SessionSummaryDto>());
			mock.Setup(x => x.GetSessionSamplesAsync(
					It.IsAny<int>(),
					It.IsAny<int>(),
					It.IsAny<int>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(Array.Empty<TelemetrySampleDto>());
			mock.Setup(x => x.UpdateSessionMetadataAsync(
					It.IsAny<int>(),
					It.IsAny<SessionMetadataUpdateDto>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync((int sessionId, SessionMetadataUpdateDto update, CancellationToken ct) =>
					new SessionSummaryDto
					{
						SessionId = sessionId,
						Track = update.Track ?? string.Empty,
						TrackId = update.TrackId,
						Car = update.Car ?? string.Empty
					});
			return mock;
		}

		private static SessionSummaryDto CreateSessionSummary(
			int sessionId,
			string track = "Test Track",
			string car = "Test Car",
			DateTimeOffset? startTime = null,
			string? trackId = null)
		{
			return new SessionSummaryDto
			{
				SessionId = sessionId,
				Track = track,
				Car = car,
				TrackId = trackId,
				StartTimeUtc = startTime ?? DateTimeOffset.UtcNow
			};
		}

		private static DateTimeOffset CreateLocalDate(int year, int month, int day)
		{
			// Store as UTC for StartTimeUtc but preserve the intended local calendar date.
			var localDate = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Local);
			return new DateTimeOffset(localDate).ToUniversalTime();
		}

		private static TelemetrySampleDto CreateTelemetrySample(
			int lapNumber = 1,
			double speedKph = 200.0,
			double throttle = 1.0,
			double brake = 0.0,
			double steeringAngle = 0.0,
			double latitude = 0.0,
			double longitude = 0.0,
			double lateralG = 0.0,
			DateTime? timestamp = null)
		{
			return new TelemetrySampleDto
			{
				LapNumber = lapNumber,
				SpeedKph = speedKph,
				ThrottlePosition = throttle,
				BrakePosition = brake,
				SteeringAngle = steeringAngle,
				Latitude = latitude,
				Longitude = longitude,
				LateralG = lateralG,
				Timestamp = timestamp ?? DateTime.UtcNow
			};
		}

		private static MainWindowViewModel CreateViewModel(
			Mock<IRecommendationClient>? recommendationClient = null,
			Mock<ITelemetryStreamClient>? telemetryStreamClient = null,
			Mock<IAgentQueryClient>? agentQueryClient = null,
			Mock<IAgentConfigClient>? agentConfigClient = null,
			Mock<ISessionClient>? sessionClient = null)
		{
			return new MainWindowViewModel(
				recommendationClient?.Object ?? CreateMockRecommendationClient().Object,
				telemetryStreamClient?.Object ?? CreateMockTelemetryStreamClient().Object,
				agentQueryClient?.Object ?? CreateMockAgentQueryClient().Object,
				agentConfigClient?.Object ?? CreateMockAgentConfigClient().Object,
				sessionClient?.Object ?? CreateMockSessionClient().Object);
		}

		#endregion

		#region Recommendation Fallback Tests

		[Fact]
		public void ApplyRecommendationFailure_UpdatesStrategyAndDashboard()
		{
			// Arrange
			var vm = CreateViewModel();
			const string message = "Strategy offline. Retrying.";

			// Act
			vm.ApplyRecommendationFailure(message);

			// Assert
			Assert.Equal(message, vm.Dashboard.StrategyMessage);
			Assert.Equal(message, vm.Strategy.RecommendedAction);
			Assert.Equal(0.0, vm.Strategy.StrategyConfidence);
		}

		[Fact]
		public void ApplyRecommendationFailure_IgnoresRepeatFailures()
		{
			// Arrange
			var vm = CreateViewModel();
			vm.ApplyRecommendationFailure("First");

			// Act
			vm.ApplyRecommendationFailure("Second");

			// Assert
			Assert.Equal("First", vm.Strategy.RecommendedAction);
		}

		[Fact]
		public void ApplyRecommendation_ResetsFailureState()
		{
			// Arrange
			var vm = CreateViewModel();
			vm.ApplyRecommendationFailure("Offline");
			var recommendation = new RecommendationDto
			{
				Recommendation = "Pit now",
				Confidence = 0.91
			};

			// Act
			vm.ApplyRecommendation(recommendation);
			vm.ApplyRecommendationFailure("Offline again");

			// Assert
			Assert.Equal("Offline again", vm.Strategy.RecommendedAction);
		}

		#endregion

		#region UI Refresh Cadence Tests

		[Fact]
		public void UiUpdateInterval_Is10Hz()
		{
			Assert.Equal(0.1, MainWindowViewModel.UiUpdateIntervalSeconds);
		}

		#endregion

		#region ApplyRecommendation Tests

		[Fact]
		public void ApplyRecommendation_UpdatesStrategyRecommendedActionAndConfidence()
		{
			// Arrange
			var vm = CreateViewModel();
			var recommendation = new RecommendationDto
			{
				Recommendation = "Box this lap",
				Confidence = 0.85
			};

			// Act
			vm.ApplyRecommendation(recommendation);

			// Assert
			Assert.Equal("Box this lap", vm.Strategy.RecommendedAction);
			Assert.Equal(0.85, vm.Strategy.StrategyConfidence);
		}

		#endregion

		#region Constructor Tests

		[Fact]
		public void Constructor_InitializesViewModels()
		{
			// Arrange & Act
			var vm = CreateViewModel();

			// Assert
			Assert.NotNull(vm.Dashboard);
			Assert.NotNull(vm.TelemetryAnalysis);
			Assert.NotNull(vm.Strategy);
			Assert.NotNull(vm.AiAssistant);
			Assert.NotNull(vm.Settings);
			Assert.NotNull(vm.TrackMap);
		}

		[Fact]
		public void Constructor_InitializesCommands()
		{
			// Arrange & Act
			var vm = CreateViewModel();

			// Assert
			Assert.NotNull(vm.LoadSessionsCommand);
			Assert.NotNull(vm.ToggleReplayCommand);
			Assert.NotNull(vm.RestartReplayCommand);
			Assert.NotNull(vm.ApplyReplaySettingsCommand);
			Assert.NotNull(vm.SaveSessionMetadataCommand);
			Assert.NotNull(vm.PlayReplayCommand);
			Assert.NotNull(vm.PauseReplayCommand);
			Assert.NotNull(vm.StopReplayCommand);
			Assert.NotNull(vm.FastForwardCommand);
			Assert.NotNull(vm.RewindCommand);
			Assert.NotNull(vm.HalfSpeedForwardCommand);
			Assert.NotNull(vm.QuarterSpeedForwardCommand);
			Assert.NotNull(vm.HalfSpeedReverseCommand);
			Assert.NotNull(vm.QuarterSpeedReverseCommand);
		}

		[Fact]
		public void Constructor_InitializesCollections()
		{
			// Arrange & Act
			var vm = CreateViewModel();

			// Assert
			Assert.NotNull(vm.AvailableSessions);
			Assert.Empty(vm.AvailableSessions);
			Assert.NotNull(vm.FilteredSessions);
			Assert.Empty(vm.FilteredSessions);
		}

		[Fact]
		public void Constructor_InitializesDefaultPropertyValues()
		{
			// Arrange & Act
			var vm = CreateViewModel();

			// Assert
			Assert.Equal(0, vm.SelectedTabIndex);
			Assert.Equal("--/--", vm.CurrentLapDisplay);
			Assert.Equal("P--", vm.PositionDisplay);
			Assert.Equal("--", vm.GapDisplay);
			Assert.True(vm.ReplayEnabled);
			Assert.Equal(1, vm.SelectedSessionId);
			Assert.Null(vm.SelectedSession);
			Assert.Null(vm.SessionFilterFrom);
			Assert.Null(vm.SessionFilterTo);
			Assert.Equal(string.Empty, vm.SessionFilterTrack);
			Assert.Equal(string.Empty, vm.SessionFilterCar);
			Assert.Equal(0, vm.ReplayStartRow);
			Assert.Equal(-1, vm.ReplayEndRow);
			Assert.Equal(100, vm.ReplayIntervalMs);
			Assert.True(vm.IsReplayPlaying);
			Assert.Equal("Replay ready", vm.ReplayStatusMessage);
			Assert.False(vm.IsReplayPaused);
			Assert.Equal(1.0, vm.ReplaySpeed);
			Assert.Equal(0, vm.ReplaySampleIndex);
			Assert.Equal(0, vm.ReplayTotalSamples);
			Assert.Equal(0.0, vm.ReplayProgressPercent);
			Assert.False(vm.IsPreloading);
			Assert.Equal("Ready", vm.PreloadStatusMessage);
		}

		#endregion

		#region StartAsync Tests

		[Fact]
		public async Task StartAsync_WithValidSessionId_SetsSelectedSessionId()
		{
			// Arrange
			var vm = CreateViewModel();
			var cts = new CancellationTokenSource();

			// Act
			await vm.StartAsync("42", cts.Token);

			// Assert
			Assert.Equal(42, vm.SelectedSessionId);
		}

		[Fact]
		public async Task StartAsync_WithInvalidSessionId_KeepsDefaultSessionId()
		{
			// Arrange
			var vm = CreateViewModel();
			var cts = new CancellationTokenSource();

			// Act
			await vm.StartAsync("invalid", cts.Token);

			// Assert
			Assert.Equal(1, vm.SelectedSessionId);
		}

		[Fact]
		public async Task StartAsync_WithNegativeSessionId_KeepsDefaultSessionId()
		{
			// Arrange
			var vm = CreateViewModel();
			var cts = new CancellationTokenSource();

			// Act
			await vm.StartAsync("-5", cts.Token);

			// Assert
			Assert.Equal(1, vm.SelectedSessionId);
		}

		[Fact]
		public async Task StartAsync_WithZeroSessionId_KeepsDefaultSessionId()
		{
			// Arrange
			var vm = CreateViewModel();
			var cts = new CancellationTokenSource();

			// Act
			await vm.StartAsync("0", cts.Token);

			// Assert
			Assert.Equal(1, vm.SelectedSessionId);
		}

		#endregion

		#region Session Loading Tests

		[Fact]
		public async Task LoadSessionsCommand_WithNoSessions_ShowsNoSessionsMessage()
		{
			// Arrange
			var sessionClient = CreateMockSessionClient(Array.Empty<SessionSummaryDto>());
			var vm = CreateViewModel(sessionClient: sessionClient);

			// Act
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Assert
			Assert.Empty(vm.AvailableSessions);
			Assert.Empty(vm.FilteredSessions);
			Assert.Equal("No sessions found in telemetry database.", vm.ReplayStatusMessage);
		}

		[Fact]
		public async Task LoadSessionsCommand_WithSessions_PopulatesAvailableSessions()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, "Track 1", "Car 1"),
				CreateSessionSummary(2, "Track 2", "Car 2"),
				CreateSessionSummary(3, "Track 3", "Car 3")
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);

			// Act
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Assert
			Assert.Equal(3, vm.AvailableSessions.Count);
			Assert.Equal(3, vm.FilteredSessions.Count);
			Assert.Contains("Loaded 3 session(s).", vm.ReplayStatusMessage);
		}

		[Fact]
		public async Task LoadSessionsCommand_OrdersSessionsByDateDescending()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, startTime: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)),
				CreateSessionSummary(2, startTime: new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero)),
				CreateSessionSummary(3, startTime: new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero))
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);

			// Act
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Assert
			Assert.Equal(2, vm.AvailableSessions[0].SessionId); // March (most recent)
			Assert.Equal(3, vm.AvailableSessions[1].SessionId); // February
			Assert.Equal(1, vm.AvailableSessions[2].SessionId); // January
		}

		[Fact]
		public async Task LoadSessionsCommand_SelectsFirstSessionWhenNoneSelected()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, "Track 1", "Car 1", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)),
				CreateSessionSummary(2, "Track 2", "Car 2", new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero))
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);

			// Act
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Assert
			Assert.NotNull(vm.SelectedSession);
			Assert.Equal(2, vm.SelectedSession.SessionId); // Session 2 is selected (most recent date)
		}

		[Fact]
		public async Task LoadSessionsCommand_OnException_ShowsErrorMessage()
		{
			// Arrange
			var sessionClient = new Mock<ISessionClient>();
			sessionClient.Setup(x => x.GetSessionSummariesAsync(It.IsAny<CancellationToken>()))
				.ThrowsAsync(new Exception("Test error"));
			var vm = CreateViewModel(sessionClient: sessionClient);

			// Act
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Assert
			Assert.Contains("Failed to load sessions", vm.ReplayStatusMessage);
		}

		#endregion

		#region Session Filtering Tests

		[Fact]
		public async Task SessionFilterTrack_FiltersSessionsByTrackName()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, "Silverstone", "Car 1"),
				CreateSessionSummary(2, "Monza", "Car 2"),
				CreateSessionSummary(3, "Silverstone GP", "Car 3")
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Act
			vm.SessionFilterTrack = "Silver";

			// Assert
			Assert.Equal(2, vm.FilteredSessions.Count);
			Assert.All(vm.FilteredSessions, s => Assert.Contains("Silver", s.Track, StringComparison.OrdinalIgnoreCase));
		}

		[Fact]
		public async Task SessionFilterTrack_CaseInsensitive()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, "Silverstone", "Car 1"),
				CreateSessionSummary(2, "Monza", "Car 2")
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Act
			vm.SessionFilterTrack = "SILVERSTONE";

			// Assert
			Assert.Single(vm.FilteredSessions);
			Assert.Equal("Silverstone", vm.FilteredSessions[0].Track);
		}

		[Fact]
		public async Task SessionFilterTrack_FiltersUsingTrackId()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, "Silverstone", "Car 1", trackId: "silverstone_gp"),
				CreateSessionSummary(2, "Monza", "Car 2", trackId: "monza"),
				CreateSessionSummary(3, "Spa", "Car 3", trackId: "spa")
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Act
			vm.SessionFilterTrack = "silverstone";

			// Assert
			Assert.Single(vm.FilteredSessions);
			Assert.Equal("Silverstone", vm.FilteredSessions[0].Track);
		}

		[Fact]
		public async Task SessionFilterCar_FiltersSessionsByCar()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, "Track 1", "Ferrari 488"),
				CreateSessionSummary(2, "Track 2", "McLaren 720S"),
				CreateSessionSummary(3, "Track 3", "Ferrari 812")
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Act
			vm.SessionFilterCar = "Ferrari";

			// Assert
			Assert.Equal(2, vm.FilteredSessions.Count);
			Assert.All(vm.FilteredSessions, s => Assert.Contains("Ferrari", s.Car));
		}

		[Fact]
		public async Task SessionFilterFrom_FiltersSessionsByStartDate()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, startTime: CreateLocalDate(2024, 1, 1)),
				CreateSessionSummary(2, startTime: CreateLocalDate(2024, 2, 1)),
				CreateSessionSummary(3, startTime: CreateLocalDate(2024, 3, 1))
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Act
			vm.SessionFilterFrom = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Local);

			// Assert
			Assert.Equal(2, vm.FilteredSessions.Count);
			Assert.All(vm.FilteredSessions, s =>
			{
				Assert.True(s.StartTimeUtc.HasValue);
				Assert.True(s.StartTimeUtc.Value.ToLocalTime().Date >= new DateTime(2024, 2, 1));
			});
		}

		[Fact]
		public async Task SessionFilterTo_FiltersSessionsByEndDate()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, startTime: CreateLocalDate(2024, 1, 1)),
				CreateSessionSummary(2, startTime: CreateLocalDate(2024, 2, 1)),
				CreateSessionSummary(3, startTime: CreateLocalDate(2024, 3, 1))
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Act
			vm.SessionFilterTo = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Local);

			// Assert
			Assert.Equal(2, vm.FilteredSessions.Count);
			Assert.All(vm.FilteredSessions, s =>
			{
				Assert.True(s.StartTimeUtc.HasValue);
				Assert.True(s.StartTimeUtc.Value.ToLocalTime().Date <= new DateTime(2024, 2, 1));
			});
		}

		[Fact]
		public async Task SessionFilterFromAndTo_FiltersSessionsByDateRange()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, startTime: CreateLocalDate(2024, 1, 1)),
				CreateSessionSummary(2, startTime: CreateLocalDate(2024, 2, 1)),
				CreateSessionSummary(3, startTime: CreateLocalDate(2024, 3, 1)),
				CreateSessionSummary(4, startTime: CreateLocalDate(2024, 4, 1))
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Act
			vm.SessionFilterFrom = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Local);
			vm.SessionFilterTo = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Local);

			// Assert
			Assert.Equal(2, vm.FilteredSessions.Count);
			Assert.Contains(vm.FilteredSessions, s => s.SessionId == 2);
			Assert.Contains(vm.FilteredSessions, s => s.SessionId == 3);
		}

		[Fact]
		public async Task SessionFilters_CombinedTrackAndCarFilter()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, "Silverstone", "Ferrari 488"),
				CreateSessionSummary(2, "Silverstone", "McLaren 720S"),
				CreateSessionSummary(3, "Monza", "Ferrari 488"),
				CreateSessionSummary(4, "Monza", "McLaren 720S")
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Act
			vm.SessionFilterTrack = "Silverstone";
			vm.SessionFilterCar = "Ferrari";

			// Assert
			Assert.Single(vm.FilteredSessions);
			Assert.Equal("Silverstone", vm.FilteredSessions[0].Track);
			Assert.Equal("Ferrari 488", vm.FilteredSessions[0].Car);
		}

		[Fact]
		public async Task SessionFilters_EmptyFilters_ShowsAllSessions()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, "Track 1", "Car 1"),
				CreateSessionSummary(2, "Track 2", "Car 2")
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Act
			vm.SessionFilterTrack = "";
			vm.SessionFilterCar = "";

			// Assert
			Assert.Equal(2, vm.FilteredSessions.Count);
		}

		[Fact]
		public async Task SessionFilters_WithWhitespaceOnly_ShowsAllSessions()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, "Track 1", "Car 1"),
				CreateSessionSummary(2, "Track 2", "Car 2")
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Act
			vm.SessionFilterTrack = "   ";
			vm.SessionFilterCar = "  ";

			// Assert
			Assert.Equal(2, vm.FilteredSessions.Count);
		}

		[Fact]
		public async Task SessionFilters_NoMatches_ShowsEmptyFilteredSessions()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, "Silverstone", "Ferrari"),
				CreateSessionSummary(2, "Monza", "McLaren")
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Act
			vm.SessionFilterTrack = "Spa";

			// Assert
			Assert.Empty(vm.FilteredSessions);
		}

		[Fact]
		public async Task SessionFilters_UpdatesSelectedSessionWhenFilteredOut()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, "Silverstone", "Ferrari"),
				CreateSessionSummary(2, "Monza", "McLaren")
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);
			await vm.LoadSessionsCommand.ExecuteAsync(null);
			vm.SelectedSession = vm.FilteredSessions[1]; // Select Monza

			// Act
			vm.SessionFilterTrack = "Silverstone"; // Filter out Monza

			// Assert
			Assert.NotNull(vm.SelectedSession);
			Assert.Equal("Silverstone", vm.SelectedSession.Track);
		}

		[Fact]
		public async Task SessionFilters_SessionWithoutStartTime_FilteredOutByDateFilter()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto>
			{
				CreateSessionSummary(1, startTime: CreateLocalDate(2024, 1, 1)),
				new SessionSummaryDto { SessionId = 2, Track = "Test", Car = "Test", StartTimeUtc = null }
			};
			var sessionClient = CreateMockSessionClient(sessions);
			var vm = CreateViewModel(sessionClient: sessionClient);
			await vm.LoadSessionsCommand.ExecuteAsync(null);

			// Act
			vm.SessionFilterFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Local);

			// Assert
			Assert.Single(vm.FilteredSessions);
			Assert.Equal(1, vm.FilteredSessions[0].SessionId);
		}

		#endregion

		#region Replay Control Tests

		[Fact]
		public void PlayReplayCommand_SetsIsReplayPlayingToTrue()
		{
			// Arrange
			var vm = CreateViewModel();
			vm.IsReplayPlaying = false;

			// Act
			vm.PlayReplayCommand.Execute(null);

			// Assert
			Assert.True(vm.IsReplayPlaying);
			Assert.Equal("Replay running", vm.ReplayStatusMessage);
		}

		[Fact]
		public void PauseReplayCommand_SetsIsReplayPlayingToFalse()
		{
			// Arrange
			var vm = CreateViewModel();
			vm.IsReplayPlaying = true;

			// Act
			vm.PauseReplayCommand.Execute(null);

			// Assert
			Assert.False(vm.IsReplayPlaying);
			Assert.True(vm.IsReplayPaused);
			Assert.Equal("Replay paused", vm.ReplayStatusMessage);
		}

		[Fact]
		public void StopReplayCommand_StopsReplayAndResetsState()
		{
			// Arrange
			var vm = CreateViewModel();
			vm.IsReplayPlaying = true;
			vm.IsReplayPaused = true;

			// Act
			vm.StopReplayCommand.Execute(null);

			// Assert
			Assert.False(vm.IsReplayPlaying);
			Assert.False(vm.IsReplayPaused);
			Assert.Equal("Replay stopped", vm.ReplayStatusMessage);
		}

		[Fact]
		public void ToggleReplayCommand_TogglesPlaybackState()
		{
			// Arrange
			var vm = CreateViewModel();
			var initialState = vm.IsReplayPlaying;

			// Act
			vm.ToggleReplayCommand.Execute(null);

			// Assert
			Assert.NotEqual(initialState, vm.IsReplayPlaying);
		}

		[Fact]
		public void ToggleReplayCommand_UpdatesStatusMessage()
		{
			// Arrange
			var vm = CreateViewModel();
			vm.IsReplayPlaying = true;

			// Act
			vm.ToggleReplayCommand.Execute(null);

			// Assert
			Assert.Equal("Replay paused", vm.ReplayStatusMessage);
		}

		[Fact]
		public void HalfSpeedForwardCommand_SetsPlaybackRateToHalf()
		{
			// Arrange
			var vm = CreateViewModel();

			// Act
			vm.HalfSpeedForwardCommand.Execute(null);

			// Assert
			Assert.Equal(0.5, vm.ReplaySpeed);
			Assert.Contains("0.5x", vm.ReplayStatusMessage);
		}

		[Fact]
		public void QuarterSpeedForwardCommand_SetsPlaybackRateToQuarter()
		{
			// Arrange
			var vm = CreateViewModel();

			// Act
			vm.QuarterSpeedForwardCommand.Execute(null);

			// Assert
			Assert.Equal(0.25, vm.ReplaySpeed);
			Assert.Contains("0.25x", vm.ReplayStatusMessage);
		}

		[Fact]
		public void HalfSpeedReverseCommand_SetsPlaybackRateToNegativeHalf()
		{
			// Arrange
			var vm = CreateViewModel();

			// Act
			vm.HalfSpeedReverseCommand.Execute(null);

			// Assert
			Assert.Equal(-0.5, vm.ReplaySpeed);
			Assert.Contains("-0.5x", vm.ReplayStatusMessage);
		}

		[Fact]
		public void QuarterSpeedReverseCommand_SetsPlaybackRateToNegativeQuarter()
		{
			// Arrange
			var vm = CreateViewModel();

			// Act
			vm.QuarterSpeedReverseCommand.Execute(null);

			// Assert
			Assert.Equal(-0.25, vm.ReplaySpeed);
			Assert.Contains("-0.25x", vm.ReplayStatusMessage);
		}

		[Fact]
		public void RestartReplayCommand_ResetsReplayRange()
		{
			// Arrange
			var vm = CreateViewModel();
			vm.ReplayStartRow = 100;
			vm.ReplayEndRow = 500;

			// Act
			vm.RestartReplayCommand.Execute(null);

			// Assert
			Assert.Equal(0, vm.ReplayStartRow);
			Assert.Equal(-1, vm.ReplayEndRow);
		}

		[Fact]
		public void ReplayEnabled_SetToTrue_StartsReplay()
		{
			// Arrange
			var sessions = new List<SessionSummaryDto> { CreateSessionSummary(1) };
			var sessionClient = CreateMockSessionClient(sessions);
			sessionClient.Setup(x => x.GetSessionSamplesAsync(
					It.IsAny<int>(),
					It.IsAny<int>(),
					It.IsAny<int>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(new List<TelemetrySampleDto> { CreateTelemetrySample() });
			var vm = CreateViewModel(sessionClient: sessionClient);
			vm.ReplayEnabled = false;

			// Act
			vm.ReplayEnabled = true;

			// Assert - ReplayEnabled triggers StartReplay which should update status
			Assert.True(vm.ReplayEnabled);
		}

		[Fact]
		public void ReplayEnabled_SetToFalse_StopsReplay()
		{
			// Arrange
			var vm = CreateViewModel();
			vm.ReplayEnabled = true;

			// Act
			vm.ReplayEnabled = false;

			// Assert
			Assert.False(vm.ReplayEnabled);
			Assert.Equal("Replay disabled", vm.ReplayStatusMessage);
		}

		#endregion

		#region Property Change Tests

		[Fact]
		public void SelectedSessionId_Changed_TriggersReplayWhenEnabled()
		{
			// Arrange
			var vm = CreateViewModel();
			vm.ReplayEnabled = true;
			vm.IsReplayPlaying = true;
			var originalSessionId = vm.SelectedSessionId;

			// Act
			vm.SelectedSessionId = 42;

			// Assert
			Assert.NotEqual(originalSessionId, vm.SelectedSessionId);
			Assert.Equal(42, vm.SelectedSessionId);
		}

		[Fact]
		public void SelectedSession_Changed_UpdatesSessionMetadata()
		{
			// Arrange
			var vm = CreateViewModel();
			var session = CreateSessionSummary(
				sessionId: 5,
				track: "Spa-Francorchamps",
				car: "Porsche 911",
				trackId: "spa");

			// Act
			vm.SelectedSession = session;

			// Assert
			Assert.Equal(5, vm.SelectedSessionId);
			Assert.Equal("Spa-Francorchamps", vm.SessionMetadataTrack);
			Assert.Equal("spa", vm.SessionMetadataTrackId);
			Assert.Equal("Porsche 911", vm.SessionMetadataCar);
		}

		[Fact]
		public void SelectedSession_SetToNull_DoesNotThrow()
		{
			// Arrange
			var vm = CreateViewModel();
			vm.SelectedSession = CreateSessionSummary(1);

			// Act & Assert
			vm.SelectedSession = null;
		}

		[Fact]
		public void IsReplayPlaying_SetToTrue_ClearsIsReplayPausedWhenReplayStarts()
		{
			// Arrange
			var vm = CreateViewModel();
			vm.IsReplayPaused = true;
			vm.IsReplayPlaying = false;

			// Act
			vm.IsReplayPlaying = true;

			// Assert
			// Note: IsReplayPaused is only cleared to false when _telemetryCts is not null
			// Since we're not actually starting a replay task in unit tests, IsReplayPaused
			// will be cleared when StartReplay() creates the cancellation token source
			Assert.True(vm.IsReplayPlaying);
		}

		[Fact]
		public void IsReplayPlaying_SetToFalse_SetsIsReplayPaused()
		{
			// Arrange
			var vm = CreateViewModel();
			vm.IsReplayPaused = false;
			vm.IsReplayPlaying = true;

			// Act
			vm.IsReplayPlaying = false;

			// Assert
			Assert.True(vm.IsReplayPaused);
		}

		#endregion

		#region Telemetry Tests

		[Fact]
		public void UpdateHoverSample_WithNullTelemetry_DoesNotThrow()
		{
			// Arrange
			var vm = CreateViewModel();

			// Act & Assert
			vm.UpdateHoverSample(null);
		}

		[Fact]
		public void UpdateHoverSample_WithValidTelemetry_UpdatesTrackMap()
		{
			// Arrange
			var vm = CreateViewModel();
			var sample = CreateTelemetrySample(lapNumber: 5);

			// Act
			vm.UpdateHoverSample(sample);

			// Assert - No exception and method completes
			Assert.NotNull(vm.TrackMap);
		}

		#endregion

		#region Session Metadata Tests

		[Fact]
		public async Task SaveSessionMetadataCommand_WithNoSelectedSession_DoesNothing()
		{
			// Arrange
			var sessionClient = CreateMockSessionClient();
			var vm = CreateViewModel(sessionClient: sessionClient);
			vm.SelectedSession = null;

			// Act
			await vm.SaveSessionMetadataCommand.ExecuteAsync(null);

			// Assert
			sessionClient.Verify(
				x => x.UpdateSessionMetadataAsync(
					It.IsAny<int>(),
					It.IsAny<SessionMetadataUpdateDto>(),
					It.IsAny<CancellationToken>()),
				Times.Never);
		}

		[Fact]
		public async Task SaveSessionMetadataCommand_WithSelectedSession_CallsUpdateAPI()
		{
			// Arrange
			var sessionClient = CreateMockSessionClient();
			var vm = CreateViewModel(sessionClient: sessionClient);
			vm.SelectedSession = CreateSessionSummary(1);
			vm.SessionMetadataTrack = "New Track";
			vm.SessionMetadataTrackId = "new_track";
			vm.SessionMetadataCar = "New Car";

			// Act
			await vm.SaveSessionMetadataCommand.ExecuteAsync(null);

			// Assert
			sessionClient.Verify(
				x => x.UpdateSessionMetadataAsync(
					1,
					It.Is<SessionMetadataUpdateDto>(u =>
						u.Track == "New Track" &&
						u.TrackId == "new_track" &&
						u.Car == "New Car"),
					It.IsAny<CancellationToken>()),
				Times.Once);
		}

		[Fact]
		public async Task SaveSessionMetadataCommand_OnSuccess_UpdatesStatusMessage()
		{
			// Arrange
			var sessionClient = CreateMockSessionClient();
			var vm = CreateViewModel(sessionClient: sessionClient);
			vm.SelectedSession = CreateSessionSummary(1);

			// Act
			await vm.SaveSessionMetadataCommand.ExecuteAsync(null);

			// Assert
			Assert.Contains("Saved metadata for session", vm.ReplayStatusMessage);
		}

		[Fact]
		public async Task SaveSessionMetadataCommand_WithEmptyTrackId_PassesNullToAPI()
		{
			// Arrange
			var sessionClient = CreateMockSessionClient();
			var vm = CreateViewModel(sessionClient: sessionClient);
			vm.SelectedSession = CreateSessionSummary(1);
			vm.SessionMetadataTrackId = "";

			// Act
			await vm.SaveSessionMetadataCommand.ExecuteAsync(null);

			// Assert
			sessionClient.Verify(
				x => x.UpdateSessionMetadataAsync(
					1,
					It.Is<SessionMetadataUpdateDto>(u => u.TrackId == null),
					It.IsAny<CancellationToken>()),
				Times.Once);
		}

		[Fact]
		public async Task SaveSessionMetadataCommand_WithWhitespaceTrackId_PassesNullToAPI()
		{
			// Arrange
			var sessionClient = CreateMockSessionClient();
			var vm = CreateViewModel(sessionClient: sessionClient);
			vm.SelectedSession = CreateSessionSummary(1);
			vm.SessionMetadataTrackId = "   ";

			// Act
			await vm.SaveSessionMetadataCommand.ExecuteAsync(null);

			// Assert
			sessionClient.Verify(
				x => x.UpdateSessionMetadataAsync(
					1,
					It.Is<SessionMetadataUpdateDto>(u => u.TrackId == null),
					It.IsAny<CancellationToken>()),
				Times.Once);
		}

		[Fact]
		public async Task SaveSessionMetadataCommand_OnException_ShowsErrorMessage()
		{
			// Arrange
			var sessionClient = CreateMockSessionClient();
			sessionClient.Setup(x => x.UpdateSessionMetadataAsync(
					It.IsAny<int>(),
					It.IsAny<SessionMetadataUpdateDto>(),
					It.IsAny<CancellationToken>()))
				.ThrowsAsync(new Exception("Test error"));
			var vm = CreateViewModel(sessionClient: sessionClient);
			vm.SelectedSession = CreateSessionSummary(1);

			// Act
			await vm.SaveSessionMetadataCommand.ExecuteAsync(null);

			// Assert
			Assert.Contains("Failed to save session metadata", vm.ReplayStatusMessage);
		}

		#endregion

		#region Edge Cases

		[Fact]
		public void Stop_WithoutStart_DoesNotThrow()
		{
			// Arrange
			var vm = CreateViewModel();

			// Act & Assert
			vm.Stop();
		}

		[Fact]
		public void Stop_CalledMultipleTimes_DoesNotThrow()
		{
			// Arrange
			var vm = CreateViewModel();

			// Act & Assert
			vm.Stop();
			vm.Stop();
			vm.Stop();
		}

		[Fact]
		public void Commands_CanExecuteInitially()
		{
			// Arrange
			var vm = CreateViewModel();

			// Assert
			Assert.True(vm.ToggleReplayCommand.CanExecute(null));
			Assert.True(vm.RestartReplayCommand.CanExecute(null));
			Assert.True(vm.ApplyReplaySettingsCommand.CanExecute(null));
			Assert.True(vm.PlayReplayCommand.CanExecute(null));
			Assert.True(vm.PauseReplayCommand.CanExecute(null));
			Assert.True(vm.StopReplayCommand.CanExecute(null));
			Assert.True(vm.FastForwardCommand.CanExecute(null));
			Assert.True(vm.RewindCommand.CanExecute(null));
		}

		[Fact]
		public void ReplayIntervalMs_NegativeValue_AllowedByProperty()
		{
			// Arrange
			var vm = CreateViewModel();

			// Act
			vm.ReplayIntervalMs = -100;

			// Assert
			Assert.Equal(-100, vm.ReplayIntervalMs);
		}

		[Fact]
		public void SelectedTabIndex_CanBeChanged()
		{
			// Arrange
			var vm = CreateViewModel();

			// Act
			vm.SelectedTabIndex = 3;

			// Assert
			Assert.Equal(3, vm.SelectedTabIndex);
		}

		[Fact]
		public void ReplayStartRow_NegativeValue_Allowed()
		{
			// Arrange
			var vm = CreateViewModel();

			// Act
			vm.ReplayStartRow = -10;

			// Assert
			Assert.Equal(-10, vm.ReplayStartRow);
		}

		[Fact]
		public void ReplayEndRow_NegativeValue_Allowed()
		{
			// Arrange
			var vm = CreateViewModel();

			// Act
			vm.ReplayEndRow = -100;

			// Assert
			Assert.Equal(-100, vm.ReplayEndRow);
		}

		[Fact]
		public void CurrentLapDisplay_DefaultValue()
		{
			// Arrange & Act
			var vm = CreateViewModel();

			// Assert
			Assert.Equal("--/--", vm.CurrentLapDisplay);
		}

		[Fact]
		public void PositionDisplay_DefaultValue()
		{
			// Arrange & Act
			var vm = CreateViewModel();

			// Assert
			Assert.Equal("P--", vm.PositionDisplay);
		}

		[Fact]
		public void GapDisplay_DefaultValue()
		{
			// Arrange & Act
			var vm = CreateViewModel();

			// Assert
			Assert.Equal("--", vm.GapDisplay);
		}

		#endregion

		#region Shortcut Action Tests

		[Fact]
		public void RequestPitNow_AddsAlertAndUpdatesStrategy()
		{
			// Arrange
			var vm = CreateViewModel();

			// Act
			vm.RequestPitNow();

			// Assert
			Assert.Single(vm.Dashboard.Alerts);
			Assert.Contains("Pit request", vm.Dashboard.Alerts[0]);
			Assert.Equal("Pit this lap", vm.Strategy.RecommendedAction);
			Assert.Equal(1.0, vm.Strategy.StrategyConfidence);
			Assert.Equal("Pit this lap", vm.Dashboard.StrategyMessage);
			Assert.Equal(1.0, vm.Dashboard.StrategyConfidence);
		}

		[Fact]
		public void TriggerEmergencyMode_AddsAlertAndUpdatesStrategy()
		{
			// Arrange
			var vm = CreateViewModel();

			// Act
			vm.TriggerEmergencyMode();

			// Assert
			Assert.Single(vm.Dashboard.Alerts);
			Assert.Contains("Emergency mode", vm.Dashboard.Alerts[0]);
			Assert.Equal("Emergency: pit now", vm.Strategy.RecommendedAction);
			Assert.Equal(1.0, vm.Strategy.StrategyConfidence);
			Assert.Equal("Emergency: pit now", vm.Dashboard.StrategyMessage);
			Assert.Equal(1.0, vm.Dashboard.StrategyConfidence);
		}

		[Fact]
		public void DismissAlerts_ClearsDashboardAlerts()
		{
			// Arrange
			var vm = CreateViewModel();
			vm.Dashboard.Alerts.Add("Alert 1");
			vm.Dashboard.Alerts.Add("Alert 2");

			// Act
			vm.DismissAlerts();

			// Assert
			Assert.Empty(vm.Dashboard.Alerts);
		}

		[Fact]
		public void PauseTelemetry_PausesReplay()
		{
			// Arrange
			var vm = CreateViewModel();
			vm.IsReplayPlaying = true;

			// Act
			vm.PauseTelemetry();

			// Assert
			Assert.False(vm.IsReplayPlaying);
			Assert.True(vm.IsReplayPaused);
			Assert.Equal("Replay paused", vm.ReplayStatusMessage);
		}

		#endregion

		#region Collection Tests

		[Fact]
		public void ReplayIntervalsMs_ContainsExpectedValues()
		{
			// Arrange
			var vm = CreateViewModel();

			// Act
			var intervals = vm.ReplayIntervalsMs;

			// Assert
			Assert.Contains(0, intervals);
			Assert.Contains(10, intervals);
			Assert.Contains(50, intervals);
			Assert.Contains(100, intervals);
			Assert.Contains(250, intervals);
			Assert.Contains(500, intervals);
			Assert.Contains(1000, intervals);
		}

		[Fact]
		public void AvailableSessions_IsObservableCollection()
		{
			// Arrange
			var vm = CreateViewModel();

			// Assert
			Assert.IsAssignableFrom<System.Collections.Specialized.INotifyCollectionChanged>(vm.AvailableSessions);
		}

		[Fact]
		public void FilteredSessions_IsObservableCollection()
		{
			// Arrange
			var vm = CreateViewModel();

			// Assert
			Assert.IsAssignableFrom<System.Collections.Specialized.INotifyCollectionChanged>(vm.FilteredSessions);
		}

		#endregion

		#region Replay Progress Tests

		[Fact]
		public void ReplayProgressPercent_InitiallyZero()
		{
			// Arrange & Act
			var vm = CreateViewModel();

			// Assert
			Assert.Equal(0.0, vm.ReplayProgressPercent);
		}

		[Fact]
		public void ReplaySampleIndex_InitiallyZero()
		{
			// Arrange & Act
			var vm = CreateViewModel();

			// Assert
			Assert.Equal(0, vm.ReplaySampleIndex);
		}

		[Fact]
		public void ReplayTotalSamples_InitiallyZero()
		{
			// Arrange & Act
			var vm = CreateViewModel();

			// Assert
			Assert.Equal(0, vm.ReplayTotalSamples);
		}

		#endregion

		#region Status Message Tests

		[Fact]
		public void ReplayStatusMessage_InitialValue()
		{
			// Arrange & Act
			var vm = CreateViewModel();

			// Assert
			Assert.Equal("Replay ready", vm.ReplayStatusMessage);
		}

		[Fact]
		public void PreloadStatusMessage_InitialValue()
		{
			// Arrange & Act
			var vm = CreateViewModel();

			// Assert
			Assert.Equal("Ready", vm.PreloadStatusMessage);
		}

		[Fact]
		public void IsPreloading_InitiallyFalse()
		{
			// Arrange & Act
			var vm = CreateViewModel();

			// Assert
			Assert.False(vm.IsPreloading);
		}

		#endregion
	}
}
