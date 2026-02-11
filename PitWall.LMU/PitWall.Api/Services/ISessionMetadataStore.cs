using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PitWall.Api.Models;

namespace PitWall.Api.Services
{
    public interface ISessionMetadataStore
    {
        Task<IReadOnlyDictionary<int, SessionMetadata>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<SessionMetadata?> GetAsync(int sessionId, CancellationToken cancellationToken = default);
        Task SetAsync(int sessionId, SessionMetadata metadata, CancellationToken cancellationToken = default);
    }
}
