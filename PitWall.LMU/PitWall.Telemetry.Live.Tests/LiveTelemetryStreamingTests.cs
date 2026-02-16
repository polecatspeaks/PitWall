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
    /// TDD tests for LiveTelemetryReader.StreamAsync() — IAsyncEnumerable streaming with
    /// bounded Channel, configurable read interval, and health monitoring.
    /// Written FIRST per TDD RED phase.
    /// </summary>
    public class LiveTelemetryStreamingTests
    {
        private readonly Mock<ITelemetryDataSource> _mockSource;

        public LiveTelemetryStreamingTests()
        {
            _mockSource = new Mock<ITelemetryDataSource>();
        }

        #region StreamAsync — basic streaming

        [Fact]
        public async Task StreamAsync_YieldsSnapshotsFromSource()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var reader = new LiveTelemetryReader(_mockSource.Object, readIntervalMs: 1);
            var received = new List<TelemetrySnapshot>();

            // Act
            await foreach (var snapshot in reader.StreamAsync(CancellationToken.None))
            {
                received.Add(snapshot);
                if (received.Count >= 5) break;
            }

            // Assert
            Assert.Equal(5, received.Count);
            Assert.All(received, s => Assert.NotNull(s));
        }

        [Fact]
        public async Task StreamAsync_CancellationToken_StopsStream()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var reader = new LiveTelemetryReader(_mockSource.Object, readIntervalMs: 1);
            using var cts = new CancellationTokenSource();
            var received = new List<TelemetrySnapshot>();

            // Act
            try
            {
                await foreach (var snapshot in reader.StreamAsync(cts.Token))
                {
                    received.Add(snapshot);
                    if (received.Count >= 3) cts.Cancel();
                }
            }
            catch (OperationCanceledException) { /* expected */ }

            // Assert — should have stopped
            Assert.True(received.Count >= 3);
            Assert.True(received.Count < 100);
        }

        [Fact]
        public async Task StreamAsync_SourceUnavailable_SkipsWithoutError()
        {
            // Arrange — alternate available/unavailable
            int callCount = 0;
            _mockSource.Setup(s => s.IsAvailable()).Returns(() =>
                Interlocked.Increment(ref callCount) % 2 == 1);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var reader = new LiveTelemetryReader(_mockSource.Object, readIntervalMs: 1);
            var received = new List<TelemetrySnapshot>();

            // Act
            await foreach (var snapshot in reader.StreamAsync(CancellationToken.None))
            {
                received.Add(snapshot);
                if (received.Count >= 5) break;
            }

            // Assert — still yields snapshots despite intermittent unavailability
            Assert.Equal(5, received.Count);
        }

        [Fact]
        public async Task StreamAsync_SourceThrows_ContinuesStreaming()
        {
            // Arrange — throw on every 3rd call
            int callCount = 0;
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(() =>
            {
                var c = Interlocked.Increment(ref callCount);
                if (c % 3 == 0) throw new InvalidOperationException("Simulated failure");
                return CreateSnapshot();
            });

            var reader = new LiveTelemetryReader(_mockSource.Object, readIntervalMs: 1);
            var received = new List<TelemetrySnapshot>();

            // Act
            await foreach (var snapshot in reader.StreamAsync(CancellationToken.None))
            {
                received.Add(snapshot);
                if (received.Count >= 5) break;
            }

            // Assert — got 5 items despite errors
            Assert.Equal(5, received.Count);
        }

        #endregion

        #region StreamAsync — configurable interval

        [Fact]
        public async Task StreamAsync_RespectsConfiguredInterval()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            int intervalMs = 50;
            var reader = new LiveTelemetryReader(_mockSource.Object, readIntervalMs: intervalMs);

            // Act
            var sw = Stopwatch.StartNew();
            int count = 0;
            await foreach (var snapshot in reader.StreamAsync(CancellationToken.None))
            {
                count++;
                if (count >= 5) break;
            }
            sw.Stop();

            // Assert — 5 reads at 50ms interval should take >= ~150ms
            // (first read is near-immediate, then 4 intervals of 50ms)
            Assert.True(sw.ElapsedMilliseconds >= 100,
                $"Expected at least 100ms for 5 reads at {intervalMs}ms interval, got {sw.ElapsedMilliseconds}ms");
        }

        #endregion

        #region StreamAsync — bounded channel

        [Fact]
        public async Task StreamAsync_BoundedChannel_QueueDepthNeverExceedsCapacity()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            int channelCapacity = 5;
            var reader = new LiveTelemetryReader(_mockSource.Object,
                readIntervalMs: 1, channelCapacity: channelCapacity);
            var received = new List<TelemetrySnapshot>();
            int maxQueueDepth = 0;

            // Act — consume slowly to let producer fill the channel
            await foreach (var snapshot in reader.StreamAsync(CancellationToken.None))
            {
                await Task.Delay(10); // slow consumer
                received.Add(snapshot);
                var depth = reader.HealthMetrics.QueueDepth;
                if (depth > maxQueueDepth) maxQueueDepth = depth;
                if (received.Count >= 10) break;
            }

            // Assert — queue depth never exceeds capacity
            Assert.True(maxQueueDepth <= channelCapacity,
                $"Max queue depth {maxQueueDepth} exceeded capacity {channelCapacity}");
        }

        #endregion

        #region Health metrics

        [Fact]
        public async Task HealthMetrics_TracksSuccessfulReads()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var reader = new LiveTelemetryReader(_mockSource.Object, readIntervalMs: 1);

            // Act
            int count = 0;
            await foreach (var snapshot in reader.StreamAsync(CancellationToken.None))
            {
                count++;
                if (count >= 10) break;
            }

            // Assert
            Assert.True(reader.HealthMetrics.SuccessfulReads >= 10);
            Assert.Equal(0, reader.HealthMetrics.FailedReads);
        }

        [Fact]
        public async Task HealthMetrics_TracksFailureReads()
        {
            // Arrange — every 3rd call throws
            int callCount = 0;
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(() =>
            {
                var c = Interlocked.Increment(ref callCount);
                if (c % 3 == 0) throw new InvalidOperationException("test error");
                return CreateSnapshot();
            });

            var reader = new LiveTelemetryReader(_mockSource.Object, readIntervalMs: 1);
            int received = 0;

            // Act
            await foreach (var snapshot in reader.StreamAsync(CancellationToken.None))
            {
                received++;
                if (received >= 6) break;
            }

            // Assert — at least 2 failures (calls 3 and 6 if sequential)
            Assert.True(reader.HealthMetrics.FailedReads >= 2,
                $"Expected >= 2 failures, got {reader.HealthMetrics.FailedReads}");
            Assert.True(reader.HealthMetrics.SuccessfulReads >= 6);
        }

        [Fact]
        public async Task HealthMetrics_ReadsPerSecond_IsPositive()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var reader = new LiveTelemetryReader(_mockSource.Object, readIntervalMs: 1);

            // Act
            int count = 0;
            await foreach (var snapshot in reader.StreamAsync(CancellationToken.None))
            {
                count++;
                if (count >= 20) break;
            }

            // Assert
            Assert.True(reader.HealthMetrics.ReadsPerSecond > 0,
                $"Expected positive reads/sec, got {reader.HealthMetrics.ReadsPerSecond}");
        }

        [Fact]
        public async Task HealthMetrics_QueueDepth_IsNonNegative()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var reader = new LiveTelemetryReader(_mockSource.Object,
                readIntervalMs: 1, channelCapacity: 10);

            // Act
            await foreach (var snapshot in reader.StreamAsync(CancellationToken.None))
            {
                Assert.True(reader.HealthMetrics.QueueDepth >= 0);
                break;
            }
        }

        #endregion

        #region Graceful shutdown

        [Fact]
        public async Task StreamAsync_GracefulShutdown_CompletesQuickly()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var reader = new LiveTelemetryReader(_mockSource.Object, readIntervalMs: 1);

            // Act — break immediately
            var sw = Stopwatch.StartNew();
            await foreach (var snapshot in reader.StreamAsync(CancellationToken.None))
            {
                break;
            }
            sw.Stop();

            // Assert — shutdown should complete < 2 seconds (no orphaned tasks)
            Assert.True(sw.ElapsedMilliseconds < 2000,
                $"Shutdown took {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task StreamAsync_MultipleCallsSequentially_DoNotInterfere()
        {
            // Arrange
            _mockSource.Setup(s => s.IsAvailable()).Returns(true);
            _mockSource.Setup(s => s.ReadSnapshotAsync()).ReturnsAsync(CreateSnapshot);

            var reader = new LiveTelemetryReader(_mockSource.Object, readIntervalMs: 1);

            // Act — first stream
            int count1 = 0;
            await foreach (var snapshot in reader.StreamAsync(CancellationToken.None))
            {
                count1++;
                if (count1 >= 3) break;
            }

            // Second stream
            int count2 = 0;
            await foreach (var snapshot in reader.StreamAsync(CancellationToken.None))
            {
                count2++;
                if (count2 >= 3) break;
            }

            // Assert
            Assert.Equal(3, count1);
            Assert.Equal(3, count2);
        }

        #endregion

        #region Helpers

        private TelemetrySnapshot CreateSnapshot()
        {
            return new TelemetrySnapshot
            {
                Timestamp = DateTime.UtcNow,
                SessionId = "stream-test",
                Session = new SessionInfo { TrackName = "TestTrack" },
                PlayerVehicle = new VehicleTelemetry { VehicleId = 0, IsPlayer = true },
                AllVehicles = new List<VehicleTelemetry>
                {
                    new VehicleTelemetry { VehicleId = 0, IsPlayer = true }
                },
                Scoring = new ScoringInfo { NumVehicles = 1, SectorFlags = new int[3] }
            };
        }

        #endregion
    }
}
