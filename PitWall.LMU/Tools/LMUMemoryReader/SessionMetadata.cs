using System;

namespace LMUMemoryReader;

public sealed class SessionMetadata
{
    public DateTime StartTimeUtc { get; init; }
    public string SessionType { get; init; } = string.Empty;
    public string TrackName { get; init; } = string.Empty;
    public string CarName { get; init; } = string.Empty;
}
