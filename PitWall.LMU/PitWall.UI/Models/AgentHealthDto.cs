namespace PitWall.UI.Models
{
    /// <summary>
    /// Response from the <c>GET /agent/health</c> endpoint containing
    /// the current status of the agent and its LLM integration.
    /// </summary>
    public class AgentHealthDto
    {
        /// <summary>Whether LLM integration is enabled in the agent configuration.</summary>
        public bool LlmEnabled { get; set; }

        /// <summary>Whether the LLM provider is currently available.</summary>
        public bool LlmAvailable { get; set; }

        /// <summary>The configured LLM provider name (e.g. "Ollama").</summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>The configured LLM model name (e.g. "llama3.2").</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>The configured LLM endpoint URL.</summary>
        public string Endpoint { get; set; } = string.Empty;
    }
}
