using System.Collections.Generic;
using PitWall.Agent.Models;

namespace PitWall.Agent.Services
{
    public static class AgentConfigValidator
    {
        public static IReadOnlyList<string> Validate(AgentConfigUpdate? update)
        {
            var errors = new List<string>();
            if (update == null)
            {
                errors.Add("Request body is required.");
                return errors;
            }

            if (update.LLMTimeoutMs.HasValue && update.LLMTimeoutMs.Value <= 0)
            {
                errors.Add("LLM timeout must be greater than zero.");
            }

            if (update.LLMDiscoveryTimeoutMs.HasValue && update.LLMDiscoveryTimeoutMs.Value <= 0)
            {
                errors.Add("Discovery timeout must be greater than zero.");
            }

            if (update.LLMDiscoveryMaxConcurrency.HasValue && update.LLMDiscoveryMaxConcurrency.Value <= 0)
            {
                errors.Add("Discovery max concurrency must be greater than zero.");
            }

            if (update.LLMDiscoveryPort.HasValue)
            {
                var port = update.LLMDiscoveryPort.Value;
                if (port < 1 || port > 65535)
                {
                    errors.Add("Discovery port must be between 1 and 65535.");
                }
            }

            return errors;
        }
    }
}
