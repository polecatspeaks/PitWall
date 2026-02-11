using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PitWall.Api.Models;

namespace PitWall.Api.Services
{
    public class NullSessionSummaryService : ISessionSummaryService
    {
        public Task<IReadOnlyList<SessionSummary>> GetSessionSummariesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult((IReadOnlyList<SessionSummary>)new List<SessionSummary>());
        }

        public Task<SessionSummary?> GetSessionSummaryAsync(int sessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SessionSummary?>(null);
        }
    }
}
