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
        private readonly IRaceContextProvider _contextProvider;
        private readonly AgentOptions _options;
        private readonly ILogger<AgentService> _logger;

        public AgentService(
            IRulesEngine rulesEngine,
            StrategyEngine strategyEngine,
            IRaceContextProvider contextProvider,
            AgentOptions options,
            ILogger<AgentService> logger,
            ILLMService? llmService = null)
        {
            _rulesEngine = rulesEngine;
            _contextProvider = contextProvider;
            _options = options;
            _logger = logger;
            _llmService = llmService;
        }

        public async Task<AgentResponse> ProcessQueryAsync(AgentRequest request)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var context = await _contextProvider.BuildAsync(request);

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
                    if (_options.RequirePitForLlm && context.IsActivelyRacing)
                    {
                        _logger.LogInformation(
                            "LLM suppressed while racing: {Query}",
                            request.Query);

                        return new AgentResponse
                        {
                            Answer = "LLM disabled while racing. Ask again in pit lane.",
                            Source = "Safety",
                            Confidence = 0.0,
                            ResponseTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                            Success = false
                        };
                    }

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

        private RaceContext BuildRaceContext(AgentRequest request) => _contextProvider.BuildAsync(request).GetAwaiter().GetResult();
    }
}
