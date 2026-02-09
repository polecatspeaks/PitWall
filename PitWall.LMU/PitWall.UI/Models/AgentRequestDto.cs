using System.Collections.Generic;

namespace PitWall.UI.Models
{
    public class AgentRequestDto
    {
        public string Query { get; set; } = string.Empty;
        public Dictionary<string, object> Context { get; set; } = new();
    }
}
