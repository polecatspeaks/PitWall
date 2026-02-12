using PitWall.Agent.Models;
using PitWall.Agent.Services;
using Xunit;

namespace PitWall.Tests
{
    public class AgentValidationTests
    {
        [Fact]
        public void ValidateQuery_ReturnsError_WhenRequestMissing()
        {
            var errors = AgentRequestValidator.ValidateQuery(null);

            Assert.NotEmpty(errors);
        }

        [Fact]
        public void ValidateQuery_ReturnsError_WhenQueryBlank()
        {
            var errors = AgentRequestValidator.ValidateQuery(new AgentRequest { Query = " " });

            Assert.Single(errors);
        }

        [Fact]
        public void ValidateConfig_ReturnsErrors_ForInvalidValues()
        {
            var update = new AgentConfigUpdate
            {
                LLMTimeoutMs = 0,
                LLMDiscoveryTimeoutMs = -1,
                LLMDiscoveryMaxConcurrency = 0,
                LLMDiscoveryPort = 70000
            };

            var errors = AgentConfigValidator.Validate(update);

            Assert.Equal(4, errors.Count);
        }

        [Fact]
        public void ValidateConfig_ReturnsEmpty_WhenValid()
        {
            var update = new AgentConfigUpdate
            {
                LLMTimeoutMs = 5000,
                LLMDiscoveryTimeoutMs = 2000,
                LLMDiscoveryMaxConcurrency = 25,
                LLMDiscoveryPort = 11434
            };

            var errors = AgentConfigValidator.Validate(update);

            Assert.Empty(errors);
        }
    }
}
