using System;
using PitWall.Core.Models;
using PitWall.Core.Utilities;
using Xunit;

namespace PitWall.Tests
{
    public class DataValidatorTests
    {
        private TelemetrySample CreateValidSample()
        {
            return new TelemetrySample(
                Timestamp: DateTime.UtcNow,
                SpeedKph: 200.0,
                TyreTempsC: new[] { 80.0, 85.0, 82.0, 83.0 },
                FuelLiters: 50.0,
                Brake: 0.5,
                Throttle: 0.8,
                Steering: 0.0
            )
            {
                LapNumber = 1
            };
        }

        [Fact]
        public void IsValid_ReturnsTrue_ForValidSample()
        {
            var sample = CreateValidSample();

            var result = DataValidator.IsValid(sample);

            Assert.True(result);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-0.1)]
        [InlineData(-100)]
        public void IsValid_ReturnsFalse_WhenSpeedIsNegative(double speed)
        {
            var sample = CreateValidSample() with { SpeedKph = speed };

            var result = DataValidator.IsValid(sample);

            Assert.False(result);
        }

        [Theory]
        [InlineData(601)]
        [InlineData(700)]
        [InlineData(1000)]
        public void IsValid_ReturnsFalse_WhenSpeedExceedsMaximum(double speed)
        {
            var sample = CreateValidSample() with { SpeedKph = speed };

            var result = DataValidator.IsValid(sample);

            Assert.False(result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(300)]
        [InlineData(600)]
        public void IsValid_ReturnsTrue_ForValidSpeedBoundaries(double speed)
        {
            var sample = CreateValidSample() with { SpeedKph = speed };

            var result = DataValidator.IsValid(sample);

            Assert.True(result);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-0.1)]
        [InlineData(-50)]
        public void IsValid_ReturnsFalse_WhenFuelIsNegative(double fuel)
        {
            var sample = CreateValidSample() with { FuelLiters = fuel };

            var result = DataValidator.IsValid(sample);

            Assert.False(result);
        }

