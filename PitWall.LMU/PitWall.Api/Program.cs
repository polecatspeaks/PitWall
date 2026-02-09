using PitWall.Api.Services;
using PitWall.Core.Storage;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<ITelemetryWriter, InMemoryTelemetryWriter>();

var app = builder.Build();

app.MapGet("/", () => "PitWall.LMU API - Real-time race engineering");

// GET /api/recommend?sessionId={sessionId}
app.MapGet("/api/recommend", (string sessionId, IRecommendationService service, ITelemetryWriter writer) =>
{
    if (string.IsNullOrWhiteSpace(sessionId))
        return Results.BadRequest(new { error = "sessionId query parameter is required" });

    var response = service.GetRecommendation(sessionId, writer);
    return Results.Ok(response);
})
.WithName("GetRecommendation");

app.Run();
