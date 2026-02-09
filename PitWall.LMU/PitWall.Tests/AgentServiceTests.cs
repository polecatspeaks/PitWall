using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Agent.Models;
using PitWall.Agent.Services;
using PitWall.Agent.Services.RulesEngine;
using PitWall.Strategy;
using Xunit;

namespace PitWall.Tests
{
    public class AgentServiceTests
    {
        [Fact]
        public async Task FuelQuery_AnsweredByRulesEngine()
        {
            var rulesEngine = new RulesEngine();
            var strategyEngine = new StrategyEngine();
            var logger = NullLogger<AgentService>.Instance;

            var agent = new AgentService(rulesEngine, strategyEngine, logger);
            var request = new AgentRequest
            {
                Query = "How much fuel do I have?",
                Context = new()
            };

            var response = await agent.ProcessQueryAsync(request);

            Assert.True(response.Success);
            Assert.Equal("RulesEngine", response.Source);
            Assert.Contains("laps", response.Answer.ToLowerInvariant());
            Assert.InRange(response.Confidence, 0.7, 1.0);
            Assert.InRange(response.ResponseTimeMs, 0, 50);
        }

        [Fact]
        public async Task PitQuery_AnsweredByRulesEngine()
        {
            var rulesEngine = new RulesEngine();
            var strategyEngine = new StrategyEngine();
            var logger = NullLogger<AgentService>.Instance;

            var agent = new AgentService(rulesEngine, strategyEngine, logger);
            var request = new AgentRequest
            {
                Query = "Should I pit this lap?",
                Context = new()
            };

            var response = await agent.ProcessQueryAsync(request);

            Assert.True(response.Success);
            Assert.Equal("RulesEngine", response.Source);
            Assert.Contains("pit", response.Answer.ToLowerInvariant());
        }

        [Fact]
        public async Task ComplexQuery_FallsBackWhenNoLlm()
        {
            var rulesEngine = new RulesEngine();
            var strategyEngine = new StrategyEngine();
            var logger = NullLogger<AgentService>.Instance;

            var agent = new AgentService(rulesEngine, strategyEngine, logger, llmService: null);
            var request = new AgentRequest
            {
                Query = "Why am I understeering in the Porsche curves?",
                Context = new()
            };

            var response = await agent.ProcessQueryAsync(request);

            Assert.False(response.Success);
            Assert.Equal("Fallback", response.Source);
            Assert.Contains("don't have enough information", response.Answer.ToLowerInvariant());
        }
    }
}
