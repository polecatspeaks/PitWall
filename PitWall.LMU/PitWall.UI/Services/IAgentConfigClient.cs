using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public interface IAgentConfigClient
    {
        Task<AgentConfigDto> GetConfigAsync(CancellationToken cancellationToken);
        Task<AgentConfigDto> UpdateConfigAsync(AgentConfigUpdateDto update, CancellationToken cancellationToken);
    }
}
