using System;
using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public interface ITelemetryStreamClient
    {
        Task ConnectAsync(int sessionId, Action<TelemetrySampleDto> onMessage, CancellationToken cancellationToken);
    }
}
