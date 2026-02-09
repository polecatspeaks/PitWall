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
        private readonly ILmuTelemetryReader? _lmuReader;

        public SessionService(ILmuTelemetryReader? lmuReader = null)
        {
            _lmuReader = lmuReader;
        }

        public async Task<int> GetTotalSessionCountAsync()
        {
            if (_lmuReader == null)
                return 0;

            // For now, return a hardcoded count based on imported sessions (1-230)
            return await Task.FromResult(229);
        }

        public async Task<List<ChannelInfo>> GetAvailableChannelsAsync()
        {
            if (_lmuReader == null)
                return new List<ChannelInfo>();

            return await _lmuReader.GetChannelsAsync();
        }

        public async Task<IAsyncEnumerable<TelemetrySample>> GetSessionDataAsync(int sessionId, int startRow = 0, int endRow = -1)
        {
            if (_lmuReader == null)
                throw new InvalidOperationException("LmuTelemetryReader not available");

            // Return async enumerable for streaming data
            return await Task.FromResult(_lmuReader.ReadSamplesAsync(startRow, endRow));
        }
    }
}
