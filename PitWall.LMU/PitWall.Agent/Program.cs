using PitWall.Agent.Models;
using PitWall.Agent.Services;
using PitWall.Agent.Services.LLM;
using PitWall.Agent.Services.RulesEngine;
using PitWall.Strategy;

var builder = WebApplication.CreateBuilder(args);

var agentOptions = builder.Configuration
	.GetSection(AgentOptions.SectionName)
	.Get<AgentOptions>() ?? new AgentOptions();

builder.Services.AddSingleton(agentOptions);
builder.Services.AddSingleton<IRulesEngine, RulesEngine>();
builder.Services.AddSingleton<StrategyEngine>();

if (agentOptions.EnableLLM)
{
	builder.Services.AddHttpClient<ILLMService, OllamaLLMService>();
}

builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddLogging(logging =>
{
	logging.AddConsole();
	logging.SetMinimumLevel(LogLevel.Information);
});

var app = builder.Build();

app.MapGet("/", () => "PitWall Agent Service Running");

app.MapPost("/agent/query", async (AgentRequest request, IAgentService agentService) =>
{
	if (string.IsNullOrWhiteSpace(request.Query))
		return Results.BadRequest(new { error = "query is required" });

	var response = await agentService.ProcessQueryAsync(request);
	return Results.Ok(response);
});

app.Run();
