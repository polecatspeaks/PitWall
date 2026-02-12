using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public interface IAgentConfigClient
    {
        Task<AgentConfigDto> GetConfigAsync(CancellationToken cancellationToken);
        Task<AgentConfigDto> UpdateConfigAsync(AgentConfigUpdateDto update, CancellationToken cancellationToken);
        Task<IReadOnlyList<string>> DiscoverEndpointsAsync(CancellationToken cancellationToken);
        Task<AgentHealthDto> CheckHealthAsync(CancellationToken cancellationToken);
        Task<LlmTestDto> TestLlmAsync(CancellationToken cancellationToken);
    }
}
