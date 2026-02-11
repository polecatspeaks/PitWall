using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PitWall.Api.Models;

namespace PitWall.Api.Services
{
    public class NullSessionMetadataStore : ISessionMetadataStore
    {
        public Task<IReadOnlyDictionary<int, SessionMetadata>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult((IReadOnlyDictionary<int, SessionMetadata>)new Dictionary<int, SessionMetadata>());
        }

        public Task<SessionMetadata?> GetAsync(int sessionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SessionMetadata?>(null);
        }

        public Task SetAsync(int sessionId, SessionMetadata metadata, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
