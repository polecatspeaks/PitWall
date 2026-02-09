using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public class TelemetryStreamClient : ITelemetryStreamClient
    {
        private readonly Uri _baseUri;

        public TelemetryStreamClient(Uri baseUri)
        {
            _baseUri = baseUri;
        }

        public async Task ConnectAsync(int sessionId, Action<TelemetrySampleDto> onMessage, CancellationToken cancellationToken)
        {
            using var socket = new ClientWebSocket();
            var uri = new Uri(_baseUri, $"/ws/state?sessionId={sessionId}");

            await socket.ConnectAsync(uri, cancellationToken);

            var buffer = new byte[4096];
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var sample = TelemetryMessageParser.Parse(json);
                onMessage(sample);
            }
        }
    }
}
