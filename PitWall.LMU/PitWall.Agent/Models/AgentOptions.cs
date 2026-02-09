namespace PitWall.Agent.Models
{
    public class AgentOptions
    {
        public const string SectionName = "Agent";

        public bool EnableLLM { get; set; }
        public string LLMProvider { get; set; } = "Ollama";
        public string LLMEndpoint { get; set; } = "http://localhost:11434";
        public string LLMModel { get; set; } = "llama3.2";
        public int LLMTimeoutMs { get; set; } = 5000;
    }
}
