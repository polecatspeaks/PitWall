using Microsoft.AspNetCore.Mvc;
using PitWall.Api.Models;
using PitWall.Api.Services;
using PitWall.Core.Services;
using PitWall.Core.Storage;
using PitWall.Telemetry.Live.Services;
using PitWall.Telemetry.Live.Models;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.IO;
using Serilog;
using Serilog.Events;

// Alias to resolve naming conflict between Core.Storage and Telemetry.Live.Services
using CoreTelemetryWriter = PitWall.Core.Storage.ITelemetryWriter;
using CoreDuckDbWriter = PitWall.Core.Storage.DuckDbTelemetryWriter;

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
    builder.Services.AddScoped<CoreTelemetryWriter, CoreDuckDbWriter>();
    builder.Services.AddSingleton<ISessionMetadataStore>(sp =>
        new DuckDbSessionMetadataStore(sp.GetRequiredService<IDuckDbConnector>(), sp.GetRequiredService<ILogger<DuckDbSessionMetadataStore>>()));
    builder.Services.AddSingleton<ISessionSummaryService, SessionSummaryService>();
}
else
{
    builder.Services.AddSingleton<ILmuTelemetryReader, NullLmuTelemetryReader>();
    // Fallback to in-memory writer if database not found
    builder.Services.AddScoped<CoreTelemetryWriter, InMemoryTelemetryWriter>();
    builder.Services.AddSingleton<ISessionMetadataStore, NullSessionMetadataStore>();
    builder.Services.AddSingleton<ISessionSummaryService, NullSessionSummaryService>();
}

// Then register services that depend on the reader
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<ISessionService, SessionService>();

// Register live telemetry pipeline services
builder.Services.AddSingleton<ISharedMemoryReader, SharedMemoryReader>();
builder.Services.AddSingleton<ITelemetryDataSource>(sp =>
    new SharedMemoryDataSource(
        sp.GetRequiredService<ISharedMemoryReader>(),
        sp.GetService<ILogger<SharedMemoryDataSource>>()));
builder.Services.AddSingleton<TelemetryPipelineService>(sp =>
    new TelemetryPipelineService(
        sp.GetRequiredService<ITelemetryDataSource>(),
        writer: null, // Persistence wired separately via #18 DuckDbTelemetryWriter
        sp.GetService<ILogger<TelemetryPipelineService>>()));

var app = builder.Build();

app.UseSerilogRequestLogging();

app.Logger.LogInformation("PitWall API starting. Telemetry DB path: {DbPath}", dbPath);

app.UseWebSockets();

app.MapGet("/", () => "PitWall.LMU API - Real-time race engineering");

// GET /api/recommend?sessionId={sessionId}
app.MapGet("/api/recommend", (string sessionId, IRecommendationService service, CoreTelemetryWriter writer, ILogger<Program> logger) =>
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
        TrackId = string.IsNullOrWhiteSpace(update.TrackId) ? existing.TrackId : update.TrackId.Trim(),
        Car = string.IsNullOrWhiteSpace(update.Car) ? existing.Car : update.Car.Trim()
    };

    await store.SetAsync(sessionId, updated, cancellationToken);

    var summary = await summaryService.GetSessionSummaryAsync(sessionId, cancellationToken);
    logger.LogDebug("Session metadata updated for {SessionId}.", sessionId);

    if (summary == null)
        return Results.Ok(new { sessionId, track = updated.Track, trackId = updated.TrackId, car = updated.Car });

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

    return Results.Ok(new { sessionId, timebase = "gps_time", sampleCount = samples.Count, samples });
})
.WithName("GetSessionSamples")
.WithDescription("Get telemetry samples for a session");

app.Map("/ws/live", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var pipeline = context.RequestServices.GetRequiredService<TelemetryPipelineService>();
    var dataSource = context.RequestServices.GetRequiredService<ITelemetryDataSource>();
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    using var socket = await context.WebSockets.AcceptWebSocketAsync();

    logger.LogInformation("Live WebSocket connected");

    try
    {
        // Check if live telemetry source is available
        if (!dataSource.IsAvailable())
        {
            var errorBytes = LiveTelemetrySerializer.SerializeUnavailableMessageBytes();
            await socket.SendAsync(errorBytes, WebSocketMessageType.Text, true, context.RequestAborted);
            logger.LogWarning("Live telemetry unavailable — shared memory not connected");

            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Source unavailable", CancellationToken.None);
            }
            return;
        }

        // Send initial meta message
        var firstSnapshot = await dataSource.ReadSnapshotAsync();
        var sessionId = firstSnapshot?.SessionId ?? "unknown";
        var metaBytes = LiveTelemetrySerializer.SerializeMetaMessageBytes(sessionId);
        await socket.SendAsync(metaBytes, WebSocketMessageType.Text, true, context.RequestAborted);

        // Stream live telemetry
        await foreach (var snapshot in pipeline.StreamForBroadcastAsync(context.RequestAborted))
        {
            if (socket.State != WebSocketState.Open)
                break;

            var bytes = LiveTelemetrySerializer.SerializeSnapshotBytes(snapshot);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, context.RequestAborted);
        }

        // Close gracefully
        if (socket.State == WebSocketState.Open)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Stream ended", CancellationToken.None);
        }
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Live WebSocket cancelled");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Live WebSocket streaming error");
    }
});

