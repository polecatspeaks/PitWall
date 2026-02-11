using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PitWall.Api.Models;

namespace PitWall.Api.Services
{
    public interface ISessionSummaryService
    {
        Task<IReadOnlyList<SessionSummary>> GetSessionSummariesAsync(CancellationToken cancellationToken = default);
        Task<SessionSummary?> GetSessionSummaryAsync(int sessionId, CancellationToken cancellationToken = default);
    }
}
