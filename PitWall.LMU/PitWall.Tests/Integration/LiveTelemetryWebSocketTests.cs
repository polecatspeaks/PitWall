using System;
using System.Collections.Generic;
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
    /// Integration tests for the /ws/live WebSocket endpoint.
    /// Tests that the API correctly wires TelemetryPipelineService
    /// and streams live telemetry snapshots over WebSocket.
    /// </summary>
    public class LiveTelemetryWebSocketTests : IDisposable
    {
        private readonly WebApplicationFactory<global::Program> _factory;

        public LiveTelemetryWebSocketTests()
        {
            _factory = new WebApplicationFactory<global::Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        // Replace shared memory with a mock that provides test data
                        services.RemoveAll<ISharedMemoryReader>();
                        var mockReader = CreateMockSharedMemoryReader(isConnected: true);
                        services.AddSingleton(mockReader.Object);
                    });
                });
        }

        public void Dispose()
        {
            _factory.Dispose();
        }

        [Fact]
        public async Task LiveEndpoint_NonWebSocket_ReturnsBadRequest()
        {
            // Non-WebSocket HTTP requests to /ws/live should return 400
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/ws/live");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task LiveEndpoint_WebSocket_SendsMetaMessageFirst()
        {
            // The first message over WebSocket should be a meta message with mode=live
            var wsClient = _factory.Server.CreateWebSocketClient();
            using var socket = await wsClient.ConnectAsync(
                new Uri(_factory.Server.BaseAddress, "/ws/live"),
                CancellationToken.None);

            var firstMessage = await ReceiveJsonAsync(socket);

            Assert.Equal("meta", firstMessage.GetProperty("type").GetString());
            Assert.Equal("live", firstMessage.GetProperty("mode").GetString());

            await CloseSocketAsync(socket);
        }

        [Fact]
        public async Task LiveEndpoint_WebSocket_StreamsTelemetrySnapshots()
        {
            // After the meta message, telemetry snapshots should stream
            var wsClient = _factory.Server.CreateWebSocketClient();
            using var socket = await wsClient.ConnectAsync(
                new Uri(_factory.Server.BaseAddress, "/ws/live"),
                CancellationToken.None);

            // Skip meta
            _ = await ReceiveJsonAsync(socket);

            // Read at least one telemetry message
            var telemetry = await ReceiveJsonAsync(socket);

            Assert.Equal("telemetry", telemetry.GetProperty("type").GetString());
            Assert.Equal("live", telemetry.GetProperty("mode").GetString());
            Assert.True(telemetry.TryGetProperty("speedKph", out _));
            Assert.True(telemetry.TryGetProperty("throttle", out _));
            Assert.True(telemetry.TryGetProperty("brake", out _));

            await CloseSocketAsync(socket);
        }

        [Fact]
        public async Task LiveEndpoint_SourceUnavailable_SendsErrorAndCloses()
        {
            // When shared memory is unavailable, send error message and close
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
                new Uri(factory.Server.BaseAddress, "/ws/live"),
                CancellationToken.None);

            var message = await ReceiveJsonAsync(socket);

            Assert.Equal("error", message.GetProperty("type").GetString());
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
                        200.0 + callCount,
                        new double[] { 80, 82, 79, 81 },
                        50.0,
                        0.3,
                        0.7,
                        0.1)
                    { LapNumber = 5, Latitude = 48.123, Longitude = 11.456 };
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
                    // Server is streaming; abort the connection
                    socket.Abort();
                }
            }
        }

        #endregion
    }
}
