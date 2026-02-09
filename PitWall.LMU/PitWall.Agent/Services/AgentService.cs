using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PitWall.Agent.Models;
using PitWall.Agent.Services.LLM;
using PitWall.Agent.Services.RulesEngine;
using PitWall.Strategy;

namespace PitWall.Agent.Services
{
    public class AgentService : IAgentService
    {
        private readonly IRulesEngine _rulesEngine;
        private readonly ILLMService? _llmService;
        private readonly StrategyEngine _strategyEngine;
        private readonly ILogger<AgentService> _logger;

        public AgentService(
            IRulesEngine rulesEngine,
            StrategyEngine strategyEngine,
            ILogger<AgentService> logger,
            ILLMService? llmService = null)
        {
            _rulesEngine = rulesEngine;
            _strategyEngine = strategyEngine;
            _logger = logger;
            _llmService = llmService;
        }

        public async Task<AgentResponse> ProcessQueryAsync(AgentRequest request)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var context = BuildRaceContext(request);

                var rulesResponse = _rulesEngine.TryAnswer(request.Query, context);
                if (rulesResponse != null)
                {
                    _logger.LogInformation(
                        "Query answered by rules engine in {Ms}ms: {Query}",
                        rulesResponse.ResponseTimeMs,
                        request.Query);

                    return rulesResponse;
                }

                if (_llmService?.IsEnabled == true)
                {
                    _logger.LogInformation(
                        "Rules engine unable to answer, querying LLM: {Query}",
                        request.Query);

                    var llmResponse = await _llmService.QueryAsync(request.Query, context);
                    if (llmResponse.Success)
                    {
                        _logger.LogInformation(
                            "Query answered by LLM in {Ms}ms",
                            llmResponse.ResponseTimeMs);

                        return llmResponse;
                    }
                }

                _logger.LogWarning(
                    "Unable to answer query (no LLM available): {Query}",
                    request.Query);

                return new AgentResponse
                {
                    Answer = "I don't have enough information for that question. Try asking about fuel, tires, pit strategy, or gaps.",
                    Source = "Fallback",
                    Confidence = 0.0,
                    ResponseTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    Success = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing query: {Query}", request.Query);

                return new AgentResponse
                {
                    Answer = "An error occurred while processing your question.",
                    Source = "Error",
                    Confidence = 0.0,
                    ResponseTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        private RaceContext BuildRaceContext(AgentRequest request)
        {
            _ = _strategyEngine;

            return new RaceContext
            {
                TrackName = "Le Mans",
                CarName = "Toyota GR010",
                CurrentLap = 15,
                TotalLaps = 30,
                Position = 3,
                FuelLevel = 45.0,
                FuelCapacity = 90.0,
                FuelLapsRemaining = 14.2,
                AvgFuelPerLap = 3.2,
                OptimalPitLap = 18,
                StrategyConfidence = 0.85,
                AverageTireWear = 68.0,
                TireLapsOnSet = 15,
                LastLapTime = 221.5,
                BestLapTime = 218.3,
                GapToAhead = 2.3,
                GapToBehind = 4.1,
                CurrentWeather = "Clear",
                TrackTemp = 32.0,
                InPitLane = false
            };
        }
    }
}
