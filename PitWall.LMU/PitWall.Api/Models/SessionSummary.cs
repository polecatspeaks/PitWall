using System;

namespace PitWall.Api.Models
{
    public class SessionSummary
    {
        public int SessionId { get; init; }
        public DateTimeOffset? StartTimeUtc { get; init; }
        public DateTimeOffset? EndTimeUtc { get; init; }
        public string Track { get; init; } = "Unknown";
        public string Car { get; init; } = "Unknown";
    }
}
