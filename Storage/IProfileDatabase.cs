using System.Collections.Generic;
using System.Threading.Tasks;
using PitWall.Models;

namespace PitWall.Storage
{
    /// <summary>
    /// Interface for profile database operations
    /// </summary>
    public interface IProfileDatabase
    {
        Task<DriverProfile?> GetProfile(string driver, string track, string car);
        Task SaveProfile(DriverProfile profile);
        Task<List<SessionData>> GetRecentSessions(int count);
        Task SaveSession(SessionData session);
    }
}
