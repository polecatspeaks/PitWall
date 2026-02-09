using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using PitWall.Core.Models;

namespace PitWall.Core.Services
{
    public class NullLmuTelemetryReader : ILmuTelemetryReader
    {
        public Task<int> GetSessionCountAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task<List<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<ChannelInfo>());
        }

        public async IAsyncEnumerable<TelemetrySample> ReadSamplesAsync(
            int sessionId,
            int startRow,
            int endRow,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
