namespace PitWall.Agent.Models
{
    public class AgentResponse
    {
        public string Answer { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public int ResponseTimeMs { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}
