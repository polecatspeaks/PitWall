using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PitWall.Models.Telemetry;
using PitWall.Telemetry;

namespace PitWall.Storage.Telemetry
{
    /// <summary>
    /// Repository for telemetry session operations
    /// Manages ImportedSession with hierarchical relationships:
    /// ImportedSession -> SessionMetadata, LapMetadata[], TelemetrySample[]
    /// </summary>
    public interface ISessionRepository
    {
        /// <summary>
        /// Saves a complete imported session with metadata, laps, and samples
        /// </summary>
        Task<string> SaveSessionAsync(ImportedSession session);

        /// <summary>
        /// Gets a session by ID with all related data
        /// </summary>
        Task<ImportedSession?> GetSessionAsync(string sessionId);

        /// <summary>
        /// Gets recent sessions with metadata only (no samples)
        /// </summary>
        Task<List<ImportedSession>> GetRecentSessionsAsync(int count);

        /// <summary>
        /// Deletes a session and all related data
        /// </summary>
        Task<bool> DeleteSessionAsync(string sessionId);
    }

    /// <summary>
    /// Repository for lap metadata operations
    /// </summary>
    public interface ILapRepository
    {
        /// <summary>
        /// Gets all laps for a session
        /// </summary>
        Task<List<LapMetadata>> GetSessionLapsAsync(string sessionId);

        /// <summary>
        /// Gets a specific lap by session and lap number
        /// </summary>
        Task<LapMetadata?> GetLapAsync(string sessionId, int lapNumber);

        /// <summary>
        /// Saves lap metadata for a session
        /// </summary>
        Task SaveLapsAsync(string sessionId, List<LapMetadata> laps);
    }

    /// <summary>
    /// Repository for telemetry sample operations
    /// </summary>
    public interface ITelemetrySampleRepository
    {
        /// <summary>
        /// Saves 60Hz telemetry samples for a session
        /// </summary>
        Task SaveSamplesAsync(string sessionId, List<TelemetrySample> samples);

        /// <summary>
        /// Gets samples for a session with optional lap filter
        /// </summary>
        Task<List<TelemetrySample>> GetSamplesAsync(string sessionId, int? lapNumber = null);

        /// <summary>
        /// Gets sample count for a session
        /// </summary>
        Task<int> GetSampleCountAsync(string sessionId);
    }
}