app.Map("/ws/state", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var dataSource = context.RequestServices.GetRequiredService<ITelemetryDataSource>();
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    // Auto-detect mode: if live telemetry source is available, stream live data
    if (dataSource.IsAvailable())
    {
        var pipeline = context.RequestServices.GetRequiredService<TelemetryPipelineService>();
        using var socket = await context.WebSockets.AcceptWebSocketAsync();

        logger.LogInformation("/ws/state: Live telemetry available — switching to live mode");

        try
        {
            // Send initial meta message indicating live mode
            var firstSnapshot = await dataSource.ReadSnapshotAsync();
            var liveSessionId = firstSnapshot?.SessionId ?? "unknown";
            var metaBytes = LiveTelemetrySerializer.SerializeMetaMessageBytes(liveSessionId);
            await socket.SendAsync(metaBytes, WebSocketMessageType.Text, true, context.RequestAborted);

            // Stream live telemetry via pipeline
            await foreach (var snapshot in pipeline.StreamForBroadcastAsync(context.RequestAborted))
            {
                if (socket.State != WebSocketState.Open)
                    break;

                var bytes = LiveTelemetrySerializer.SerializeSnapshotBytes(snapshot);
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, context.RequestAborted);
            }

            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Live stream ended", CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("/ws/state: Live WebSocket cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "/ws/state: Live WebSocket streaming error");
        }
        return;
    }

    // Fallback: live not available — try replay mode with sessionId
    var sessionIdRaw = context.Request.Query["sessionId"].ToString();
    if (!int.TryParse(sessionIdRaw, out var sessionId))
    {
        // No live source and no valid sessionId — send error via WebSocket
        using var errorSocket = await context.WebSockets.AcceptWebSocketAsync();
        var errorBytes = LiveTelemetrySerializer.SerializeUnavailableMessageBytes();
        await errorSocket.SendAsync(errorBytes, WebSocketMessageType.Text, true, context.RequestAborted);
        logger.LogWarning("/ws/state: No live source and no sessionId — sent error to client");
        if (errorSocket.State == WebSocketState.Open)
        {
            await errorSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "No source available", CancellationToken.None);
        }
        return;
    }

    logger.LogInformation("/ws/state: Live unavailable — falling back to replay mode for session {SessionId}", sessionId);

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
    using var socket2 = await context.WebSockets.AcceptWebSocketAsync();

    logger.LogInformation("WebSocket replay connected. Session {SessionId}, start {StartRow}, end {EndRow}, interval {IntervalMs}", sessionId, startRow, endRow, intervalMs);

    try
    {
        var metadataPayload = JsonSerializer.Serialize(new
        {
            type = "meta",
            timebase = "gps_time",
            sessionId,
            startRow,
            endRow
        });
        var metadataBytes = Encoding.UTF8.GetBytes(metadataPayload);
        await socket2.SendAsync(metadataBytes, WebSocketMessageType.Text, true, context.RequestAborted);

        var sampleCount = 0;
        await foreach (var sample in reader.ReadSamplesAsync(sessionId, startRow, endRow, context.RequestAborted))
        {
            if (socket2.State != WebSocketState.Open)
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
            await socket2.SendAsync(bytes, WebSocketMessageType.Text, true, context.RequestAborted);
            sampleCount++;

            if (intervalMs > 0)
                await Task.Delay(intervalMs, context.RequestAborted);
        }
        
        // Send completion message
        if (socket2.State == WebSocketState.Open)
        {
            var completionPayload = JsonSerializer.Serialize(new { type = "complete", sampleCount });
            var completionBytes = Encoding.UTF8.GetBytes(completionPayload);
            await socket2.SendAsync(completionBytes, WebSocketMessageType.Text, true, context.RequestAborted);
            logger.LogInformation("WebSocket replay completed. Session {SessionId}, sent {SampleCount} samples", sessionId, sampleCount);
        }

        // Close socket gracefully
        if (socket2.State == WebSocketState.Open)
        {
            await socket2.CloseAsync(WebSocketCloseStatus.NormalClosure, "Replay complete", CancellationToken.None);
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