using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PitWall.Agent.Models
{
    public class AgentConfigUpdate
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        public bool? EnableLLM { get; set; }
        public string? LLMProvider { get; set; }
        public string? LLMEndpoint { get; set; }
        public string? LLMModel { get; set; }
        public int? LLMTimeoutMs { get; set; }

        public string? OpenAiApiKey { get; set; }
        public string? OpenAiEndpoint { get; set; }
        public string? OpenAiModel { get; set; }

        public string? AnthropicApiKey { get; set; }
        public string? AnthropicEndpoint { get; set; }
        public string? AnthropicModel { get; set; }

        public bool? RequirePitForLlm { get; set; }

        public bool? EnableLLMDiscovery { get; set; }
        public int? LLMDiscoveryTimeoutMs { get; set; }
        public int? LLMDiscoveryPort { get; set; }
        public int? LLMDiscoveryMaxConcurrency { get; set; }
        public string? LLMDiscoverySubnetPrefix { get; set; }
    }
}
