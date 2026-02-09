using System.Threading.Tasks;
using PitWall.Agent.Models;

namespace PitWall.Agent.Services.LLM
{
    public interface ILLMService
    {
        bool IsEnabled { get; }
        bool IsAvailable { get; }

        Task<bool> TestConnectionAsync();
        Task<AgentResponse> QueryAsync(string query, RaceContext context);
    }
}
