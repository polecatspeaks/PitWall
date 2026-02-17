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
    /// <summary>
    /// Additional tests for MainWindowViewModel to increase coverage of
    /// session filtering, replay controls, and recommendation handling.
    /// </summary>
    public class MainWindowViewModelAdditionalTests
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
            return mock;
        }

        private static MainWindowViewModel CreateViewModel(
            Mock<ISessionClient>? sessionClient = null)
        {
            return new MainWindowViewModel(
                CreateMockRecommendationClient().Object,
                CreateMockTelemetryStreamClient().Object,
                CreateMockAgentQueryClient().Object,
                CreateMockAgentConfigClient().Object,
                sessionClient?.Object ?? CreateMockSessionClient().Object);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Parameterless_Constructor_InitializesAllChildViewModels()
        {
            var vm = new MainWindowViewModel();

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
            var vm = CreateViewModel();

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
        public void Constructor_DefaultPropertyValues()
        {
            var vm = CreateViewModel();

            Assert.Equal("--/--", vm.CurrentLapDisplay);
            Assert.Equal("P--", vm.PositionDisplay);
            Assert.Equal("--", vm.GapDisplay);
            Assert.True(vm.ReplayEnabled);
            Assert.True(vm.IsReplayPlaying);
            Assert.Equal(100, vm.ReplayIntervalMs);
            Assert.Equal(1.0, vm.ReplaySpeed);
            Assert.NotNull(vm.AvailableSessions);
            Assert.NotNull(vm.FilteredSessions);
        }

        #endregion

        #region RequestPitNow Tests

        [Fact]
        public void RequestPitNow_SetsDashboardAndStrategy()
        {
            var vm = CreateViewModel();

            vm.RequestPitNow();

            Assert.Equal("Pit this lap", vm.Dashboard.StrategyMessage);
            Assert.Equal(1.0, vm.Dashboard.StrategyConfidence);
            Assert.Equal("Pit this lap", vm.Strategy.RecommendedAction);
            Assert.Equal(1.0, vm.Strategy.StrategyConfidence);
        }

        [Fact]
        public void RequestPitNow_AddsAlert()
        {
            var vm = CreateViewModel();

            vm.RequestPitNow();

            Assert.Contains(vm.Dashboard.Alerts, a => a.Contains("Pit request"));
        }

        #endregion

        #region TriggerEmergencyMode Tests

        [Fact]
        public void TriggerEmergencyMode_SetsEmergencyState()
        {
            var vm = CreateViewModel();

            vm.TriggerEmergencyMode();

            Assert.Equal("Emergency: pit now", vm.Strategy.RecommendedAction);
            Assert.Equal(1.0, vm.Strategy.StrategyConfidence);
            Assert.Equal("Emergency: pit now", vm.Dashboard.StrategyMessage);
        }

        [Fact]
        public void TriggerEmergencyMode_AddsAlert()
        {
            var vm = CreateViewModel();

            vm.TriggerEmergencyMode();

            Assert.Contains(vm.Dashboard.Alerts, a => a.Contains("Emergency"));
        }

        #endregion

        #region DismissAlerts Tests

        [Fact]
        public void DismissAlerts_ClearsAllAlerts()
        {
            var vm = CreateViewModel();
            vm.RequestPitNow();
            vm.TriggerEmergencyMode();
            Assert.NotEmpty(vm.Dashboard.Alerts);

            vm.DismissAlerts();

            Assert.Empty(vm.Dashboard.Alerts);
        }

        #endregion

        #region ApplyRecommendation Tests

        [Fact]
        public void ApplyRecommendation_UpdatesDashboardAndStrategy()
        {
            var vm = CreateViewModel();
            var recommendation = new RecommendationDto
            {
                Recommendation = "Stay out. Fuel delta +3 laps.",
                Confidence = 0.87,
                SessionId = "1"
            };

            vm.ApplyRecommendation(recommendation);

            Assert.Equal("Stay out. Fuel delta +3 laps.", vm.Dashboard.StrategyMessage);
            Assert.Equal("Stay out. Fuel delta +3 laps.", vm.Strategy.RecommendedAction);
            Assert.Equal(0.87, vm.Strategy.StrategyConfidence);
        }

        [Fact]
        public void ApplyRecommendation_ResetsHealthyFlag_AllowsNewFailure()
        {
            var vm = CreateViewModel();
            vm.ApplyRecommendationFailure("First fail");
            vm.ApplyRecommendation(new RecommendationDto { Recommendation = "OK" });

            vm.ApplyRecommendationFailure("Second fail");

            Assert.Equal("Second fail", vm.Strategy.RecommendedAction);
        }

        #endregion

        #region Stop Tests

        [Fact]
        public void Stop_DoesNotThrow_WhenCalledMultipleTimes()
        {
            var vm = CreateViewModel();

            vm.Stop();
            vm.Stop();

            // No exception thrown
        }

        #endregion

        #region PauseTelemetry Tests

        [Fact]
        public void PauseTelemetry_ExecutesPauseCommand()
        {
            var vm = CreateViewModel();
            vm.IsReplayPlaying = true;

            vm.PauseTelemetry();

            Assert.False(vm.IsReplayPlaying);
        }

        #endregion

        #region ReplayIntervals Tests

        [Fact]
        public void ReplayIntervalsMs_ContainsExpectedValues()
        {
            var vm = CreateViewModel();

            Assert.Contains(0, vm.ReplayIntervalsMs);
            Assert.Contains(100, vm.ReplayIntervalsMs);
            Assert.Contains(1000, vm.ReplayIntervalsMs);
        }

        #endregion

        #region UpdateHoverSample Tests

        [Fact]
        public void UpdateHoverSample_NullSample_DoesNotThrow()
        {
            var vm = CreateViewModel();

            vm.UpdateHoverSample(null);

            // No exception
        }

        #endregion

        #region AddAlert Deduplication Tests

        [Fact]
        public void RequestPitNow_Twice_DoesNotDuplicateAlerts()
        {
            var vm = CreateViewModel();

            vm.RequestPitNow();
            vm.RequestPitNow();

            Assert.Single(vm.Dashboard.Alerts, a => a.Contains("Pit request"));
        }

        #endregion
    }
}
