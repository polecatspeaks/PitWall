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

        /// <summary>
        /// Retrieves the agent health status.
        /// </summary>
        Task<AgentHealthDto> GetHealthAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Tests the configured LLM connection on the agent.
        /// </summary>
        Task<AgentLlmTestDto> TestLlmAsync(CancellationToken cancellationToken);
    }
}
