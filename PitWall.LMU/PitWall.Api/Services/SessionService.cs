using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<SessionService> _logger;

        public SessionService(ILmuTelemetryReader lmuReader, ILogger<SessionService> logger)
        {
            _lmuReader = lmuReader ?? throw new ArgumentNullException(nameof(lmuReader));
            _logger = logger;
        }

        public async Task<int> GetTotalSessionCountAsync()
        {
            var count = await _lmuReader.GetSessionCountAsync();
            _logger.LogDebug("Total session count resolved to {SessionCount}.", count);
            return count;
        }

        public async Task<List<ChannelInfo>> GetAvailableChannelsAsync()
        {
            var channels = await _lmuReader.GetChannelsAsync();
            _logger.LogDebug("Available channel groups: {ChannelCount}.", channels.Count);
            return channels;
        }

        public async Task<IAsyncEnumerable<TelemetrySample>> GetSessionDataAsync(int sessionId, int startRow = 0, int endRow = -1)
        {
            // Return async enumerable for streaming data
            _logger.LogDebug("Streaming session data for {SessionId}, start {StartRow}, end {EndRow}.", sessionId, startRow, endRow);
            return await Task.FromResult(_lmuReader.ReadSamplesAsync(sessionId, startRow, endRow));
        }
    }
}
