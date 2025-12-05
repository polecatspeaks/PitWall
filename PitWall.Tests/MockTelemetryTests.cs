using Xunit;
using PitWall.Tests.Mocks;

namespace PitWall.Tests
{
    /// <summary>
    /// Tests for mock telemetry builder
    /// </summary>
    public class MockTelemetryTests
    {
        [Fact]
        public void MockTelemetry_ProvidesValidGT3Data()
        {
            // Arrange & Act
            var telemetry = MockTelemetryBuilder.GT3()
                .WithLapTime(120.5)
                .WithFuelRemaining(50.0)
                .Build();

            // Assert
            Assert.Equal(120.5, telemetry.LastLapTime, 0.1);
            Assert.InRange(telemetry.FuelRemaining, 0, 120);
            Assert.Equal(120.0, telemetry.FuelCapacity);
            Assert.Equal("Porsche 911 GT3 R", telemetry.CarName);
        }

        [Fact]
        public void MockTelemetry_ProvidesValidLMP2Data()
        {
            // Arrange & Act
            var telemetry = MockTelemetryBuilder.LMP2()
                .WithLapTime(110.0)
                .WithFuelRemaining(40.0)
                .Build();

            // Assert
            Assert.Equal(110.0, telemetry.LastLapTime, 0.1);
            Assert.InRange(telemetry.FuelRemaining, 0, 75);
            Assert.Equal(75.0, telemetry.FuelCapacity);
            Assert.Equal("Oreca 07", telemetry.CarName);
        }

        [Fact]
        public void MockTelemetry_BuilderChaining_Works()
        {
            // Arrange & Act
            var telemetry = MockTelemetryBuilder.GT3()
                .WithFuelRemaining(25.0)
                .WithCurrentLap(10)
                .InPit()
                .Build();

            // Assert
            Assert.Equal(25.0, telemetry.FuelRemaining);
            Assert.Equal(10, telemetry.CurrentLap);
            Assert.True(telemetry.IsInPit);
        }
    }
}
