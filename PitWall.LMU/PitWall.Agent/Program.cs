using Microsoft.AspNetCore.Mvc;
using PitWall.Agent.Models;
using PitWall.Agent.Services;
using PitWall.Agent.Services.LLM;
using PitWall.Agent.Services.RulesEngine;
using PitWall.Core.Storage;
using PitWall.Strategy;

namespace PitWall.Agent
{
	public partial class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			var agentOptions = builder.Configuration
				.GetSection(AgentOptions.SectionName)
				.Get<AgentOptions>() ?? new AgentOptions();

			builder.Services.AddSingleton(agentOptions);
			builder.Services.AddSingleton<IRulesEngine, RulesEngine>();
			builder.Services.AddSingleton<StrategyEngine>();
			builder.Services.AddSingleton<ITelemetryWriter, InMemoryTelemetryWriter>();
			builder.Services.AddSingleton<IRaceContextProvider, RaceContextProvider>();
			builder.Services.AddSingleton<ILlmEndpointEnumerator, LocalSubnetEndpointEnumerator>();
			builder.Services.AddHttpClient<ILLMDiscoveryService, OllamaDiscoveryService>();

			if (agentOptions.EnableLLM)
			{
				switch (agentOptions.LLMProvider.ToLowerInvariant())
				{
					case "openai":
						builder.Services.AddHttpClient<ILLMService, OpenAiLlmService>();
						break;
					case "anthropic":
						builder.Services.AddHttpClient<ILLMService, AnthropicLlmService>();
						break;
					default:
						builder.Services.AddHttpClient<ILLMService, OllamaLLMService>();
						break;
				}
			}

			builder.Services.AddScoped<IAgentService, AgentService>();
			builder.Services.AddLogging(logging =>
			{
				logging.AddConsole();
				logging.SetMinimumLevel(LogLevel.Information);
			});

			var app = builder.Build();

			app.MapGet("/", () => "PitWall Agent Service Running");

			app.MapGet("/agent/health", ([FromServices] AgentOptions options, [FromServices] ILLMService? llmService = null) =>
			{
				return Results.Ok(new
				{
					llmEnabled = options.EnableLLM,
					llmAvailable = llmService?.IsAvailable ?? false,
					provider = options.LLMProvider,
					model = options.LLMModel,
					endpoint = options.LLMEndpoint
				});
			});

			app.MapPost("/agent/query", async (AgentRequest request, IAgentService agentService) =>
			{
				if (string.IsNullOrWhiteSpace(request.Query))
					return Results.BadRequest(new { error = "query is required" });

				var response = await agentService.ProcessQueryAsync(request);
				return Results.Ok(response);
			});

			app.MapGet("/agent/llm/test", async ([FromServices] AgentOptions options, [FromServices] ILLMService? llmService = null) =>
			{
				var enabled = llmService?.IsEnabled ?? options.EnableLLM;

				if (!enabled || llmService == null)
				{
					return Results.Ok(new { llmEnabled = enabled, available = false });
				}

				var available = await llmService.TestConnectionAsync();
				return Results.Ok(new { llmEnabled = enabled, available });
			});

			app.MapGet("/agent/llm/discover", async (ILLMDiscoveryService discoveryService) =>
			{
				var endpoints = await discoveryService.DiscoverAsync();
				return Results.Ok(new { endpoints });
			});

			app.Run();
		}
	}
}
