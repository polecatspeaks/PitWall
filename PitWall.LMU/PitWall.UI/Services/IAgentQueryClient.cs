using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public interface IAgentQueryClient
    {
        Task<AgentResponseDto> SendQueryAsync(string query, CancellationToken cancellationToken);
    }
}
