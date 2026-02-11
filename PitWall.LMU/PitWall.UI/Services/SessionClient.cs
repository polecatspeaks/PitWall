using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public class SessionClient : ISessionClient
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly HttpClient _httpClient;
        private readonly ILogger<SessionClient> _logger;

        public SessionClient(HttpClient httpClient, ILogger<SessionClient>? logger = null)
        {
            _httpClient = httpClient;
            _logger = logger ?? NullLogger<SessionClient>.Instance;
        }

        public async Task<int> GetSessionCountAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Requesting session count from {BaseAddress}", _httpClient.BaseAddress);
                var response = await _httpClient.GetAsync("/api/sessions/count", cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<SessionCountResponse>(json, Options);
                var count = result?.SessionCount ?? 0;
                _logger.LogDebug("Received session count {SessionCount}", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch session count.");
                throw;
            }
        }

        public async Task<IReadOnlyList<SessionSummaryDto>> GetSessionSummariesAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Requesting session summaries from {BaseAddress}", _httpClient.BaseAddress);
                var response = await _httpClient.GetAsync("/api/sessions/summary", cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<SessionSummaryResponse>(json, Options);
                var sessions = result?.Sessions ?? new List<SessionSummaryDto>();
                _logger.LogDebug("Received {SessionCount} session summaries", sessions.Count);
                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch session summaries.");
                throw;
            }
        }

        public async Task<SessionSummaryDto?> UpdateSessionMetadataAsync(int sessionId, SessionMetadataUpdateDto update, CancellationToken cancellationToken)
        {
            try
            {
                var payload = JsonSerializer.Serialize(update, Options);
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"/api/sessions/{sessionId}/metadata", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var summary = JsonSerializer.Deserialize<SessionSummaryDto>(json, Options);
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update session metadata for session {SessionId}.", sessionId);
                throw;
            }
        }

        public async Task<IReadOnlyList<TelemetrySampleDto>> GetSessionSamplesAsync(int sessionId, int startRow, int endRow, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/sessions/{sessionId}/samples?startRow={startRow}&endRow={endRow}", cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<SessionSamplesResponse>(json, Options);
                return result?.Samples ?? new List<TelemetrySampleDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch samples for session {SessionId}.", sessionId);
                throw;
            }
        }

        private record SessionCountResponse(int SessionCount);
        private record SessionSummaryResponse(List<SessionSummaryDto> Sessions);
        private record SessionSamplesResponse(int SessionId, int SampleCount, List<TelemetrySampleDto> Samples);
    }
}
