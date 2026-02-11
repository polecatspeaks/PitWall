using Microsoft.AspNetCore.Mvc;
using PitWall.Api.Models;
using PitWall.Api.Services;
using PitWall.Core.Services;
using PitWall.Core.Storage;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.IO;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("System", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "logs", "pitwall-api-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        shared: true)
    .CreateLogger();

builder.Host.UseSerilog();

// Get database path from configuration or environment
var dbPath = ResolveTelemetryDbPath();

// Register telemetry reader if database exists FIRST
if (File.Exists(dbPath))
{
    builder.Services.AddSingleton<ILmuTelemetryReader>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<LmuTelemetryReader>>();
        return new LmuTelemetryReader(dbPath, fallbackSessionCount: 229, logger);
    });
    builder.Services.AddSingleton<IDuckDbConnector>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<DuckDbConnector>>();
        return new DuckDbConnector(dbPath, logger);
    });
    builder.Services.AddScoped<ITelemetryWriter, DuckDbTelemetryWriter>();
    builder.Services.AddSingleton<ISessionMetadataStore>(sp =>
        new DuckDbSessionMetadataStore(sp.GetRequiredService<IDuckDbConnector>(), sp.GetRequiredService<ILogger<DuckDbSessionMetadataStore>>()));
    builder.Services.AddSingleton<ISessionSummaryService, SessionSummaryService>();
}
else
{
    builder.Services.AddSingleton<ILmuTelemetryReader, NullLmuTelemetryReader>();
    // Fallback to in-memory writer if database not found
    builder.Services.AddScoped<ITelemetryWriter, InMemoryTelemetryWriter>();
    builder.Services.AddSingleton<ISessionMetadataStore, NullSessionMetadataStore>();
    builder.Services.AddSingleton<ISessionSummaryService, NullSessionSummaryService>();
}

// Then register services that depend on the reader
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<ISessionService, SessionService>();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.Logger.LogInformation("PitWall API starting. Telemetry DB path: {DbPath}", dbPath);

app.UseWebSockets();

app.MapGet("/", () => "PitWall.LMU API - Real-time race engineering");

// GET /api/recommend?sessionId={sessionId}
app.MapGet("/api/recommend", (string sessionId, IRecommendationService service, ITelemetryWriter writer, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(sessionId))
        return Results.BadRequest(new { error = "sessionId query parameter is required" });

    logger.LogDebug("Recommendation requested for session {SessionId}", sessionId);

    var response = service.GetRecommendation(sessionId, writer);
    return Results.Ok(response);
})
.WithName("GetRecommendation")
.WithDescription("Get a recommendation for a specific session");

// GET /api/sessions/count
app.MapGet("/api/sessions/count", async (ISessionService sessionService, ILogger<Program> logger) =>
{
    var count = await sessionService.GetTotalSessionCountAsync();
    logger.LogDebug("Session count returned: {SessionCount}", count);
    return Results.Ok(new { sessionCount = count });
})
.WithName("GetSessionCount")
.WithDescription("Get total number of imported sessions");

// GET /api/sessions/summary
app.MapGet("/api/sessions/summary", async (ISessionSummaryService summaryService, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    var sessions = await summaryService.GetSessionSummariesAsync(cancellationToken);
    logger.LogDebug("Session summaries returned: {SessionCount}", sessions.Count);
    return Results.Ok(new { sessions });
})
.WithName("GetSessionSummaries")
.WithDescription("Get session summaries with timestamps and metadata");

// PUT /api/sessions/{sessionId}/metadata
app.MapPut("/api/sessions/{sessionId}/metadata", async (int sessionId, SessionMetadataUpdate update, ISessionMetadataStore store, ISessionSummaryService summaryService, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    var existing = await store.GetAsync(sessionId, cancellationToken) ?? new SessionMetadata();
    var updated = new SessionMetadata
    {
        Track = string.IsNullOrWhiteSpace(update.Track) ? existing.Track : update.Track.Trim(),
        Car = string.IsNullOrWhiteSpace(update.Car) ? existing.Car : update.Car.Trim()
    };

    await store.SetAsync(sessionId, updated, cancellationToken);

    var summary = await summaryService.GetSessionSummaryAsync(sessionId, cancellationToken);
    logger.LogDebug("Session metadata updated for {SessionId}.", sessionId);

    if (summary == null)
        return Results.Ok(new { sessionId, track = updated.Track, car = updated.Car });

    return Results.Ok(summary);
})
.WithName("UpdateSessionMetadata")
.WithDescription("Update track and car metadata for a session");

// GET /api/sessions/channels
app.MapGet("/api/sessions/channels", async (ISessionService sessionService, ILogger<Program> logger) =>
{
    var channels = await sessionService.GetAvailableChannelsAsync();
    logger.LogDebug("Channel list returned: {ChannelCount}", channels.Count);
    return Results.Ok(new { channels });
})
.WithName("GetChannels")
.WithDescription("Get available telemetry channels");

