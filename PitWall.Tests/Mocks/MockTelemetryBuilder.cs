using PitWall.Models;

namespace PitWall.Tests.Mocks
{
    /// <summary>
    /// Builder for creating mock telemetry data for testing
    /// </summary>
    public class MockTelemetryBuilder
    {
        private Telemetry _telemetry = new();

        /// <summary>
        /// Creates a builder with GT3 car defaults
        /// </summary>
        public static MockTelemetryBuilder GT3()
        {
            var builder = new MockTelemetryBuilder();
            builder._telemetry = new Telemetry
            {
                FuelCapacity = 120.0,
                FuelRemaining = 100.0,
                LastLapTime = 120.0,
                BestLapTime = 119.5,
                CurrentLap = 1,
                IsInPit = false,
                IsLapValid = true,
                TrackName = "Watkins Glen",
                CarName = "Porsche 911 GT3 R",
                IsGameRunning = true
            };
            return builder;
        }

        /// <summary>
        /// Creates a builder with LMP2 car defaults
        /// </summary>
        public static MockTelemetryBuilder LMP2()
        {
            var builder = new MockTelemetryBuilder();
            builder._telemetry = new Telemetry
            {
                FuelCapacity = 75.0,
                FuelRemaining = 60.0,
                LastLapTime = 110.0,
                BestLapTime = 109.5,
                CurrentLap = 1,
                IsInPit = false,
                IsLapValid = true,
                TrackName = "Watkins Glen",
                CarName = "Oreca 07",
                IsGameRunning = true
            };
            return builder;
        }

        public MockTelemetryBuilder WithFuelRemaining(double fuel)
        {
            _telemetry.FuelRemaining = fuel;
            return this;
        }

        public MockTelemetryBuilder WithFuelCapacity(double capacity)
        {
            _telemetry.FuelCapacity = capacity;
            return this;
        }

        public MockTelemetryBuilder WithLapTime(double seconds)
        {
            _telemetry.LastLapTime = seconds;
            return this;
        }

        public MockTelemetryBuilder WithCurrentLap(int lap)
        {
            _telemetry.CurrentLap = lap;
            return this;
        }

        public MockTelemetryBuilder InPit()
        {
            _telemetry.IsInPit = true;
            return this;
        }

        public MockTelemetryBuilder InvalidLap()
        {
            _telemetry.IsLapValid = false;
            return this;
        }

        public Telemetry Build()
        {
            return _telemetry;
        }
    }
}
