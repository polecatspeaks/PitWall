using System;
using System.Collections.Generic;
using PitWall.Core.Models;

namespace PitWall.Core.Storage
{
    public class DuckDbTelemetryWriter : ITelemetryWriter
    {
        private readonly IDuckDbConnector _connector;

        public DuckDbTelemetryWriter(IDuckDbConnector connector)
        {
            _connector = connector ?? throw new ArgumentNullException(nameof(connector));
            _connector.EnsureSchema();
        }

        public List<TelemetrySample> GetSamples(string sessionId)
        {
            throw new NotImplementedException("GetSamples is not implemented for DuckDbTelemetryWriter yet.");
        }

        public void WriteSamples(string sessionId, List<TelemetrySample> samples)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            _connector.InsertSamples(sessionId, samples);
        }
    }
}