// GET /api/sessions/{sessionId}/samples?startRow={startRow}&endRow={endRow}
app.MapGet("/api/sessions/{sessionId}/samples", async (int sessionId, int startRow, int endRow, [FromServices] ILmuTelemetryReader reader, ILogger<Program> logger) =>
{
    if (startRow < 0 || (endRow >= 0 && endRow < startRow))
        return Results.BadRequest(new { error = "Invalid row range" });

    logger.LogDebug("Samples requested. Session {SessionId}, start {StartRow}, end {EndRow}", sessionId, startRow, endRow);

    var samples = new List<object>();
    try
    {
        await foreach (var sample in reader.ReadSamplesAsync(sessionId, startRow, endRow))
        {
            samples.Add(new
            {
                timestamp = sample.Timestamp,
                speedKph = sample.SpeedKph,
                throttle = sample.Throttle,
                brake = sample.Brake,
                steering = sample.Steering,
                tyreTemps = sample.TyreTempsC,
                    fuelLiters = sample.FuelLiters,
                    lapNumber = sample.LapNumber,
                    latitude = sample.Latitude,
                    longitude = sample.Longitude,
                    lateralG = sample.LateralG
            });

            // Limit response size
            if (samples.Count >= 1000)
                break;
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to read samples for session {SessionId}", sessionId);
        return Results.BadRequest(new { error = ex.Message });
    }

    return Results.Ok(new { sessionId, sampleCount = samples.Count, samples });
})
.WithName("GetSessionSamples")
.WithDescription("Get telemetry samples for a session");

app.Map("/ws/state", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var sessionIdRaw = context.Request.Query["sessionId"].ToString();
    if (!int.TryParse(sessionIdRaw, out var sessionId))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var startRow = 0;
    if (int.TryParse(context.Request.Query["startRow"].ToString(), out var startRowValue))
        startRow = startRowValue;

    var endRow = -1;
    if (int.TryParse(context.Request.Query["endRow"].ToString(), out var endRowValue))
        endRow = endRowValue;

    var intervalMs = 100;
    if (int.TryParse(context.Request.Query["intervalMs"].ToString(), out var intervalValue))
        intervalMs = Math.Max(0, intervalValue);

    var reader = context.RequestServices.GetRequiredService<ILmuTelemetryReader>();
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    using var socket = await context.WebSockets.AcceptWebSocketAsync();

    logger.LogInformation("WebSocket connected. Session {SessionId}, start {StartRow}, end {EndRow}, interval {IntervalMs}", sessionId, startRow, endRow, intervalMs);

    try
    {
        var sampleCount = 0;
        await foreach (var sample in reader.ReadSamplesAsync(sessionId, startRow, endRow, context.RequestAborted))
        {
            if (socket.State != WebSocketState.Open)
                break;

            var payload = JsonSerializer.Serialize(new
            {
                timestamp = sample.Timestamp,
                speedKph = sample.SpeedKph,
                throttle = sample.Throttle,
                brake = sample.Brake,
                steering = sample.Steering,
                tyreTemps = sample.TyreTempsC,
                fuelLiters = sample.FuelLiters,
                lapNumber = sample.LapNumber,
                latitude = sample.Latitude,
                longitude = sample.Longitude,
                lateralG = sample.LateralG
            });

            var bytes = Encoding.UTF8.GetBytes(payload);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, context.RequestAborted);
            sampleCount++;

            if (intervalMs > 0)
                await Task.Delay(intervalMs, context.RequestAborted);
        }
        
        // Send completion message
        if (socket.State == WebSocketState.Open)
        {
            var completionPayload = JsonSerializer.Serialize(new { type = "complete", sampleCount });
            var completionBytes = Encoding.UTF8.GetBytes(completionPayload);
            await socket.SendAsync(completionBytes, WebSocketMessageType.Text, true, context.RequestAborted);
            logger.LogInformation("WebSocket replay completed. Session {SessionId}, sent {SampleCount} samples", sessionId, sampleCount);
        }

        // Close socket gracefully
        if (socket.State == WebSocketState.Open)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Replay complete", CancellationToken.None);
        }
    }
    catch (OperationCanceledException)
    {
        // Client disconnected or request aborted.
        logger.LogInformation("WebSocket cancelled. Session {SessionId}", sessionId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "WebSocket streaming error. Session {SessionId}", sessionId);
    }
});

app.Run();

static string ResolveTelemetryDbPath()
{
    var envPath = Environment.GetEnvironmentVariable("LMU_TELEMETRY_DB");
    if (!string.IsNullOrWhiteSpace(envPath))
    {
        return Path.GetFullPath(envPath);
    }

    var baseDir = AppContext.BaseDirectory;
    var discovered = FindInParents(baseDir, "lmu_telemetry.db", maxDepth: 6);
    if (!string.IsNullOrWhiteSpace(discovered))
    {
        return discovered;
    }

    var fallback = Path.Combine(baseDir, "..\\..\\..\\lmu_telemetry.db");
    return Path.GetFullPath(fallback);
}

static string? FindInParents(string baseDir, string fileName, int maxDepth)
{
    var current = new DirectoryInfo(baseDir);
    for (var depth = 0; depth <= maxDepth && current != null; depth++)
    {
        var candidate = Path.Combine(current.FullName, fileName);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        current = current.Parent;
    }

    return null;
}

public partial class Program
{
}