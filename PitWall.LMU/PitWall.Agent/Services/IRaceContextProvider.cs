using System.Threading;
using System.Threading.Tasks;
using PitWall.Agent.Models;

namespace PitWall.Agent.Services
{
    public interface IRaceContextProvider
    {
        Task<RaceContext> BuildAsync(AgentRequest request, CancellationToken cancellationToken = default);
    }
}
