namespace PitWall.UI.Models
{
    public class AgentHealthDto
    {
        public bool LlmEnabled { get; set; }
        public bool LlmAvailable { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
    }
}
