using System;
using rF2SharedMemoryNet.LMUData.Models;
using rF2SharedMemoryNet.RF2Data.Structs;

namespace LMUMemoryReader;

public sealed class TelemetryLogEntry
{
    public DateTime TimestampUtc { get; init; }
    public Telemetry Telemetry { get; init; }
    public Scoring Scoring { get; init; }
    public Electronics Electronics { get; init; } = new();
}
