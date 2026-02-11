using System.Threading;
using System.Threading.Tasks;
using PitWall.Agent.Models;

namespace PitWall.Agent.Services;

public interface IAgentOptionsStore
{
    Task SaveAsync(AgentOptions options, CancellationToken cancellationToken);
}
