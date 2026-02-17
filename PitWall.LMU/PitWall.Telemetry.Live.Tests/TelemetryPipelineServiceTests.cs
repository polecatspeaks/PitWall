using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using PitWall.Telemetry.Live.Models;
using PitWall.Telemetry.Live.Services;
using Xunit;

namespace PitWall.Telemetry.Live.Tests
{
    /// <summary>
    /// TDD tests for TelemetryPipelineService — orchestrates live read → optional write → broadcast.
    /// Written FIRST per TDD RED phase.
    /// </summary>
    public class TelemetryPipelineServiceTests
    {
        private readonly Mock<ITelemetryDataSource> _mockSource;
        private readonly Mock<ITelemetryWriter> _mockWriter;

        public TelemetryPipelineServiceTests()
        {
            _mockSource = new Mock<ITelemetryDataSource>();
            _mockWriter = new Mock<ITelemetryWriter>();
            _mockWriter.Setup(w => w.WriteSampleAsync(It.IsAny<TelemetrySnapshot>()))
                .Returns(Task.CompletedTask);
            _mockWriter.Setup(w => w.WriteSessionAsync(It.IsAny<TelemetrySnapshot>()))
                .Returns(Task.CompletedTask);
        }

        #region Constructor & initial state

        [Fact]
        public void Constructor_WithNullSource_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TelemetryPipelineService(null!, _mockWriter.Object));
        }

        [Fact]
        public void Constructor_WithNullWriter_DoesNotThrow()
        {
            // Writer is optional — pipeline can run without persistence
            var service = new TelemetryPipelineService(_mockSource.Object, writer: null);
            Assert.NotNull(service);
        }

        [Fact]
        public void InitialMode_IsIdle()
        {
            var service = CreateService();
            Assert.Equal(TelemetryMode.Idle, service.CurrentMode);
        }

        [Fact]
        public void InitialLatestSnapshot_IsNull()
        {
            var service = CreateService();
            Assert.Null(service.LatestSnapshot);
        }

        #endregion

        #region StreamForBroadcastAsync — basic streaming

        [Fact]
        public async Task StreamForBroadcast_WhenSourceAvailable_YieldsSnapshots()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var service = CreateService(readIntervalMs: 1, broadcastIntervalMs: 1);
            var received = new List<TelemetrySnapshot>();

            // Act
            await foreach (var snapshot in service.StreamForBroadcastAsync(CancellationToken.None))
            {
                received.Add(snapshot);
                if (received.Count >= 5) break;
            }

            // Assert
            Assert.Equal(5, received.Count);
        }

        [Fact]
        public async Task StreamForBroadcast_SetsCurrentModeToLive()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var service = CreateService(readIntervalMs: 1, broadcastIntervalMs: 1);

            // Act
            await foreach (var snapshot in service.StreamForBroadcastAsync(CancellationToken.None))
            {
                // Assert — mode should be Live while streaming
                Assert.Equal(TelemetryMode.Live, service.CurrentMode);
                break;
            }
        }

        [Fact]
        public async Task StreamForBroadcast_UpdatesLatestSnapshot()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var service = CreateService(readIntervalMs: 1, broadcastIntervalMs: 1);

            // Act
            await foreach (var snapshot in service.StreamForBroadcastAsync(CancellationToken.None))
            {
                // Assert — latest snapshot should be set
                Assert.NotNull(service.LatestSnapshot);
                break;
            }
        }

        [Fact]
        public async Task StreamForBroadcast_CancellationStopsCleanly()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var service = CreateService(readIntervalMs: 1, broadcastIntervalMs: 1);
            using var cts = new CancellationTokenSource();
            var received = new List<TelemetrySnapshot>();

            // Act
            try
            {
                await foreach (var snapshot in service.StreamForBroadcastAsync(cts.Token))
                {
                    received.Add(snapshot);
                    if (received.Count >= 3) cts.Cancel();
                }
            }
            catch (OperationCanceledException) { }

            // Assert
            Assert.True(received.Count >= 3);
        }

        #endregion

        #region StreamForBroadcastAsync — throttling

        [Fact]
        public async Task StreamForBroadcast_ThrottlesToBroadcastInterval()
        {
            // Arrange — 100ms broadcast interval
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var service = CreateService(readIntervalMs: 1, broadcastIntervalMs: 100);

            // Act
            var sw = Stopwatch.StartNew();
            int count = 0;
            await foreach (var snapshot in service.StreamForBroadcastAsync(CancellationToken.None))
            {
                count++;
                if (count >= 5) break;
            }
            sw.Stop();

            // Assert — 5 broadcasts at 100ms interval should take >= 300ms
            Assert.True(sw.ElapsedMilliseconds >= 250,
                $"Expected >= 250ms for 5 broadcasts at 100ms interval, got {sw.ElapsedMilliseconds}ms");
        }

        #endregion

        #region StreamForBroadcastAsync — writer integration

        [Fact]
        public async Task StreamForBroadcast_WritesSessionToWriter()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var service = CreateService(readIntervalMs: 1, broadcastIntervalMs: 1);

            // Act
            await foreach (var snapshot in service.StreamForBroadcastAsync(CancellationToken.None))
            {
                break;
            }

            // Assert — session should have been written
            _mockWriter.Verify(w => w.WriteSessionAsync(It.IsAny<TelemetrySnapshot>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task StreamForBroadcast_WritesSamplesToWriter()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var service = CreateService(readIntervalMs: 1, broadcastIntervalMs: 1);
            int count = 0;

            // Act
            await foreach (var snapshot in service.StreamForBroadcastAsync(CancellationToken.None))
            {
                count++;
                if (count >= 5) break;
            }

            // Assert — samples should have been written to writer
            _mockWriter.Verify(w => w.WriteSampleAsync(It.IsAny<TelemetrySnapshot>()),
                Times.AtLeast(5));
        }

        [Fact]
        public async Task StreamForBroadcast_WithoutWriter_StillStreams()
        {
            // Arrange — no writer
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var service = new TelemetryPipelineService(_mockSource.Object,
                writer: null, readIntervalMs: 1, broadcastIntervalMs: 1);
            var received = new List<TelemetrySnapshot>();

            // Act
            await foreach (var snapshot in service.StreamForBroadcastAsync(CancellationToken.None))
            {
                received.Add(snapshot);
                if (received.Count >= 5) break;
            }

            // Assert — should work without writer
            Assert.Equal(5, received.Count);
        }

        #endregion

        #region Mode transitions

        [Fact]
        public async Task StreamForBroadcast_WhenSourceBecomesUnavailable_WaitsForRecovery()
        {
            // Arrange — source available for 3 reads, then unavailable for 5 checks, then available again
            long checkCount = 0;
            _mockSource.Setup(s => s.IsAvailable()).Returns(() =>
            {
                var c = Interlocked.Increment(ref checkCount);
                return c <= 3 || c > 8; // Available for first 3 checks and after 8th check
            });
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(() =>
            {
                return CreateSnapshot();
            });

            var service = CreateService(readIntervalMs: 1, broadcastIntervalMs: 1);
            var received = new List<TelemetrySnapshot>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Act — collect 5 snapshots (should get 3 before gap, then recover)
            await foreach (var snapshot in service.StreamForBroadcastAsync(cts.Token))
            {
                received.Add(snapshot);
                if (received.Count >= 5) break;
            }

            // Assert
            Assert.Equal(5, received.Count);
        }

        [Fact]
        public async Task StreamForBroadcast_AfterStreamEnds_ModeReturnsToIdle()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var service = CreateService(readIntervalMs: 1, broadcastIntervalMs: 1);
            using var cts = new CancellationTokenSource();

            // Act
            try
            {
                await foreach (var snapshot in service.StreamForBroadcastAsync(cts.Token))
                {
                    Assert.Equal(TelemetryMode.Live, service.CurrentMode);
                    cts.Cancel();
                }
            }
            catch (OperationCanceledException) { }

            // Assert — after cancellation, mode should return to Idle
            Assert.Equal(TelemetryMode.Idle, service.CurrentMode);
        }

        #endregion

        #region Health metrics passthrough

        [Fact]
        public async Task HealthMetrics_AreAccessibleFromPipeline()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var service = CreateService(readIntervalMs: 1, broadcastIntervalMs: 1);

            // Act
            int count = 0;
            await foreach (var snapshot in service.StreamForBroadcastAsync(CancellationToken.None))
            {
                count++;
                if (count >= 10) break;
            }

            // Assert
            Assert.NotNull(service.HealthMetrics);
            Assert.True(service.HealthMetrics.SuccessfulReads >= 10);
        }

        #endregion

        #region Helpers

        private TelemetryPipelineService CreateService(
            int readIntervalMs = 10,
            int broadcastIntervalMs = 100)
        {
            return new TelemetryPipelineService(
                _mockSource.Object,
                _mockWriter.Object,
                readIntervalMs: readIntervalMs,
                broadcastIntervalMs: broadcastIntervalMs);
        }

        private TelemetrySnapshot CreateSnapshot()
        {
            return new TelemetrySnapshot
            {
                Timestamp = DateTime.UtcNow,
                SessionId = "pipeline-test",
                Session = new SessionInfo
                {
                    TrackName = "TestTrack",
                    SessionType = "Practice",
                    NumVehicles = 1,
                    TrackLength = 4000.0
                },
                PlayerVehicle = new VehicleTelemetry { VehicleId = 0, IsPlayer = true, Speed = 200 },
                AllVehicles = new List<VehicleTelemetry>
                {
                    new VehicleTelemetry { VehicleId = 0, IsPlayer = true, Speed = 200 }
                },
                Scoring = new ScoringInfo { NumVehicles = 1, SectorFlags = new int[3] }
            };
        }

        #endregion
    }
}
