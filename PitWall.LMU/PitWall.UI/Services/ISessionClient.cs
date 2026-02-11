using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public interface ISessionClient
    {
        Task<int> GetSessionCountAsync(CancellationToken cancellationToken);
        Task<IReadOnlyList<SessionSummaryDto>> GetSessionSummariesAsync(CancellationToken cancellationToken);
        Task<SessionSummaryDto?> UpdateSessionMetadataAsync(int sessionId, SessionMetadataUpdateDto update, CancellationToken cancellationToken);
    }
}
