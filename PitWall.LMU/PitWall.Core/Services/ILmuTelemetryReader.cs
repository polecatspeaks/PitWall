using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PitWall.Core.Models;

namespace PitWall.Core.Services
{
    public interface ILmuTelemetryReader
    {
        Task<int> GetSessionCountAsync(CancellationToken cancellationToken = default);
        Task<List<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken = default);
        IAsyncEnumerable<TelemetrySample> ReadSamplesAsync(
            int sessionId,
            int startRow,
            int endRow,
            CancellationToken cancellationToken = default);
    }
}
