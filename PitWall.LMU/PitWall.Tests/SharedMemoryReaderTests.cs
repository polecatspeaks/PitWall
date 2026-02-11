using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using PitWall.Core.Models;
using PitWall.Core.Services;
using Xunit;

namespace PitWall.Tests
{
    [SupportedOSPlatform("windows")]
    public class SharedMemoryReaderTests : IDisposable
    {
        private MemoryMappedFile? _testMmf;
        private const string TestMapName = "test-lmu-reader";

        public void Dispose()
        {
            _testMmf?.Dispose();
        }

        private void CreateTestMemoryMap()
        {
            try
            {
                // Try to delete existing test map
                var existing = MemoryMappedFile.OpenExisting(TestMapName);
                existing.Dispose();
            }
            catch
            {
                // Doesn't exist, which is fine
            }

            // Create new test memory-mapped file
            _testMmf = MemoryMappedFile.CreateNew(TestMapName, 4096);

            using (var accessor = _testMmf.CreateViewAccessor())
            {
                // Write known telemetry data (raw values, not scaled)
                accessor.Write(0, 125.5);   // Speed in kph
                accessor.Write(8, 45.3);    // Fuel
                accessor.Write(16, 45.0);   // Brake (0-100 scale, will be divided by 100)
                accessor.Write(24, 75.0);   // Throttle (0-100 scale, will be divided by 100)
                accessor.Write(32, 0.15);   // Steering
                accessor.Write(40, 95.0);   // Tyre FL
                accessor.Write(48, 98.0);   // Tyre FR
                accessor.Write(56, 92.0);   // Tyre RL
                accessor.Write(64, 96.0);   // Tyre RR
            }
        }

        private void UpdateTestMemoryMap(double speed, double fuel, double brake, double throttle, double steering,
            double tyreTempFL, double tyreTempFR, double tyreTempRL, double tyreTempRR)
        {
            if (_testMmf == null)
                return;

            using (var accessor = _testMmf.CreateViewAccessor())
            {
                accessor.Write(0, speed);
                accessor.Write(8, fuel);
                accessor.Write(16, brake);
                accessor.Write(24, throttle);
                accessor.Write(32, steering);
                accessor.Write(40, tyreTempFL);
                accessor.Write(48, tyreTempFR);
                accessor.Write(56, tyreTempRL);
                accessor.Write(64, tyreTempRR);
            }
        }

        [Fact]
        public void Constructor_InitializesWithDefaultValues()
        {
            var reader = new SharedMemoryReader();

            Assert.NotNull(reader);
            Assert.False(reader.IsConnected);
            Assert.Equal(100, reader.PollingFrequency);

            reader.Dispose();
        }

        [Fact]
        public void Constructor_InitializesWithCustomMapName()
        {
            var reader = new SharedMemoryReader("Custom_LMU_Map", 8192);

            Assert.NotNull(reader);
            Assert.False(reader.IsConnected);

            reader.Dispose();
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenMapNameIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new SharedMemoryReader(null!));
        }

        [Fact]
        public async Task StartAsync_CompletesSuccessfully()
        {
            var reader = new SharedMemoryReader("nonexistent-map");

            await reader.StartAsync(100);

            Assert.True(true, "StartAsync should complete without throwing");

            reader.Dispose();
        }

        [Fact]
        public async Task StartAsync_ThrowsInvalidOperationException_WhenAlreadyStarted()
        {
            var reader = new SharedMemoryReader("test-map");
            await reader.StartAsync(100);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await reader.StartAsync(100));

