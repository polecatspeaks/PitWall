namespace PitWall.UI.Models
{
    /// <summary>
    /// Response from the <c>GET /agent/llm/test</c> endpoint indicating
    /// whether the configured LLM provider is enabled and reachable.
    /// </summary>
    public class AgentLlmTestDto
    {
        /// <summary>Whether LLM integration is enabled in the agent configuration.</summary>
        public bool LlmEnabled { get; set; }

        /// <summary>Whether the LLM provider responded successfully to a connection test.</summary>
        public bool Available { get; set; }
    }
}
