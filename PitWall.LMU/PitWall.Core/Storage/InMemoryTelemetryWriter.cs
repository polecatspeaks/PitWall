using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Core.Models;

namespace PitWall.Core.Storage
{
    public class InMemoryTelemetryWriter : ITelemetryWriter
    {
        private readonly Dictionary<string, List<TelemetrySample>> _store = new();
        private readonly ILogger<InMemoryTelemetryWriter> _logger;

        public InMemoryTelemetryWriter(ILogger<InMemoryTelemetryWriter>? logger = null)
        {
            _logger = logger ?? NullLogger<InMemoryTelemetryWriter>.Instance;
        }

        public void WriteSamples(string sessionId, List<TelemetrySample> samples)
        {
            if (!_store.ContainsKey(sessionId))
            {
                _store[sessionId] = new List<TelemetrySample>();
            }

            _store[sessionId].AddRange(samples);
            _logger.LogDebug("Stored {SampleCount} samples for session {SessionId} (total {Total}).", samples.Count, sessionId, _store[sessionId].Count);
        }

        public List<TelemetrySample> GetSamples(string sessionId)
        {
            var samples = _store.ContainsKey(sessionId) ? new List<TelemetrySample>(_store[sessionId]) : new List<TelemetrySample>();
            _logger.LogDebug("Retrieved {SampleCount} samples for session {SessionId}.", samples.Count, sessionId);
            return samples;
        }
    }
}