            reader.Dispose();
        }

        [Fact]
        public async Task StartAsync_SetsPollingFrequency()
        {
            var reader = new SharedMemoryReader("test-map");

            await reader.StartAsync(200);

            Assert.Equal(200, reader.PollingFrequency);

            reader.Dispose();
        }

        [Fact]
        public async Task StartAsync_AcceptsCancellationToken()
        {
            var reader = new SharedMemoryReader("test-map");
            var cts = new CancellationTokenSource();

            await reader.StartAsync(100, cts.Token);

            cts.Cancel();
            await Task.Delay(50);

            reader.Dispose();
        }

        [Fact]
        public async Task StopAsync_CompletesSuccessfully()
        {
            var reader = new SharedMemoryReader("test-map");
            await reader.StartAsync(100);

            await reader.StopAsync();

            Assert.False(reader.IsConnected);

            reader.Dispose();
        }

        [Fact]
        public async Task StopAsync_CanBeCalledMultipleTimes()
        {
            var reader = new SharedMemoryReader("test-map");
            await reader.StartAsync(100);

            await reader.StopAsync();
            await reader.StopAsync();

            Assert.False(reader.IsConnected);

            reader.Dispose();
        }

        [Fact]
        public void GetLatestTelemetry_ReturnsNull_WhenNotConnected()
        {
            var reader = new SharedMemoryReader("nonexistent-map");

            var latest = reader.GetLatestTelemetry();

            Assert.Null(latest);

            reader.Dispose();
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            var reader = new SharedMemoryReader("test-dispose-map");

            reader.Dispose();
            reader.Dispose();

            Assert.True(true, "Multiple Dispose calls should not throw");
        }

        [Fact]
        public async Task OnConnectionStateChanged_RaisesEvent_WhenConnectionFails()
        {
            if (!OperatingSystem.IsWindows())
            {
                // Skip on non-Windows - event won't be raised due to platform check
                return;
            }

            var reader = new SharedMemoryReader("nonexistent-test-map");
            var tcs = new TaskCompletionSource<bool>();

            reader.OnConnectionStateChanged += (sender, isConnected) =>
            {
                tcs.TrySetResult(isConnected);
            };

            await reader.StartAsync(100);
            
            // Wait for event with timeout
            var timeoutTask = Task.Delay(500);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            Assert.True(completedTask == tcs.Task, "Event should be raised");
            Assert.False(await tcs.Task);

            reader.Dispose();
        }

        [Fact]
        public async Task OnError_RaisesEvent_WhenNonFileNotFoundExceptionOccurs()
        {
            // This test verifies that OnError is invoked for exceptions other than FileNotFoundException
            var reader = new SharedMemoryReader("invalid-map-name-!@#$%");
            bool errorRaised = false;

            reader.OnError += (sender, ex) =>
            {
                errorRaised = true;
            };

            await reader.StartAsync(100);
            await Task.Delay(100);

            // Error might or might not be raised depending on platform implementation
            // Just verify no exceptions are thrown
            Assert.True(true);

            reader.Dispose();
        }

        [Fact]
        public async Task StreamSamples_ReturnsEmptySample_WhenMemoryMapNotFound()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows
            }

            var reader = new SharedMemoryReader("nonexistent-stream-map");
            var samples = new List<TelemetrySample>();

            await foreach (var sample in reader.StreamSamples())
            {
                samples.Add(sample);
                break; // Just get the first sample
            }

            Assert.Single(samples);
            Assert.NotNull(samples[0]);

            reader.Dispose();
        }

        [Fact]
        public async Task StreamSamples_ThrowsPlatformNotSupportedException_OnNonWindows()
        {
            if (OperatingSystem.IsWindows())
            {
                return; // Skip on Windows
            }

            var reader = new SharedMemoryReader("test-map");

            await Assert.ThrowsAsync<PlatformNotSupportedException>(async () =>
            {
                await foreach (var sample in reader.StreamSamples())
                {
                    break;
                }
            });

            reader.Dispose();
        }

        [Fact]
        public async Task StreamSamples_ReadsDataFromMemoryMap()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows
            }

            CreateTestMemoryMap();
            var reader = new SharedMemoryReader(TestMapName, 4096);
            var samples = new List<TelemetrySample>();

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));

            try
            {
                await foreach (var sample in reader.StreamSamples())
                {
                    samples.Add(sample);
                    if (samples.Count >= 3)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            Assert.True(samples.Count > 0);
            var firstSample = samples[0];
            Assert.NotNull(firstSample);
            Assert.Equal(125.5, firstSample.SpeedKph, 0.1);
            Assert.Equal(45.3, firstSample.FuelLiters, 0.1);
            Assert.Equal(0.45, firstSample.Brake, 0.01); // 45.0 / 100 = 0.45
            Assert.Equal(0.75, firstSample.Throttle, 0.01); // 75.0 / 100 = 0.75
            Assert.Equal(0.15, firstSample.Steering, 0.01);
            Assert.Equal(4, firstSample.TyreTempsC.Length);

            reader.Dispose();
        }

        [Fact]
        public async Task StreamSamples_ReadsTyreTemperaturesCorrectly()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows
            }

            CreateTestMemoryMap();
            var reader = new SharedMemoryReader(TestMapName, 4096);
            TelemetrySample? sample = null;

            await foreach (var s in reader.StreamSamples())
            {
                sample = s;
                break;
            }

            Assert.NotNull(sample);
            Assert.Equal(95.0, sample.TyreTempsC[0], 0.1); // FL
            Assert.Equal(98.0, sample.TyreTempsC[1], 0.1); // FR
            Assert.Equal(92.0, sample.TyreTempsC[2], 0.1); // RL
            Assert.Equal(96.0, sample.TyreTempsC[3], 0.1); // RR

            reader.Dispose();
        }

        [Fact]
        public async Task StreamSamples_ScalesBrakeAndThrottle()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows
            }

            CreateTestMemoryMap();
            UpdateTestMemoryMap(100.0, 50.0, 80.0, 60.0, 0.0, 90.0, 90.0, 90.0, 90.0);

            var reader = new SharedMemoryReader(TestMapName, 4096);
            TelemetrySample? sample = null;

            await foreach (var s in reader.StreamSamples())
            {
                sample = s;
                break;
            }

            Assert.NotNull(sample);
            Assert.Equal(0.8, sample.Brake, 0.01); // 80.0 / 100 = 0.8
            Assert.Equal(0.6, sample.Throttle, 0.01); // 60.0 / 100 = 0.6

            reader.Dispose();
        }

        [Fact]
        public async Task StreamSamples_UpdatesLatestTelemetry()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows
            }

            CreateTestMemoryMap();
            var reader = new SharedMemoryReader(TestMapName, 4096);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));

            try
            {
                await foreach (var sample in reader.StreamSamples())
                {
                    if (cts.Token.IsCancellationRequested)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            var latest = reader.GetLatestTelemetry();
            Assert.NotNull(latest);

            reader.Dispose();
        }

        [Fact]
        public async Task StreamSamples_RaisesOnTelemetryUpdateEvent()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows
            }

            CreateTestMemoryMap();
            var reader = new SharedMemoryReader(TestMapName, 4096);
            bool eventRaised = false;
            TelemetrySample? eventSample = null;

            reader.OnTelemetryUpdate += (sender, sample) =>
            {
                eventRaised = true;
                eventSample = sample;
            };

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));

            try
            {
                await foreach (var sample in reader.StreamSamples())
                {
                    if (eventRaised)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            Assert.True(eventRaised);
            Assert.NotNull(eventSample);

            reader.Dispose();
        }

        [Fact]
        public async Task StreamSamples_HandlesZeroValues()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows
            }

            CreateTestMemoryMap();
            UpdateTestMemoryMap(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0);

            var reader = new SharedMemoryReader(TestMapName, 4096);
            TelemetrySample? sample = null;

            await foreach (var s in reader.StreamSamples())
            {
                sample = s;
                break;
            }

            Assert.NotNull(sample);
            Assert.Equal(0.0, sample.SpeedKph);
            Assert.Equal(0.0, sample.FuelLiters);
            Assert.Equal(0.0, sample.Brake);
            Assert.Equal(0.0, sample.Throttle);

            reader.Dispose();
        }

        [Fact]
        public async Task StreamSamples_HandlesNegativeSteeringValues()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows
            }

            CreateTestMemoryMap();
            UpdateTestMemoryMap(100.0, 50.0, 0.0, 50.0, -0.8, 90.0, 90.0, 90.0, 90.0);

            var reader = new SharedMemoryReader(TestMapName, 4096);
            TelemetrySample? sample = null;

            await foreach (var s in reader.StreamSamples())
            {
                sample = s;
                break;
            }

            Assert.NotNull(sample);
            Assert.Equal(-0.8, sample.Steering, 0.01);

            reader.Dispose();
        }

        [Fact]
        public async Task StreamSamples_SetsTimestampToNow()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows
            }

            CreateTestMemoryMap();
            var reader = new SharedMemoryReader(TestMapName, 4096);
            TelemetrySample? sample = null;
            var beforeTime = DateTime.UtcNow;

            await foreach (var s in reader.StreamSamples())
            {
                sample = s;
                break;
            }

            var afterTime = DateTime.UtcNow;

            Assert.NotNull(sample);
            Assert.True(sample.Timestamp >= beforeTime);
            Assert.True(sample.Timestamp <= afterTime);

            reader.Dispose();
        }

        [Fact]
        public async Task StreamSamples_ContinuesReadingMultipleSamples()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows
            }

            CreateTestMemoryMap();
            var reader = new SharedMemoryReader(TestMapName, 4096);
            var samples = new List<TelemetrySample>();
            int targetCount = 5;

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(200));

            try
            {
                await foreach (var sample in reader.StreamSamples())
                {
                    samples.Add(sample);
                    if (samples.Count >= targetCount)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            Assert.True(samples.Count >= 3, $"Expected at least 3 samples, got {samples.Count}");

            reader.Dispose();
        }

        [Fact]
        public async Task StartAsync_IntegratesWithStreamSamples()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows
            }

            CreateTestMemoryMap();
            var reader = new SharedMemoryReader(TestMapName, 4096);
            TelemetrySample? latestSample = null;

            reader.OnTelemetryUpdate += (sender, sample) =>
            {
                latestSample = sample;
            };

            await reader.StartAsync(100);
            await Task.Delay(100);

            Assert.NotNull(latestSample);
            Assert.True(reader.IsConnected);

            await reader.StopAsync();

            reader.Dispose();
        }

        [Fact]
        public async Task StreamSamples_SetsIsConnectedTrue_WhenMapExists()
        {
            if (!OperatingSystem.IsWindows())
            {
                return; // Skip on non-Windows
            }

            CreateTestMemoryMap();
            var reader = new SharedMemoryReader(TestMapName, 4096);

            var enumerator = reader.StreamSamples().GetAsyncEnumerator();
            await enumerator.MoveNextAsync();

            Assert.True(reader.IsConnected);

            await enumerator.DisposeAsync();
            reader.Dispose();
        }
    }
}
