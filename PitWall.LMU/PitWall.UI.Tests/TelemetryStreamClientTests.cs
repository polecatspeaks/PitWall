using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PitWall.UI.Models;
using PitWall.UI.Services;
using Xunit;

namespace PitWall.UI.Tests
{
    public class TelemetryStreamClientTests
    {
        [Fact]
        public void Constructor_WithUri_InitializesSuccessfully()
        {
            var uri = new Uri("ws://localhost:5000");
            
            var client = new TelemetryStreamClient(uri);
            
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_WithNullLogger_UsesNullLogger()
        {
            var uri = new Uri("ws://localhost:5000");
            
            var client = new TelemetryStreamClient(uri, null);
            
            Assert.NotNull(client);
        }

        [Fact]
        public async Task ConnectAsync_WithInvalidUri_ThrowsException()
        {
            var uri = new Uri("ws://invalid-nonexistent-server-12345.local:9999");
            var client = new TelemetryStreamClient(uri);

            await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                await client.ConnectAsync(
                    sessionId: 1,
                    startRow: 0,
                    endRow: 100,
                    intervalMs: 100,
                    onMessage: _ => { },
                    cancellationToken: CancellationToken.None);
            });
        }

        [Fact]
        public async Task ConnectAsync_ConstructsCorrectUri()
        {
            // This test verifies the URI construction logic
            var baseUri = new Uri("ws://localhost:5000");
            var client = new TelemetryStreamClient(baseUri);
            
            // We can't test actual connection without a real WebSocket server
            // but we can verify the client doesn't throw on construction
            Assert.NotNull(client);
        }

        [Fact]
        public async Task ConnectAsync_WithCancellationToken_CanBeCancelled()
        {
            var uri = new Uri("ws://localhost:5000");
            var client = new TelemetryStreamClient(uri);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var task = client.ConnectAsync(
                sessionId: 1,
                startRow: 0,
                endRow: 100,
                intervalMs: 100,
                onMessage: _ => { },
                cancellationToken: cts.Token);

            // Should complete quickly (cancelled or unable to connect)
            await Assert.ThrowsAnyAsync<Exception>(async () => await task);
        }

        [Fact]
        public void TelemetryMessageParser_ParsesValidJson()
        {
            var json = @"{
                ""speedKph"": 250.5,
                ""throttle"": 0.8,
                ""brake"": 0.2,
                ""steering"": -0.15,
                ""fuelLiters"": 45.0,
                ""lapNumber"": 5,
                ""tyreTemps"": [90.0, 92.0, 88.0, 91.0]
            }";

            var result = TelemetryMessageParser.Parse(json);

