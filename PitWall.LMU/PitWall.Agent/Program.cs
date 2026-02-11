using System.IO;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using PitWall.Agent.Models;
using PitWall.Agent.Services;
using PitWall.Agent.Services.LLM;
using PitWall.Agent.Services.RulesEngine;
using PitWall.Core.Storage;
using PitWall.Strategy;
using Serilog;
using Serilog.Events;

namespace PitWall.Agent
{
	public partial class Program
	{
		public static void Main(string[] args)
		{
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Debug()
				.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
				.MinimumLevel.Override("System", LogEventLevel.Information)
				.Enrich.FromLogContext()
				.WriteTo.Console()
				.WriteTo.File(
					Path.Combine(AppContext.BaseDirectory, "logs", "pitwall-agent-.log"),
					rollingInterval: RollingInterval.Day,
					retainedFileCountLimit: 7,
					shared: true)
				.CreateLogger();

			var builder = WebApplication.CreateBuilder(args);
			builder.Host.UseSerilog();
			if (!builder.Environment.IsEnvironment("Test"))
				builder.Configuration.AddJsonFile("appsettings.Agent.user.json", optional: true, reloadOnChange: true);

			var agentOptions = builder.Configuration
				.GetSection(AgentOptions.SectionName)
				.Get<AgentOptions>() ?? new AgentOptions();

			builder.Services.AddSingleton(agentOptions);
			var userConfigPath = Path.Combine(builder.Environment.ContentRootPath, "appsettings.Agent.user.json");
			builder.Services.AddSingleton<IAgentOptionsStore>(new JsonAgentOptionsStore(userConfigPath));
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
			var app = builder.Build();
			app.UseSerilogRequestLogging();

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

			app.MapGet("/agent/config", ([FromServices] AgentOptions options, ILogger<Program> logger) =>
			{
				logger.LogDebug("Agent config requested.");
				return Results.Ok(new AgentConfigResponse
				{
					EnableLLM = options.EnableLLM,
					LLMProvider = options.LLMProvider,
					LLMEndpoint = options.LLMEndpoint,
					LLMModel = options.LLMModel,
					LLMTimeoutMs = options.LLMTimeoutMs,

					RequirePitForLlm = options.RequirePitForLlm,

					EnableLLMDiscovery = options.EnableLLMDiscovery,
					LLMDiscoveryTimeoutMs = options.LLMDiscoveryTimeoutMs,
					LLMDiscoveryPort = options.LLMDiscoveryPort,
					LLMDiscoveryMaxConcurrency = options.LLMDiscoveryMaxConcurrency,
					LLMDiscoverySubnetPrefix = options.LLMDiscoverySubnetPrefix,

					OpenAIEndpoint = options.OpenAIEndpoint,
					OpenAIModel = options.OpenAIModel,
					OpenAiApiKeyConfigured = !string.IsNullOrWhiteSpace(options.OpenAIApiKey),

					AnthropicEndpoint = options.AnthropicEndpoint,
					AnthropicModel = options.AnthropicModel,
					AnthropicApiKeyConfigured = !string.IsNullOrWhiteSpace(options.AnthropicApiKey)
				});
			});

			app.MapPut("/agent/config", async ([FromServices] AgentOptions options, [FromServices] IAgentOptionsStore optionsStore, [FromBody] AgentConfigUpdate update, CancellationToken cancellationToken, ILogger<Program> logger) =>
			{
				static string? ReadExtra(AgentConfigUpdate payload, params string[] names)
				{
					if (payload.Extra == null)
						return null;

					foreach (var name in names)
					{
						if (payload.Extra.TryGetValue(name, out var value)
							&& value.ValueKind == System.Text.Json.JsonValueKind.String)
						{
							return value.GetString();
						}
					}

					return null;
				}

				if (update.EnableLLM.HasValue)
					options.EnableLLM = update.EnableLLM.Value;
				if (!string.IsNullOrWhiteSpace(update.LLMProvider))
					options.LLMProvider = update.LLMProvider;
				if (!string.IsNullOrWhiteSpace(update.LLMEndpoint))
					options.LLMEndpoint = update.LLMEndpoint;
				if (!string.IsNullOrWhiteSpace(update.LLMModel))
					options.LLMModel = update.LLMModel;
				if (update.LLMTimeoutMs.HasValue)
					options.LLMTimeoutMs = update.LLMTimeoutMs.Value;

				var openAiApiKey = update.OpenAiApiKey ?? ReadExtra(update, "openAIApiKey", "openAiApiKey");
				var openAiEndpoint = update.OpenAiEndpoint ?? ReadExtra(update, "openAIEndpoint", "openAiEndpoint");
				var openAiModel = update.OpenAiModel ?? ReadExtra(update, "openAIModel", "openAiModel");

				if (!string.IsNullOrWhiteSpace(openAiApiKey))
					options.OpenAIApiKey = openAiApiKey;
				if (!string.IsNullOrWhiteSpace(openAiEndpoint))
					options.OpenAIEndpoint = openAiEndpoint;
				if (!string.IsNullOrWhiteSpace(openAiModel))
					options.OpenAIModel = openAiModel;

				if (!string.IsNullOrWhiteSpace(update.AnthropicApiKey))
					options.AnthropicApiKey = update.AnthropicApiKey;
				if (!string.IsNullOrWhiteSpace(update.AnthropicEndpoint))
					options.AnthropicEndpoint = update.AnthropicEndpoint;
				if (!string.IsNullOrWhiteSpace(update.AnthropicModel))
					options.AnthropicModel = update.AnthropicModel;

				if (update.RequirePitForLlm.HasValue)
					options.RequirePitForLlm = update.RequirePitForLlm.Value;

				if (update.EnableLLMDiscovery.HasValue)
					options.EnableLLMDiscovery = update.EnableLLMDiscovery.Value;
				if (update.LLMDiscoveryTimeoutMs.HasValue)
					options.LLMDiscoveryTimeoutMs = update.LLMDiscoveryTimeoutMs.Value;
				if (update.LLMDiscoveryPort.HasValue)
					options.LLMDiscoveryPort = update.LLMDiscoveryPort.Value;
				if (update.LLMDiscoveryMaxConcurrency.HasValue)
					options.LLMDiscoveryMaxConcurrency = update.LLMDiscoveryMaxConcurrency.Value;
				if (update.LLMDiscoverySubnetPrefix != null)
					options.LLMDiscoverySubnetPrefix = update.LLMDiscoverySubnetPrefix;

				await optionsStore.SaveAsync(options, cancellationToken);
				logger.LogDebug("Agent config persisted to user settings.");

				return Results.Ok(new AgentConfigResponse
				{
					EnableLLM = options.EnableLLM,
					LLMProvider = options.LLMProvider,
					LLMEndpoint = options.LLMEndpoint,
					LLMModel = options.LLMModel,
					LLMTimeoutMs = options.LLMTimeoutMs,

					RequirePitForLlm = options.RequirePitForLlm,

					EnableLLMDiscovery = options.EnableLLMDiscovery,
					LLMDiscoveryTimeoutMs = options.LLMDiscoveryTimeoutMs,
					LLMDiscoveryPort = options.LLMDiscoveryPort,
					LLMDiscoveryMaxConcurrency = options.LLMDiscoveryMaxConcurrency,
					LLMDiscoverySubnetPrefix = options.LLMDiscoverySubnetPrefix,

					OpenAIEndpoint = options.OpenAIEndpoint,
					OpenAIModel = options.OpenAIModel,
					OpenAiApiKeyConfigured = !string.IsNullOrWhiteSpace(options.OpenAIApiKey),
					AnthropicEndpoint = options.AnthropicEndpoint,
					AnthropicModel = options.AnthropicModel,
					AnthropicApiKeyConfigured = !string.IsNullOrWhiteSpace(options.AnthropicApiKey)
				});
			});

			app.Logger.LogInformation("PitWall Agent started with LLM enabled: {EnableLlm}", agentOptions.EnableLLM);
			app.Run();
		}
	}
}
