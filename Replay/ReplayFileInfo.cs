using System;

namespace PitWall.Replay
{
    /// <summary>
    /// Metadata about a replay file discovered during scanning
    /// </summary>
    public class ReplayFileInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public long FileSize { get; set; }
    }
}
