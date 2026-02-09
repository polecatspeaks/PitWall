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

        public string OpenAIApiKey { get; set; } = string.Empty;
        public string OpenAIEndpoint { get; set; } = "https://api.openai.com";
        public string OpenAIModel { get; set; } = "gpt-4o-mini";

        public string AnthropicApiKey { get; set; } = string.Empty;
        public string AnthropicEndpoint { get; set; } = "https://api.anthropic.com";
        public string AnthropicModel { get; set; } = "claude-3-5-sonnet";

        public bool RequirePitForLlm { get; set; } = true;

        public bool EnableLLMDiscovery { get; set; } = true;
        public int LLMDiscoveryTimeoutMs { get; set; } = 500;
        public int LLMDiscoveryPort { get; set; } = 11434;
        public int LLMDiscoveryMaxConcurrency { get; set; } = 32;
        public string? LLMDiscoverySubnetPrefix { get; set; }
    }
}