        [Theory]
        [InlineData(501)]
        [InlineData(600)]
        [InlineData(1000)]
        public void IsValid_ReturnsFalse_WhenFuelExceedsMaximum(double fuel)
        {
            var sample = CreateValidSample() with { FuelLiters = fuel };

            var result = DataValidator.IsValid(sample);

            Assert.False(result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(250)]
        [InlineData(500)]
        public void IsValid_ReturnsTrue_ForValidFuelBoundaries(double fuel)
        {
            var sample = CreateValidSample() with { FuelLiters = fuel };

            var result = DataValidator.IsValid(sample);

            Assert.True(result);
        }

        [Fact]
        public void IsValid_ReturnsFalse_WhenTyreTempsIsNull()
        {
            var sample = CreateValidSample() with { TyreTempsC = null! };

            var result = DataValidator.IsValid(sample);

            Assert.False(result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(10)]
        public void IsValid_ReturnsFalse_WhenTyreTempsArrayIsNotLength4(int length)
        {
            var temps = new double[length];
            for (int i = 0; i < length; i++)
            {
                temps[i] = 80.0;
            }
            var sample = CreateValidSample() with { TyreTempsC = temps };

            var result = DataValidator.IsValid(sample);

            Assert.False(result);
        }

        [Theory]
        [InlineData(-51)]
        [InlineData(-100)]
        [InlineData(-200)]
        public void IsValid_ReturnsFalse_WhenAnyTyreTempBelowMinimum(double temp)
        {
            var sample = CreateValidSample() with
            {
                TyreTempsC = new[] { 80.0, temp, 82.0, 83.0 }
            };

            var result = DataValidator.IsValid(sample);

            Assert.False(result);
        }

        [Theory]
        [InlineData(301)]
        [InlineData(400)]
        [InlineData(500)]
        public void IsValid_ReturnsFalse_WhenAnyTyreTempAboveMaximum(double temp)
        {
            var sample = CreateValidSample() with
            {
                TyreTempsC = new[] { 80.0, 85.0, temp, 83.0 }
            };

            var result = DataValidator.IsValid(sample);

            Assert.False(result);
        }

        [Theory]
        [InlineData(-50)]
        [InlineData(0)]
        [InlineData(150)]
        [InlineData(300)]
        public void IsValid_ReturnsTrue_ForValidTyreTempBoundaries(double temp)
        {
            var sample = CreateValidSample() with
            {
                TyreTempsC = new[] { temp, temp, temp, temp }
            };

            var result = DataValidator.IsValid(sample);

            Assert.True(result);
        }

        [Fact]
        public void IsValid_ReturnsFalse_WhenMultipleTyreTempsInvalid()
        {
            var sample = CreateValidSample() with
            {
                TyreTempsC = new[] { -51.0, 301.0, 82.0, 83.0 }
            };

            var result = DataValidator.IsValid(sample);

            Assert.False(result);
        }

        [Fact]
        public void IsValid_ReturnsFalse_WhenAllTyreTempsInvalid()
        {
            var sample = CreateValidSample() with
            {
                TyreTempsC = new[] { 301.0, 301.0, 301.0, 301.0 }
            };

            var result = DataValidator.IsValid(sample);

            Assert.False(result);
        }

        [Fact]
        public void IsValid_ValidatesAllTyrePositions()
        {
            var sample1 = CreateValidSample() with { TyreTempsC = new[] { 301.0, 80.0, 80.0, 80.0 } };
            var sample2 = CreateValidSample() with { TyreTempsC = new[] { 80.0, 301.0, 80.0, 80.0 } };
            var sample3 = CreateValidSample() with { TyreTempsC = new[] { 80.0, 80.0, 301.0, 80.0 } };
            var sample4 = CreateValidSample() with { TyreTempsC = new[] { 80.0, 80.0, 80.0, 301.0 } };

            Assert.False(DataValidator.IsValid(sample1));
            Assert.False(DataValidator.IsValid(sample2));
            Assert.False(DataValidator.IsValid(sample3));
            Assert.False(DataValidator.IsValid(sample4));
        }

        [Fact]
        public void IsValid_ReturnsFalse_ForMultipleInvalidFields()
        {
            var sample = CreateValidSample() with
            {
                SpeedKph = -1,
                FuelLiters = -1
            };

            var result = DataValidator.IsValid(sample);

            Assert.False(result);
        }

        [Fact]
        public void IsValid_HandlesExtremeButValidValues()
        {
            var sample = new TelemetrySample(
                Timestamp: DateTime.UtcNow,
                SpeedKph: 600,
                TyreTempsC: new[] { -50.0, 300.0, 0.0, 150.0 },
                FuelLiters: 500,
                Brake: 1.0,
                Throttle: 1.0,
                Steering: 1.0
            );

            var result = DataValidator.IsValid(sample);

            Assert.True(result);
        }

        [Fact]
        public void IsSessionActive_AlwaysReturnsTrue()
        {
            var sample = CreateValidSample();

            var result = DataValidator.IsSessionActive(sample);

            Assert.True(result);
        }

        [Fact]
        public void IsSessionActive_ReturnsTrueForInvalidSample()
        {
            var sample = CreateValidSample() with
            {
                SpeedKph = -100,
                FuelLiters = -100
            };

            var result = DataValidator.IsSessionActive(sample);

            Assert.True(result);
        }

        [Fact]
        public void IsValid_HandlesColdTyreTemperatures()
        {
            var sample = CreateValidSample() with
            {
                TyreTempsC = new[] { -40.0, -30.0, -20.0, -10.0 }
            };

            var result = DataValidator.IsValid(sample);

            Assert.True(result);
        }

        [Fact]
        public void IsValid_HandlesHighTyreTemperatures()
        {
            var sample = CreateValidSample() with
            {
                TyreTempsC = new[] { 250.0, 260.0, 270.0, 280.0 }
            };

            var result = DataValidator.IsValid(sample);

            Assert.True(result);
        }
    }
}
