using System.Collections.Generic;

namespace PitWall.Agent.Models
{
    public class AgentRequest
    {
        public string Query { get; set; } = string.Empty;
        public Dictionary<string, object> Context { get; set; } = new();
    }
}
