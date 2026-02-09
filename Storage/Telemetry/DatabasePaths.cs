using System;
using System.IO;

namespace PitWall.Storage.Telemetry
{
    /// <summary>
    /// Centralized database path for telemetry/profile storage.
    /// Uses LocalAppData\PitWall\pitwall.db.
    /// </summary>
    public static class DatabasePaths
    {
        private const string DbFileName = "pitwall.db";

        public static string GetDatabasePath()
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PitWall");
            Directory.CreateDirectory(baseDir);
            return Path.Combine(baseDir, DbFileName);
        }
    }
}
