using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public class TelemetryStreamClient : ITelemetryStreamClient
    {
        private readonly Uri _baseUri;
        private readonly ILogger<TelemetryStreamClient> _logger;

        public TelemetryStreamClient(Uri baseUri, ILogger<TelemetryStreamClient>? logger = null)
        {
            _baseUri = baseUri;
            _logger = logger ?? NullLogger<TelemetryStreamClient>.Instance;
        }

        public async Task ConnectAsync(int sessionId, int startRow, int endRow, int intervalMs, Action<TelemetrySampleDto> onMessage, CancellationToken cancellationToken)
        {
            using var socket = new ClientWebSocket();
            var uri = new Uri(_baseUri, $"/ws/state?sessionId={sessionId}&startRow={startRow}&endRow={endRow}&intervalMs={intervalMs}");
            _logger.LogDebug("Connecting telemetry stream to {Uri}", uri);

            await socket.ConnectAsync(uri, cancellationToken);
            _logger.LogInformation("Telemetry stream connected. Session {SessionId}", sessionId);

            var buffer = new byte[4096];
            int messageCount = 0;
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Telemetry stream closed by server.");
                    break;
                }

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                
                // Check for completion message (simple but safe check)
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("type", out var typeElement) && 
                        typeElement.GetString() == "complete")
                    {
                        _logger.LogInformation("Received completion message from server.");
                        break;
                    }
                }
                catch
                {
                    // Not a JSON object or doesn't have type property - treat as telemetry data
                }
                
                // Log every 10th message to avoid spam
                if (++messageCount % 10 == 0)
                {
                    Console.WriteLine($"[StreamClient] Message #{messageCount} Raw JSON: {json.Substring(0, Math.Min(150, json.Length))}");
                }
                
                var sample = TelemetryMessageParser.Parse(json);
                onMessage(sample);
            }

            _logger.LogDebug("Telemetry stream loop ended for session {SessionId}", sessionId);
        }
    }
}
