using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PitWall.Agent.Services.LLM
{
    public interface ILLMDiscoveryService
    {
        Task<IReadOnlyList<string>> DiscoverAsync(CancellationToken cancellationToken = default);
    }
}
