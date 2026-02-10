namespace PitWall.UI.Models
{
    public class AgentConfigDto
    {
        public bool EnableLLM { get; set; }
        public string LLMProvider { get; set; } = string.Empty;
        public string LLMEndpoint { get; set; } = string.Empty;
        public string LLMModel { get; set; } = string.Empty;
        public int LLMTimeoutMs { get; set; }

        public bool RequirePitForLlm { get; set; }

        public bool EnableLLMDiscovery { get; set; }
        public int LLMDiscoveryTimeoutMs { get; set; }
        public int LLMDiscoveryPort { get; set; }
        public int LLMDiscoveryMaxConcurrency { get; set; }
        public string? LLMDiscoverySubnetPrefix { get; set; }

        public string OpenAIEndpoint { get; set; } = string.Empty;
        public string OpenAIModel { get; set; } = string.Empty;
        public bool OpenAiApiKeyConfigured { get; set; }

        public string AnthropicEndpoint { get; set; } = string.Empty;
        public string AnthropicModel { get; set; } = string.Empty;
        public bool AnthropicApiKeyConfigured { get; set; }
    }
}
