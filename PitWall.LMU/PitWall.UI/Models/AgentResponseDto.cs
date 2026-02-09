namespace PitWall.UI.Models
{
    public class AgentResponseDto
    {
        public string Answer { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public bool Success { get; set; }
    }
}
