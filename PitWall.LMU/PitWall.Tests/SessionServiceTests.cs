using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Api.Services;
using PitWall.Core.Models;
using PitWall.Core.Services;
using Xunit;

namespace PitWall.Tests
{
    public class SessionServiceTests
    {
        private readonly TestLmuTelemetryReader _telemetryReader;
        private readonly SessionService _service;

        public SessionServiceTests()
        {
            _telemetryReader = new TestLmuTelemetryReader();
            _service = new SessionService(_telemetryReader, NullLogger<SessionService>.Instance);
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenLmuReaderIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => 
                new SessionService(null!, NullLogger<SessionService>.Instance));
        }

        [Fact]
        public async Task GetTotalSessionCountAsync_ReturnsZero_WhenNoSessions()
        {
            _telemetryReader.SetSessionCount(0);

            var count = await _service.GetTotalSessionCountAsync();

            Assert.Equal(0, count);
        }

        [Fact]
        public async Task GetTotalSessionCountAsync_ReturnsCorrectCount()
        {
            _telemetryReader.SetSessionCount(5);

            var count = await _service.GetTotalSessionCountAsync();

            Assert.Equal(5, count);
        }

        [Fact]
        public async Task GetTotalSessionCountAsync_ReturnsLargeCount()
        {
            _telemetryReader.SetSessionCount(1000);

            var count = await _service.GetTotalSessionCountAsync();

            Assert.Equal(1000, count);
        }

        [Fact]
        public async Task GetAvailableChannelsAsync_ReturnsEmptyList_WhenNoChannels()
        {
            _telemetryReader.SetChannels(new List<ChannelInfo>());

            var channels = await _service.GetAvailableChannelsAsync();

            Assert.NotNull(channels);
            Assert.Empty(channels);
        }

        [Fact]
        public async Task GetAvailableChannelsAsync_ReturnsAllChannels()
        {
            var expectedChannels = new List<ChannelInfo>
            {
                new ChannelInfo("GPS Speed", new[] { "value" }),
                new ChannelInfo("TyresTempCentre", new[] { "value1", "value2", "value3", "value4" }),
                new ChannelInfo("Throttle Pos", new[] { "value" })
            };
            _telemetryReader.SetChannels(expectedChannels);

            var channels = await _service.GetAvailableChannelsAsync();

            Assert.Equal(3, channels.Count);
            Assert.Contains(channels, c => c.Name == "GPS Speed");
            Assert.Contains(channels, c => c.Name == "TyresTempCentre");
            Assert.Contains(channels, c => c.Name == "Throttle Pos");
        }

        [Fact]
        public async Task GetAvailableChannelsAsync_ReturnsChannelsWithCorrectProperties()
        {
            var expectedChannels = new List<ChannelInfo>
            {
                new ChannelInfo("TyresTempCentre", new[] { "value1", "value2", "value3", "value4" })
            };
            _telemetryReader.SetChannels(expectedChannels);

            var channels = await _service.GetAvailableChannelsAsync();

            var channel = channels[0];
            Assert.Equal("TyresTempCentre", channel.Name);
            Assert.Equal(4, channel.ColumnCount);
            Assert.Equal(new[] { "value1", "value2", "value3", "value4" }, channel.ColumnNames);
        }

