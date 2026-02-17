using System;
using System.Collections.Generic;
using System.Text.Json;
using PitWall.Telemetry.Live.Models;
using PitWall.Telemetry.Live.Services;
using Xunit;

namespace PitWall.Telemetry.Live.Tests
{
    /// <summary>
    /// Tests for LiveTelemetrySerializer — ensures the wire format for
    /// WebSocket broadcast is stable and contains all expected fields.
    /// </summary>
    public class LiveTelemetrySerializationTests
    {
        #region SerializeSnapshot

        [Fact]
        public void SerializeSnapshot_ContainsAllPlayerVehicleFields()
        {
            var snapshot = CreateRichSnapshot();

            var json = LiveTelemetrySerializer.SerializeSnapshot(snapshot);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("telemetry", root.GetProperty("type").GetString());
            Assert.Equal("live", root.GetProperty("mode").GetString());
            Assert.True(root.TryGetProperty("timestamp", out _));
            Assert.True(root.TryGetProperty("sessionId", out _));
            Assert.True(root.TryGetProperty("speedKph", out _));
            Assert.True(root.TryGetProperty("throttle", out _));
            Assert.True(root.TryGetProperty("brake", out _));
            Assert.True(root.TryGetProperty("steering", out _));
            Assert.True(root.TryGetProperty("fuelLiters", out _));
            Assert.True(root.TryGetProperty("tyreTemps", out _));
            Assert.True(root.TryGetProperty("latitude", out _));
            Assert.True(root.TryGetProperty("longitude", out _));
        }

        [Fact]
        public void SerializeSnapshot_SpeedMapsFromPlayerVehicle()
        {
            var snapshot = CreateRichSnapshot();
            snapshot.PlayerVehicle.Speed = 255.5;

            var json = LiveTelemetrySerializer.SerializeSnapshot(snapshot);
            var doc = JsonDocument.Parse(json);

            Assert.Equal(255.5, doc.RootElement.GetProperty("speedKph").GetDouble());
        }

        [Fact]
        public void SerializeSnapshot_TyreTempsArrayHasFourElements()
        {
            var snapshot = CreateRichSnapshot();
            snapshot.PlayerVehicle.Wheels[0].TempMid = 85;
            snapshot.PlayerVehicle.Wheels[1].TempMid = 87;
            snapshot.PlayerVehicle.Wheels[2].TempMid = 90;
            snapshot.PlayerVehicle.Wheels[3].TempMid = 88;

            var json = LiveTelemetrySerializer.SerializeSnapshot(snapshot);
            var doc = JsonDocument.Parse(json);
            var temps = doc.RootElement.GetProperty("tyreTemps");

            Assert.Equal(4, temps.GetArrayLength());
            Assert.Equal(85, temps[0].GetDouble());
            Assert.Equal(87, temps[1].GetDouble());
            Assert.Equal(90, temps[2].GetDouble());
            Assert.Equal(88, temps[3].GetDouble());
        }

        [Fact]
        public void SerializeSnapshot_SessionIdPreserved()
        {
            var snapshot = CreateRichSnapshot();
            snapshot.SessionId = "lmu_20250101_racing_abc123";

            var json = LiveTelemetrySerializer.SerializeSnapshot(snapshot);
            var doc = JsonDocument.Parse(json);

            Assert.Equal("lmu_20250101_racing_abc123", doc.RootElement.GetProperty("sessionId").GetString());
        }

        [Fact]
        public void SerializeSnapshot_IncludesSessionMetadata()
        {
            var snapshot = CreateRichSnapshot();
            snapshot.Session.TrackName = "Monza";
            snapshot.Session.SessionType = "Race";
            snapshot.Session.NumVehicles = 20;

            var json = LiveTelemetrySerializer.SerializeSnapshot(snapshot);
            var doc = JsonDocument.Parse(json);

            Assert.Equal("Monza", doc.RootElement.GetProperty("track").GetString());
            Assert.Equal("Race", doc.RootElement.GetProperty("sessionType").GetString());
            Assert.Equal(20, doc.RootElement.GetProperty("numVehicles").GetInt32());
        }

        [Fact]
        public void SerializeSnapshot_NullPlayerVehicle_ReturnsEmptyTelemetry()
        {
            var snapshot = new TelemetrySnapshot
            {
                Timestamp = DateTime.UtcNow,
                SessionId = "test",
                PlayerVehicle = null!
            };

            var json = LiveTelemetrySerializer.SerializeSnapshot(snapshot);
            var doc = JsonDocument.Parse(json);

            Assert.Equal("telemetry", doc.RootElement.GetProperty("type").GetString());
            Assert.Equal(0, doc.RootElement.GetProperty("speedKph").GetDouble());
        }

        #endregion

        #region SerializeMetaMessage

        [Fact]
        public void SerializeMetaMessage_ContainsRequiredFields()
        {
            var json = LiveTelemetrySerializer.SerializeMetaMessage("lmu_session_123");
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("meta", root.GetProperty("type").GetString());
            Assert.Equal("live", root.GetProperty("mode").GetString());
            Assert.Equal("lmu_session_123", root.GetProperty("sessionId").GetString());
        }

        #endregion

        #region SerializeUnavailableMessage

        [Fact]
        public void SerializeUnavailableMessage_ContainsErrorType()
        {
            var json = LiveTelemetrySerializer.SerializeUnavailableMessage();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("error", root.GetProperty("type").GetString());
            Assert.True(root.TryGetProperty("message", out var msg));
            Assert.False(string.IsNullOrWhiteSpace(msg.GetString()));
        }

        #endregion

        #region Helpers

        private static TelemetrySnapshot CreateRichSnapshot()
        {
            return new TelemetrySnapshot
            {
                Timestamp = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                SessionId = "test-session-001",
                Session = new SessionInfo
                {
                    TrackName = "TestTrack",
                    SessionType = "Practice",
                    NumVehicles = 1,
                    TrackLength = 4000.0
                },
                PlayerVehicle = new VehicleTelemetry
                {
                    VehicleId = 0,
                    IsPlayer = true,
                    Speed = 200.5,
                    Fuel = 45.3,
                    Brake = 0.0,
                    Throttle = 0.85,
                    Steering = -0.12,
                    PosX = 48.123,
                    PosZ = 11.456,
                    ElapsedTime = 120.5
                },
                AllVehicles = new List<VehicleTelemetry>
                {
                    new VehicleTelemetry { VehicleId = 0, IsPlayer = true, Speed = 200.5 }
                },
                Scoring = new ScoringInfo { NumVehicles = 1, SectorFlags = new int[3] }
            };
        }

        #endregion
    }
}