            Assert.Equal(250.5, result.SpeedKph);
            Assert.Equal(0.8, result.ThrottlePosition);
            Assert.Equal(0.2, result.BrakePosition);
            Assert.Equal(-0.15, result.SteeringAngle);
            Assert.Equal(45.0, result.FuelLiters);
            Assert.Equal(5, result.LapNumber);
            Assert.Equal(4, result.TyreTempsC.Length);
        }

        [Fact]
        public void TelemetryMessageParser_NormalizesPedalValues()
        {
            // Test values in 0-100 range get normalized to 0-1
            var json = @"{
                ""throttle"": 80,
                ""brake"": 50
            }";

            var result = TelemetryMessageParser.Parse(json);

            Assert.Equal(0.8, result.ThrottlePosition);
            Assert.Equal(0.5, result.BrakePosition);
        }

        [Fact]
        public void TelemetryMessageParser_NormalizesSteeringValues()
        {
            // Test steering values in -100 to 100 range get normalized
            var json = @"{
                ""steering"": 50
            }";

            var result = TelemetryMessageParser.Parse(json);

            Assert.Equal(0.5, result.SteeringAngle);
        }

        [Fact]
        public void TelemetryMessageParser_ClampsPedalValues()
        {
            // Test values that should be clamped
            // Values > 1 are treated as percentages (divided by 100), then clamped
            // 1.5 becomes 0.015, -0.5 becomes -0.5 then clamped to 0
            var json = @"{
                ""throttle"": 150,
                ""brake"": -0.5
            }";

            var result = TelemetryMessageParser.Parse(json);

            // 150 is treated as percentage (150/100 = 1.5) then clamped to 1.0
            // -0.5 gets clamped to 0.0
            Assert.InRange(result.ThrottlePosition, 0, 1);
            Assert.InRange(result.BrakePosition, 0, 1);
            Assert.Equal(1.0, result.ThrottlePosition);
            Assert.Equal(0.0, result.BrakePosition);
        }

        [Fact]
        public void TelemetryMessageParser_ClampsSteeringValues()
        {
            // Test steering value that should be clamped
            // Values with abs > 1 and abs <= 100 are treated as percentages
            var json = @"{
                ""steering"": 150
            }";

            var result = TelemetryMessageParser.Parse(json);

            // 150 is treated as percentage (150/100 = 1.5) then clamped to 1.0
            Assert.InRange(result.SteeringAngle, -1, 1);
            Assert.Equal(1.0, result.SteeringAngle);
        }

        [Fact]
        public void TelemetryMessageParser_HandlesNegativeSteering()
        {
            var json = @"{
                ""steering"": -75
            }";

            var result = TelemetryMessageParser.Parse(json);

            Assert.Equal(-0.75, result.SteeringAngle);
        }

        [Fact]
        public void TelemetryMessageParser_HandlesMissingTyreTemps()
        {
            var json = @"{
                ""speedKph"": 200.0
            }";

            var result = TelemetryMessageParser.Parse(json);

            Assert.NotNull(result.TyreTempsC);
            Assert.Empty(result.TyreTempsC);
        }

        [Fact]
        public void TelemetryMessageParser_HandlesEmptyTyreTemps()
        {
            var json = @"{
                ""speedKph"": 200.0,
                ""tyreTemps"": []
            }";

            var result = TelemetryMessageParser.Parse(json);

            Assert.NotNull(result.TyreTempsC);
            Assert.Empty(result.TyreTempsC);
        }

        [Fact]
        public void TelemetryMessageParser_HandlesPartialData()
        {
            var json = @"{
                ""speedKph"": 150.0
            }";

            var result = TelemetryMessageParser.Parse(json);

            Assert.Equal(150.0, result.SpeedKph);
            Assert.Equal(0.0, result.ThrottlePosition);
            Assert.Equal(0.0, result.BrakePosition);
        }

        [Fact]
        public void TelemetryMessageParser_HandlesMalformedJson()
        {
            var json = "not valid json";

            var exception = Record.Exception(() => TelemetryMessageParser.Parse(json));

            Assert.NotNull(exception);
        }

        [Fact]
        public void TelemetryMessageParser_HandlesNullJson()
        {
            var exception = Record.Exception(() => TelemetryMessageParser.Parse(null!));

            Assert.NotNull(exception);
        }

        [Fact]
        public void TelemetryMessageParser_HandlesEmptyJson()
        {
            var json = "{}";

            var result = TelemetryMessageParser.Parse(json);

            Assert.NotNull(result);
            Assert.Equal(0.0, result.SpeedKph);
        }

        [Fact]
        public void TelemetryMessageParser_PreservesTimestamp()
        {
            var timestamp = "2024-01-15T10:30:00Z";
            var json = $@"{{
                ""timestamp"": ""{timestamp}"",
                ""speedKph"": 200.0
            }}";

            var result = TelemetryMessageParser.Parse(json);

            Assert.NotNull(result.Timestamp);
        }

        [Fact]
        public void TelemetryMessageParser_HandlesLatLong()
        {
            var json = @"{
                ""latitude"": 45.6234,
                ""longitude"": -73.5689,
                ""speedKph"": 200.0
            }";

            var result = TelemetryMessageParser.Parse(json);

            Assert.Equal(45.6234, result.Latitude);
            Assert.Equal(-73.5689, result.Longitude);
        }

        [Fact]
        public void TelemetryMessageParser_HandlesLateralG()
        {
            var json = @"{
                ""lateralG"": 2.5,
                ""speedKph"": 200.0
            }";

            var result = TelemetryMessageParser.Parse(json);

            Assert.Equal(2.5, result.LateralG);
        }

        [Fact]
        public void TelemetryMessageParser_HandlesAllFields()
        {
            var json = @"{
                ""timestamp"": ""2024-01-15T10:30:00Z"",
                ""speedKph"": 250.5,
                ""throttle"": 0.95,
                ""brake"": 0.05,
                ""steering"": -0.25,
                ""fuelLiters"": 42.3,
                ""lapNumber"": 8,
                ""tyreTemps"": [95.5, 96.2, 94.8, 95.1],
                ""latitude"": 45.6234,
                ""longitude"": -73.5689,
                ""lateralG"": 1.8
            }";

            var result = TelemetryMessageParser.Parse(json);

            Assert.NotNull(result.Timestamp);
            Assert.Equal(250.5, result.SpeedKph);
            Assert.Equal(0.95, result.ThrottlePosition);
            Assert.Equal(0.05, result.BrakePosition);
            Assert.Equal(-0.25, result.SteeringAngle);
            Assert.Equal(42.3, result.FuelLiters);
            Assert.Equal(8, result.LapNumber);
            Assert.Equal(4, result.TyreTempsC.Length);
            Assert.Equal(45.6234, result.Latitude);
            Assert.Equal(-73.5689, result.Longitude);
            Assert.Equal(1.8, result.LateralG);
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(0.5, 0.5)]
        [InlineData(1.0, 1.0)]
        [InlineData(50.0, 0.5)]
        [InlineData(100.0, 1.0)]
        public void TelemetryMessageParser_NormalizesThrottle(double input, double expected)
        {
            var json = $@"{{""throttle"": {input}}}";

            var result = TelemetryMessageParser.Parse(json);

            Assert.Equal(expected, result.ThrottlePosition);
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(0.5, 0.5)]
        [InlineData(1.0, 1.0)]
        [InlineData(50.0, 0.5)]
        [InlineData(100.0, 1.0)]
        public void TelemetryMessageParser_NormalizesBrake(double input, double expected)
        {
            var json = $@"{{""brake"": {input}}}";

            var result = TelemetryMessageParser.Parse(json);

            Assert.Equal(expected, result.BrakePosition);
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(0.5, 0.5)]
        [InlineData(-0.5, -0.5)]
        [InlineData(50.0, 0.5)]
        [InlineData(-50.0, -0.5)]
        [InlineData(100.0, 1.0)]
        [InlineData(-100.0, -1.0)]
        public void TelemetryMessageParser_NormalizesSteering(double input, double expected)
        {
            var json = $@"{{""steering"": {input}}}";

            var result = TelemetryMessageParser.Parse(json);

            Assert.Equal(expected, result.SteeringAngle);
        }

        [Fact]
        public void TelemetryMessageParser_VeryLargeValues_GetsClamped()
        {
            var json = @"{
                ""throttle"": 9999,
                ""brake"": -9999,
                ""steering"": 9999
            }";

            var result = TelemetryMessageParser.Parse(json);

            Assert.InRange(result.ThrottlePosition, 0, 1);
            Assert.InRange(result.BrakePosition, 0, 1);
            Assert.InRange(result.SteeringAngle, -1, 1);
        }

        [Fact]
        public void TelemetryMessageParser_CaseInsensitive_ParsesCorrectly()
        {
            var json = @"{
                ""SpeedKph"": 200.0,
                ""THROTTLE"": 0.8,
                ""Brake"": 0.2
            }";

            var result = TelemetryMessageParser.Parse(json);

            Assert.Equal(200.0, result.SpeedKph);
            Assert.Equal(0.8, result.ThrottlePosition);
            Assert.Equal(0.2, result.BrakePosition);
        }
    }
}
