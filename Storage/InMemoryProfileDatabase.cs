using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PitWall.Models;

namespace PitWall.Storage
{
    /// <summary>
    /// In-memory implementation of profile database for testing
    /// </summary>
    public class InMemoryProfileDatabase : IProfileDatabase
    {
        private readonly Dictionary<string, DriverProfile> _profiles = new();
        private readonly List<SessionData> _sessions = new();

        public Task<DriverProfile?> GetProfile(string driver, string track, string car)
        {
            string key = MakeKey(driver, track, car);
            _profiles.TryGetValue(key, out var profile);
            return Task.FromResult<DriverProfile?>(profile);
        }

        public Task SaveProfile(DriverProfile profile)
        {
            string key = MakeKey(profile.DriverName, profile.TrackName, profile.CarName);
            _profiles[key] = profile;
            return Task.CompletedTask;
        }

        public Task<List<SessionData>> GetRecentSessions(int count)
        {
            var recent = _sessions
                .OrderByDescending(s => s.SessionDate)
                .Take(count)
                .ToList();
            return Task.FromResult(recent);
        }

        public Task SaveSession(SessionData session)
        {
            _sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task<List<DriverProfile>> GetProfiles(int count)
        {
            var profiles = _profiles.Values
                .OrderByDescending(p => p.LastUpdated)
                .Take(count)
                .ToList();
            return Task.FromResult(profiles);
        }

        private string MakeKey(string driver, string track, string car)
        {
            return $"{driver}|{track}|{car}";
        }
    }
}