        [Fact]
        public async Task GetSessionDataAsync_ReturnsAsyncEnumerable()
        {
            var samples = new List<TelemetrySample>
            {
                CreateTestSample(DateTime.UtcNow, 100.0),
                CreateTestSample(DateTime.UtcNow.AddSeconds(1), 105.0)
            };
            _telemetryReader.SetSamples(1, samples);

            var result = await _service.GetSessionDataAsync(1);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetSessionDataAsync_ReturnsCorrectData()
        {
            var samples = new List<TelemetrySample>
            {
                CreateTestSample(DateTime.UtcNow, 100.0),
                CreateTestSample(DateTime.UtcNow.AddSeconds(1), 105.0),
                CreateTestSample(DateTime.UtcNow.AddSeconds(2), 110.0)
            };
            _telemetryReader.SetSamples(1, samples);

            var result = await _service.GetSessionDataAsync(1);
            var samplesResult = await ConsumeAsync(result);

            Assert.Equal(3, samplesResult.Count);
            Assert.Equal(100.0, samplesResult[0].SpeedKph);
            Assert.Equal(105.0, samplesResult[1].SpeedKph);
            Assert.Equal(110.0, samplesResult[2].SpeedKph);
        }

        [Fact]
        public async Task GetSessionDataAsync_WithStartRow_ReturnsDataFromStart()
        {
            var samples = new List<TelemetrySample>
            {
                CreateTestSample(DateTime.UtcNow, 100.0),
                CreateTestSample(DateTime.UtcNow.AddSeconds(1), 105.0),
                CreateTestSample(DateTime.UtcNow.AddSeconds(2), 110.0)
            };
            _telemetryReader.SetSamples(1, samples, startRow: 1);

            var result = await _service.GetSessionDataAsync(1, startRow: 1);
            var samplesResult = await ConsumeAsync(result);

            Assert.Equal(3, samplesResult.Count);
        }

        [Fact]
        public async Task GetSessionDataAsync_WithEndRow_ReturnsDataUpToEnd()
        {
            var samples = new List<TelemetrySample>
            {
                CreateTestSample(DateTime.UtcNow, 100.0),
                CreateTestSample(DateTime.UtcNow.AddSeconds(1), 105.0)
            };
            _telemetryReader.SetSamples(1, samples, endRow: 1);

            var result = await _service.GetSessionDataAsync(1, startRow: 0, endRow: 1);
            var samplesResult = await ConsumeAsync(result);

            Assert.Equal(2, samplesResult.Count);
        }

        [Fact]
        public async Task GetSessionDataAsync_WithRangeParameters_ReturnsFilteredData()
        {
            var samples = new List<TelemetrySample>
            {
                CreateTestSample(DateTime.UtcNow, 100.0),
                CreateTestSample(DateTime.UtcNow.AddSeconds(1), 105.0),
                CreateTestSample(DateTime.UtcNow.AddSeconds(2), 110.0)
            };
            _telemetryReader.SetSamples(1, samples, startRow: 1, endRow: 2);

            var result = await _service.GetSessionDataAsync(1, startRow: 1, endRow: 2);
            var samplesResult = await ConsumeAsync(result);

            Assert.Equal(3, samplesResult.Count);
        }

        [Fact]
        public async Task GetSessionDataAsync_WithNegativeEndRow_ReturnsAllData()
        {
            var samples = new List<TelemetrySample>
            {
                CreateTestSample(DateTime.UtcNow, 100.0),
                CreateTestSample(DateTime.UtcNow.AddSeconds(1), 105.0)
            };
            _telemetryReader.SetSamples(1, samples, endRow: -1);

            var result = await _service.GetSessionDataAsync(1, startRow: 0, endRow: -1);
            var samplesResult = await ConsumeAsync(result);

            Assert.Equal(2, samplesResult.Count);
        }

        [Fact]
        public async Task GetSessionDataAsync_ReturnsEmpty_WhenNoDataForSession()
        {
            _telemetryReader.SetSamples(1, new List<TelemetrySample>());

            var result = await _service.GetSessionDataAsync(1);
            var samplesResult = await ConsumeAsync(result);

            Assert.Empty(samplesResult);
        }

        [Fact]
        public async Task GetSessionDataAsync_ReturnsComplexSampleData()
        {
            var timestamp = DateTime.UtcNow;
            var samples = new List<TelemetrySample>
            {
                new TelemetrySample(
                    timestamp,
                    120.5,
                    new double[] { 85.0, 86.5, 87.0, 88.5 },
                    45.5,
                    0.2,
                    0.8,
                    -0.15)
                {
                    LapNumber = 5,
                    Latitude = 52.0706,
                    Longitude = -1.0174,
                    LateralG = 1.5
                }
            };
            _telemetryReader.SetSamples(1, samples);

            var result = await _service.GetSessionDataAsync(1);
            var samplesResult = await ConsumeAsync(result);

            Assert.Single(samplesResult);
            var sample = samplesResult[0];
            Assert.Equal(120.5, sample.SpeedKph);
            Assert.Equal(new double[] { 85.0, 86.5, 87.0, 88.5 }, sample.TyreTempsC);
            Assert.Equal(45.5, sample.FuelLiters);
            Assert.Equal(0.2, sample.Brake);
            Assert.Equal(0.8, sample.Throttle);
            Assert.Equal(-0.15, sample.Steering);
            Assert.Equal(5, sample.LapNumber);
            Assert.Equal(52.0706, sample.Latitude);
            Assert.Equal(-1.0174, sample.Longitude);
            Assert.Equal(1.5, sample.LateralG);
        }

        [Fact]
        public async Task GetSessionDataAsync_HandlesMultipleSessions()
        {
            var samples1 = new List<TelemetrySample>
            {
                CreateTestSample(DateTime.UtcNow, 100.0)
            };
            var samples2 = new List<TelemetrySample>
            {
                CreateTestSample(DateTime.UtcNow, 200.0)
            };
            _telemetryReader.SetSamples(1, samples1);
            _telemetryReader.SetSamples(2, samples2);

            var result1 = await _service.GetSessionDataAsync(1);
            var result2 = await _service.GetSessionDataAsync(2);
            
            var samplesResult1 = await ConsumeAsync(result1);
            var samplesResult2 = await ConsumeAsync(result2);

            Assert.Equal(100.0, samplesResult1[0].SpeedKph);
            Assert.Equal(200.0, samplesResult2[0].SpeedKph);
        }

        [Fact]
        public async Task GetSessionDataAsync_StreamsLargeDataset()
        {
            var samples = new List<TelemetrySample>();
            for (int i = 0; i < 1000; i++)
            {
                samples.Add(CreateTestSample(DateTime.UtcNow.AddSeconds(i), 100.0 + i));
            }
            _telemetryReader.SetSamples(1, samples);

            var result = await _service.GetSessionDataAsync(1);
            var samplesResult = await ConsumeAsync(result);

            Assert.Equal(1000, samplesResult.Count);
            Assert.Equal(100.0, samplesResult[0].SpeedKph);
            Assert.Equal(1099.0, samplesResult[999].SpeedKph);
        }

        [Fact]
        public async Task GetSessionDataAsync_PassesCorrectParametersToReader()
        {
            _telemetryReader.SetSamples(42, new List<TelemetrySample>(), startRow: 10, endRow: 20);

            var result = await _service.GetSessionDataAsync(42, startRow: 10, endRow: 20);
            
            // Consume the async enumerable to trigger the reader
            await ConsumeAsync(result);

            Assert.Equal(42, _telemetryReader.LastSessionId);
            Assert.Equal(10, _telemetryReader.LastStartRow);
            Assert.Equal(20, _telemetryReader.LastEndRow);
        }

        private static TelemetrySample CreateTestSample(DateTime timestamp, double speed)
        {
            return new TelemetrySample(
                timestamp,
                speed,
                new double[] { 80.0, 81.0, 82.0, 83.0 },
                50.0,
                0.0,
                0.5,
                0.0);
        }

        private static async Task<List<TelemetrySample>> ConsumeAsync(IAsyncEnumerable<TelemetrySample> asyncEnumerable)
        {
            var result = new List<TelemetrySample>();
            await foreach (var item in asyncEnumerable)
            {
                result.Add(item);
            }
            return result;
        }

        private class TestLmuTelemetryReader : ILmuTelemetryReader
        {
            private int _sessionCount;
            private List<ChannelInfo> _channels = new();
            private readonly Dictionary<int, List<TelemetrySample>> _sessionSamples = new();
            private readonly Dictionary<int, (int startRow, int endRow)> _sessionRanges = new();

            public int LastSessionId { get; private set; }
            public int LastStartRow { get; private set; }
            public int LastEndRow { get; private set; }

            public void SetSessionCount(int count)
            {
                _sessionCount = count;
            }

            public void SetChannels(List<ChannelInfo> channels)
            {
                _channels = channels;
            }

            public void SetSamples(int sessionId, List<TelemetrySample> samples, int startRow = 0, int endRow = -1)
            {
                _sessionSamples[sessionId] = samples;
                _sessionRanges[sessionId] = (startRow, endRow);
            }

            public Task<int> GetSessionCountAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_sessionCount);
            }

            public Task<List<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_channels);
            }

            public async IAsyncEnumerable<TelemetrySample> ReadSamplesAsync(
                int sessionId, 
                int startRow, 
                int endRow, 
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                LastSessionId = sessionId;
                LastStartRow = startRow;
                LastEndRow = endRow;

                if (_sessionSamples.TryGetValue(sessionId, out var samples))
                {
                    if (_sessionRanges.TryGetValue(sessionId, out var range))
                    {
                        if (startRow != range.startRow || endRow != range.endRow)
                        {
                            yield break;
                        }
                    }

                    foreach (var sample in samples)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        yield return sample;
                    }
                }

                await Task.CompletedTask;
            }
        }
    }
}
