using System.Collections.Generic;
using PitWall.Core.Models;

namespace PitWall.Core.Storage
{
    public class InMemoryTelemetryWriter : ITelemetryWriter
    {
        private readonly Dictionary<string, List<TelemetrySample>> _store = new();

        public void WriteSamples(string sessionId, List<TelemetrySample> samples)
        {
            if (!_store.ContainsKey(sessionId))
            {
                _store[sessionId] = new List<TelemetrySample>();
            }

            _store[sessionId].AddRange(samples);
        }

        public List<TelemetrySample> GetSamples(string sessionId)
        {
            return _store.ContainsKey(sessionId) ? new List<TelemetrySample>(_store[sessionId]) : new List<TelemetrySample>();
        }
    }
}
