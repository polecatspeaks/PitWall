using PitWall.Agent.Models;

namespace PitWall.Agent.Services.RulesEngine
{
    public interface IRulesEngine
    {
        AgentResponse? TryAnswer(string query, RaceContext context);
    }
}
