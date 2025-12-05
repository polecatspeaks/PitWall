using System;

namespace PitWall.Replay
{
    /// <summary>
    /// Parsed metadata from iRacing replay YAML header
    /// </summary>
    public class ReplayMetadata
    {
        public string TrackName { get; set; } = string.Empty;
        public string CarName { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public int SessionLength { get; set; }
        public string SessionId { get; set; } = string.Empty;
    }
}
