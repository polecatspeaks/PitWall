using Microsoft.AspNetCore.Mvc;
using PitWall.Api.Services;
using PitWall.Core.Services;
using PitWall.Core.Storage;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Get database path from configuration or environment
var dbPath = Environment.GetEnvironmentVariable("LMU_TELEMETRY_DB")
    ?? Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\lmu_telemetry.db");

// Make path absolute if relative
if (!Path.IsPathRooted(dbPath))
    dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);

// Register telemetry reader if database exists FIRST
if (File.Exists(dbPath))
{
    var lmuReader = new LmuTelemetryReader(dbPath);
    builder.Services.AddScoped<ILmuTelemetryReader>(_ => lmuReader);
    builder.Services.AddScoped<ITelemetryWriter>(sp =>
        new DuckDbTelemetryWriter(null, sp.GetRequiredService<ILmuTelemetryReader>()));
}
else
{
    // Fallback to in-memory writer if database not found
    builder.Services.AddScoped<ITelemetryWriter, InMemoryTelemetryWriter>();
}

// Then register services that depend on the reader
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<ISessionService, SessionService>();

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
.WithName("GetRecommendation")
.WithDescription("Get a recommendation for a specific session");

// GET /api/sessions/count
app.MapGet("/api/sessions/count", async (ISessionService sessionService) =>
{
    var count = await sessionService.GetTotalSessionCountAsync();
    return Results.Ok(new { sessionCount = count });
})
.WithName("GetSessionCount")
.WithDescription("Get total number of imported sessions");

// GET /api/sessions/channels
app.MapGet("/api/sessions/channels", async (ISessionService sessionService) =>
{
    var channels = await sessionService.GetAvailableChannelsAsync();
    return Results.Ok(new { channels });
})
.WithName("GetChannels")
.WithDescription("Get available telemetry channels");

// GET /api/sessions/{sessionId}/samples?startRow={startRow}&endRow={endRow}
app.MapGet("/api/sessions/{sessionId}/samples", async (int sessionId, int startRow, int endRow, [FromServices] ILmuTelemetryReader reader) =>
{
    if (startRow < 0 || (endRow >= 0 && endRow < startRow))
        return Results.BadRequest(new { error = "Invalid row range" });

    var samples = new List<object>();
    try
    {
        await foreach (var sample in reader.ReadSamplesAsync(startRow, endRow))
        {
            samples.Add(new
            {
                timestamp = sample.Timestamp,
                speedKph = sample.SpeedKph,
                throttle = sample.Throttle,
                brake = sample.Brake,
                steering = sample.Steering,
                tyreTemps = sample.TyreTempsC,
                fuelLiters = sample.FuelLiters
            });

            // Limit response size
            if (samples.Count >= 1000)
                break;
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }

    return Results.Ok(new { sessionId, sampleCount = samples.Count, samples });
})
.WithName("GetSessionSamples")
.WithDescription("Get telemetry samples for a session");

app.Run();