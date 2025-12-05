using System;

namespace PitWall
{
    /// <summary>
    /// Settings for PitWall plugin
    /// Persisted by SimHub between sessions
    /// </summary>
    public class PitWallSettings
    {
        /// <summary>
        /// Path to iRacing replay folder for historical data import
        /// </summary>
        public string ReplayFolderPath { get; set; } = string.Empty;

        /// <summary>
        /// Last time replay import was run
        /// </summary>
        public DateTime? LastImportDate { get; set; }

        /// <summary>
        /// Number of profiles imported from last replay processing
        /// </summary>
        public int ProfilesImported { get; set; }

        /// <summary>
        /// Number of replays processed in last import
        /// </summary>
        public int ReplaysProcessed { get; set; }
    }
}
