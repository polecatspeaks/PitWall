using System.Collections.Generic;
using PitWall.Core.Models;

namespace PitWall.Core.Storage
{
    public interface IDuckDbConnector
    {
        string DatabasePath { get; }
        void EnsureSchema();
        void InsertSamples(string sessionId, IEnumerable<TelemetrySample> samples);
    }
}
