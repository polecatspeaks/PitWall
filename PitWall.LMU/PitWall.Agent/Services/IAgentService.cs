using System.Threading.Tasks;
using PitWall.Agent.Models;

namespace PitWall.Agent.Services
{
    public interface IAgentService
    {
        Task<AgentResponse> ProcessQueryAsync(AgentRequest request);
    }
}
