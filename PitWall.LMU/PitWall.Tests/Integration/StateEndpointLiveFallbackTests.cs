using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using PitWall.Core.Models;
using PitWall.Core.Services;
using PitWall.Telemetry.Live.Services;
using Xunit;

namespace PitWall.Tests.Integration
{
    /// <summary>
    /// Tests that /ws/state auto-detects live telemetry availability and
    /// switches between live streaming and DuckDB replay modes.
    /// Acceptance criteria for issue #20.
    /// </summary>
    public class StateEndpointLiveFallbackTests : IDisposable
    {
        private readonly WebApplicationFactory<global::Program> _liveFactory;

        public StateEndpointLiveFallbackTests()
        {
            _liveFactory = new WebApplicationFactory<global::Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        // Wire shared memory as connected (live mode available)
                        services.RemoveAll<ISharedMemoryReader>();
                        var mockReader = CreateMockSharedMemoryReader(isConnected: true);
                        services.AddSingleton(mockReader.Object);
                    });
                });
        }

        public void Dispose()
        {
            _liveFactory.Dispose();
        }

        [Fact]
        public async Task StateEndpoint_WhenLiveAvailable_NoSessionId_StreamsLiveData()
        {
            // /ws/state without sessionId should stream live data when shared memory is available
            var wsClient = _liveFactory.Server.CreateWebSocketClient();
            using var socket = await wsClient.ConnectAsync(
                new Uri(_liveFactory.Server.BaseAddress, "/ws/state"),
                CancellationToken.None);

            // Skip meta message
            _ = await ReceiveJsonAsync(socket);

            // Should receive telemetry in live mode
            var telemetry = await ReceiveJsonAsync(socket);

            Assert.Equal("telemetry", telemetry.GetProperty("type").GetString());
            Assert.Equal("live", telemetry.GetProperty("mode").GetString());
            Assert.True(telemetry.TryGetProperty("speedKph", out _));

            await CloseSocketAsync(socket);
        }

        [Fact]
        public async Task StateEndpoint_WhenLiveAvailable_SendsMetaWithLiveMode()
        {
            // First message should be a meta message indicating live mode
            var wsClient = _liveFactory.Server.CreateWebSocketClient();
            using var socket = await wsClient.ConnectAsync(
                new Uri(_liveFactory.Server.BaseAddress, "/ws/state"),
                CancellationToken.None);

            var meta = await ReceiveJsonAsync(socket);

            Assert.Equal("meta", meta.GetProperty("type").GetString());
            Assert.Equal("live", meta.GetProperty("mode").GetString());
            Assert.True(meta.TryGetProperty("sessionId", out _));

            await CloseSocketAsync(socket);
        }

        [Fact]
        public async Task StateEndpoint_WhenLiveUnavailable_NoSessionId_SendsError()
        {
            // No live source and no sessionId → error message
            using var factory = new WebApplicationFactory<global::Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<ISharedMemoryReader>();
                        var mockReader = CreateMockSharedMemoryReader(isConnected: false);
                        services.AddSingleton(mockReader.Object);
                    });
                });

            var wsClient = factory.Server.CreateWebSocketClient();
            using var socket = await wsClient.ConnectAsync(
                new Uri(factory.Server.BaseAddress, "/ws/state"),
                CancellationToken.None);

            var message = await ReceiveJsonAsync(socket);

            Assert.Equal("error", message.GetProperty("type").GetString());
        }

        [Fact]
        public async Task StateEndpoint_NonWebSocket_ReturnsBadRequest()
        {
            // Non-WebSocket HTTP requests to /ws/state should return 400
            var client = _liveFactory.CreateClient();
            var response = await client.GetAsync("/ws/state");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task StateEndpoint_WhenLiveAvailable_IgnoresSessionIdParam()
        {
            // Even if sessionId is provided, live mode takes priority when available
            var wsClient = _liveFactory.Server.CreateWebSocketClient();
            using var socket = await wsClient.ConnectAsync(
                new Uri(_liveFactory.Server.BaseAddress, "/ws/state?sessionId=999"),
                CancellationToken.None);

            var meta = await ReceiveJsonAsync(socket);

            // Should be live mode, not replay mode
            Assert.Equal("meta", meta.GetProperty("type").GetString());
            Assert.Equal("live", meta.GetProperty("mode").GetString());

            await CloseSocketAsync(socket);
        }

        #region Helpers

        private static Mock<ISharedMemoryReader> CreateMockSharedMemoryReader(bool isConnected)
        {
            var mock = new Mock<ISharedMemoryReader>();
            mock.Setup(r => r.IsConnected).Returns(isConnected);

            if (isConnected)
            {
                var callCount = 0;
                mock.Setup(r => r.GetLatestTelemetry()).Returns(() =>
                {
                    callCount++;
                    return new TelemetrySample(
                        DateTime.UtcNow,
                        180.0 + callCount,
                        new double[] { 85, 87, 84, 86 },
                        45.0,
                        0.2,
                        0.8,
                        0.05)
                    { LapNumber = 3 };
                });
            }

            return mock;
        }

        private static async Task<JsonElement> ReceiveJsonAsync(WebSocket socket, int timeoutMs = 5000)
        {
            var buffer = new byte[4096];
            using var cts = new CancellationTokenSource(timeoutMs);

            var result = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cts.Token);

            var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
            return JsonDocument.Parse(text).RootElement;
        }

        private static async Task CloseSocketAsync(WebSocket socket)
        {
            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                try
                {
                    using var cts = new CancellationTokenSource(2000);
                    await socket.CloseOutputAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Test complete",
                        cts.Token);
                }
                catch (OperationCanceledException)
                {
                    socket.Abort();
                }
            }
        }

        #endregion
    }
}
