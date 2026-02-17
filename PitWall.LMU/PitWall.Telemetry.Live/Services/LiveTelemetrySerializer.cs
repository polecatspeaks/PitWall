using System;
using System.Text.Json;
using PitWall.Telemetry.Live.Models;

namespace PitWall.Telemetry.Live.Services
{
    /// <summary>
    /// Serializes telemetry snapshots and control messages into JSON
    /// for WebSocket broadcast to connected clients.
    /// Wire format is designed to be compatible with the existing replay format
    /// while adding live-specific metadata.
    /// </summary>
    public static class LiveTelemetrySerializer
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Serialize a telemetry snapshot into the live broadcast wire format.
        /// Extracts player vehicle data and session metadata into a flat JSON object.
        /// </summary>
        /// <param name="snapshot">Telemetry snapshot to serialize</param>
        /// <returns>JSON string ready for WebSocket send</returns>
        public static string SerializeSnapshot(TelemetrySnapshot snapshot)
        {
            var player = snapshot?.PlayerVehicle;
            var session = snapshot?.Session;

            return JsonSerializer.Serialize(new
            {
                type = "telemetry",
                mode = "live",
                timestamp = snapshot?.Timestamp ?? DateTime.MinValue,
                sessionId = snapshot?.SessionId ?? string.Empty,
                speedKph = player?.Speed ?? 0.0,
                throttle = player?.Throttle ?? 0.0,
                brake = player?.Brake ?? 0.0,
                steering = player?.Steering ?? 0.0,
                fuelLiters = player?.Fuel ?? 0.0,
                tyreTemps = player != null
                    ? new[] { player.Wheels[0].TempMid, player.Wheels[1].TempMid, player.Wheels[2].TempMid, player.Wheels[3].TempMid }
                    : new double[] { 0, 0, 0, 0 },
                latitude = player?.PosX ?? 0.0,
                longitude = player?.PosZ ?? 0.0,
                track = session?.TrackName ?? string.Empty,
                sessionType = session?.SessionType ?? string.Empty,
                numVehicles = session?.NumVehicles ?? 0
            }, JsonOptions);
        }

        /// <summary>
        /// Serialize the initial metadata message sent when a WebSocket connection opens.
        /// </summary>
        /// <param name="sessionId">Current live session ID</param>
        /// <returns>JSON string for the meta message</returns>
        public static string SerializeMetaMessage(string sessionId)
        {
            return JsonSerializer.Serialize(new
            {
                type = "meta",
                mode = "live",
                sessionId
            }, JsonOptions);
        }

        /// <summary>
        /// Serialize an error message indicating live telemetry is unavailable.
        /// Sent when shared memory is not connected (game not running).
        /// </summary>
        /// <returns>JSON string for the error message</returns>
        public static string SerializeUnavailableMessage()
        {
            return JsonSerializer.Serialize(new
            {
                type = "error",
                message = "Live telemetry unavailable. Ensure LMU is running with shared memory enabled."
            }, JsonOptions);
        }

        /// <summary>
        /// Serialize a snapshot to UTF-8 bytes, ready for WebSocket SendAsync.
        /// </summary>
        public static byte[] SerializeSnapshotBytes(TelemetrySnapshot snapshot)
        {
            return System.Text.Encoding.UTF8.GetBytes(SerializeSnapshot(snapshot));
        }

        /// <summary>
        /// Serialize a meta message to UTF-8 bytes.
        /// </summary>
        public static byte[] SerializeMetaMessageBytes(string sessionId)
        {
            return System.Text.Encoding.UTF8.GetBytes(SerializeMetaMessage(sessionId));
        }

        /// <summary>
        /// Serialize an unavailable message to UTF-8 bytes.
        /// </summary>
        public static byte[] SerializeUnavailableMessageBytes()
        {
            return System.Text.Encoding.UTF8.GetBytes(SerializeUnavailableMessage());
        }
    }
}
