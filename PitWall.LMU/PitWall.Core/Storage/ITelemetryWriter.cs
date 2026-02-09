using System.Collections.Generic;
using PitWall.Core.Models;

namespace PitWall.Core.Storage
{
    public interface ITelemetryWriter
    {
        void WriteSamples(string sessionId, List<TelemetrySample> samples);
        List<TelemetrySample> GetSamples(string sessionId);
    }
}
