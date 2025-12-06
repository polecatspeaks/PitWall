using System;
using System.IO;
using System.Text;

namespace PitWall.Core
{
    /// <summary>
    /// Minimal file logger to help diagnose SimHub plugin issues.
    /// Writes to %LOCALAPPDATA%\PitWall\logs\pitwall.log
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly string _logDir;
        private static readonly string _logFile;

        static Logger()
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PitWall", "logs");
            try
            {
                Directory.CreateDirectory(baseDir);
            }
            catch
            {
                // If directory creation fails, fallback to current directory
                baseDir = Path.Combine(Environment.CurrentDirectory, "logs");
                Directory.CreateDirectory(baseDir);
            }

            _logDir = baseDir;
            _logFile = Path.Combine(_logDir, "pitwall.log");
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Error(string message) => Write("ERROR", message);

        private static void Write(string level, string message)
        {
            try
            {
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
                lock (_lock)
                {
                    File.AppendAllText(_logFile, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Swallow logging errors; never crash plugin
            }
        }
    }
}