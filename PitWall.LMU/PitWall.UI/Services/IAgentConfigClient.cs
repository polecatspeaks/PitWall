using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    /// <summary>
    /// Client for the PitWall Agent configuration and health endpoints.
    /// </summary>
    public interface IAgentConfigClient
    {
        /// <summary>Retrieves the current agent configuration (<c>GET /agent/config</c>).</summary>
        Task<AgentConfigDto> GetConfigAsync(CancellationToken cancellationToken);

        /// <summary>Saves an updated agent configuration (<c>PUT /agent/config</c>).</summary>
        Task<AgentConfigDto> UpdateConfigAsync(AgentConfigUpdateDto update, CancellationToken cancellationToken);

        /// <summary>Discovers available LLM endpoints on the local network (<c>GET /agent/llm/discover</c>).</summary>
        Task<IReadOnlyList<string>> DiscoverEndpointsAsync(CancellationToken cancellationToken);

        /// <summary>Returns the agent health status (<c>GET /agent/health</c>).</summary>
        Task<AgentHealthDto> GetHealthAsync(CancellationToken cancellationToken);

        /// <summary>Tests the LLM connection (<c>GET /agent/llm/test</c>).</summary>
        Task<AgentLlmTestDto> TestLlmAsync(CancellationToken cancellationToken);
    }
}
