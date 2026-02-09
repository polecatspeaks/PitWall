using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PitWall.Core.Models;
using PitWall.Core.Services;

namespace PitWall.Api.Services
{
    /// <summary>
    /// Service for managing telemetry sessions and data retrieval.
    /// </summary>
    public interface ISessionService
    {
        Task<int> GetTotalSessionCountAsync();
        Task<List<ChannelInfo>> GetAvailableChannelsAsync();
        Task<IAsyncEnumerable<TelemetrySample>> GetSessionDataAsync(int sessionId, int startRow = 0, int endRow = -1);
    }

    public class SessionService : ISessionService
    {
        private readonly ILmuTelemetryReader _lmuReader;

        public SessionService(ILmuTelemetryReader lmuReader)
        {
            _lmuReader = lmuReader ?? throw new ArgumentNullException(nameof(lmuReader));
        }

        public async Task<int> GetTotalSessionCountAsync()
        {
            var count = await _lmuReader.GetSessionCountAsync();
            return count;
        }

        public async Task<List<ChannelInfo>> GetAvailableChannelsAsync()
        {
            return await _lmuReader.GetChannelsAsync();
        }

        public async Task<IAsyncEnumerable<TelemetrySample>> GetSessionDataAsync(int sessionId, int startRow = 0, int endRow = -1)
        {
            // Return async enumerable for streaming data
            return await Task.FromResult(_lmuReader.ReadSamplesAsync(sessionId, startRow, endRow));
        }
    }
}
