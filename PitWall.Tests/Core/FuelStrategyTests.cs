using Xunit;
using PitWall.Core;

namespace PitWall.Tests.Core
{
    public class FuelStrategyTests
    {
        [Fact]
        public void CalculateFuelUsed_WithStartAndEndFuel_ReturnsCorrectUsage()
        {
            // Arrange
            var fuelStrategy = new FuelStrategy();
            double startFuel = 50.0;
            double endFuel = 45.0;

            // Act
            double fuelUsed = fuelStrategy.CalculateFuelUsed(startFuel, endFuel);

            // Assert
            Assert.Equal(5.0, fuelUsed, 2); // 2 decimal places precision
        }

        [Fact]
        public void RecordLap_WithFuelData_StoresLapInformation()
        {
            // Arrange
            var fuelStrategy = new FuelStrategy();
            
            // Act
            fuelStrategy.RecordLap(lapNumber: 1, startFuel: 50.0, endFuel: 45.0);
            
            // Assert
            Assert.Equal(1, fuelStrategy.GetLapCount());
            Assert.Equal(5.0, fuelStrategy.GetFuelUsedOnLap(1), 2);
        }

        [Fact]
        public void GetAverageFuelPerLap_WithMultipleLaps_ReturnsCorrectAverage()
        {
            // Arrange
            var fuelStrategy = new FuelStrategy();
            fuelStrategy.RecordLap(1, 50.0, 45.0); // 5.0 liters
            fuelStrategy.RecordLap(2, 45.0, 39.5); // 5.5 liters
            fuelStrategy.RecordLap(3, 39.5, 34.5); // 5.0 liters

            // Act
            double average = fuelStrategy.GetAverageFuelPerLap();

            // Assert
            Assert.Equal(5.167, average, 2); // (5.0 + 5.5 + 5.0) / 3 = 5.167
        }

        [Fact]
        public void GetAverageFuelPerLap_WithNoLaps_ReturnsZero()
        {
            // Arrange
            var fuelStrategy = new FuelStrategy();

            // Act
            double average = fuelStrategy.GetAverageFuelPerLap();

            // Assert
            Assert.Equal(0.0, average);
        }

        [Fact]
        public void PredictLapsRemaining_WithCurrentFuelAndAverage_ReturnsCorrectPrediction()
        {
            // Arrange
            var fuelStrategy = new FuelStrategy();
            fuelStrategy.RecordLap(1, 50.0, 45.0); // 5.0 liters per lap average
            fuelStrategy.RecordLap(2, 45.0, 40.0); // 5.0 liters per lap average

            // Act
            int lapsRemaining = fuelStrategy.PredictLapsRemaining(currentFuel: 25.0);

            // Assert
            Assert.Equal(5, lapsRemaining); // 25.0 / 5.0 = 5 laps
        }

        [Fact]
        public void PredictLapsRemaining_WithNoData_ReturnsZero()
        {
            // Arrange
            var fuelStrategy = new FuelStrategy();

            // Act
            int lapsRemaining = fuelStrategy.PredictLapsRemaining(currentFuel: 25.0);

            // Assert
            Assert.Equal(0, lapsRemaining);
        }

        [Fact]
        public void PredictLapsRemaining_WithInsufficientFuel_ReturnsFlooredValue()
        {
            // Arrange
            var fuelStrategy = new FuelStrategy();
            fuelStrategy.RecordLap(1, 50.0, 45.0); // 5.0 liters per lap

            // Act
            int lapsRemaining = fuelStrategy.PredictLapsRemaining(currentFuel: 12.0);

            // Assert
            Assert.Equal(2, lapsRemaining); // 12.0 / 5.0 = 2.4, floor to 2
        }

        [Fact]
        public void Reset_ClearsAllLapData()
        {
            // Arrange
            var fuelStrategy = new FuelStrategy();
            fuelStrategy.RecordLap(1, 50.0, 45.0);
            fuelStrategy.RecordLap(2, 45.0, 40.0);

            // Act
            fuelStrategy.Reset();

            // Assert
            Assert.Equal(0, fuelStrategy.GetLapCount());
            Assert.Equal(0.0, fuelStrategy.GetAverageFuelPerLap());
        }
    }
}
