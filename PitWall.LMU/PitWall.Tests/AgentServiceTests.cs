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
            var contextProvider = new StubRaceContextProvider(new RaceContext
            {
                FuelLapsRemaining = 2.0,
                AvgFuelPerLap = 1.8,
                StrategyConfidence = 0.8,
                AverageTireWear = 50
            });

            var agent = new AgentService(rulesEngine, strategyEngine, contextProvider, logger);
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
            var contextProvider = new StubRaceContextProvider(new RaceContext
            {
                FuelLapsRemaining = 10.0,
                AvgFuelPerLap = 1.8,
                StrategyConfidence = 0.85,
                AverageTireWear = 60
            });

            var agent = new AgentService(rulesEngine, strategyEngine, contextProvider, logger);
            var request = new AgentRequest
            {
                Query = "Should I pit this lap?",
                Context = new()
            };

            var response = await agent.ProcessQueryAsync(request);

            Assert.True(response.Success);
            Assert.Equal("RulesEngine", response.Source);
            Assert.True(response.Answer.Contains("pit", System.StringComparison.OrdinalIgnoreCase)
                        || response.Answer.Contains("box", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ComplexQuery_FallsBackWhenNoLlm()
        {
            var rulesEngine = new RulesEngine();
            var strategyEngine = new StrategyEngine();
            var logger = NullLogger<AgentService>.Instance;
            var contextProvider = new StubRaceContextProvider(new RaceContext());

            var agent = new AgentService(rulesEngine, strategyEngine, contextProvider, logger, llmService: null);
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

    internal sealed class StubRaceContextProvider : IRaceContextProvider
    {
        private readonly RaceContext _context;

        public StubRaceContextProvider(RaceContext context)
        {
            _context = context;
        }

        public Task<RaceContext> BuildAsync(AgentRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_context);
        }
    }
}
