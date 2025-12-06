using System;
using System.Collections.Generic;
using Xunit;
using PitWall.Profile;

namespace PitWall.Tests.Unit.Profile
{
    /// <summary>
    /// Tests for 90-day exponential decay recency weighting
    /// Weight = exp(-ln(2) * days / 90)
    /// </summary>
    public class RecencyWeightCalculatorTests
    {
        private readonly RecencyWeightCalculator _calculator = new();

        [Fact]
        public void CalculateWeight_AtZeroDays_ReturnsOne()
        {
            // Arrange
            var now = DateTime.UtcNow;

            // Act
            double weight = _calculator.CalculateWeight(now);

            // Assert
            Assert.Equal(1.0, weight, 2); // 0.99+ due to floating point
        }

        [Fact]
        public void CalculateWeight_At90Days_ReturnsHalf()
        {
            // Arrange
            var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);

            // Act
            double weight = _calculator.CalculateWeight(ninetyDaysAgo);

            // Assert
            Assert.Equal(0.5, weight, 2); // Half-life at 90 days
        }

        [Fact]
        public void CalculateWeight_At180Days_ReturnsQuarter()
        {
            // Arrange
            var oneEightyDaysAgo = DateTime.UtcNow.AddDays(-180);

            // Act
            double weight = _calculator.CalculateWeight(oneEightyDaysAgo);

            // Assert
            Assert.Equal(0.25, weight, 2); // Two half-lives
        }

        [Fact]
        public void CalculateWeight_AtOneDay_IsLessThanOne()
        {
            // Arrange
            var oneDayAgo = DateTime.UtcNow.AddDays(-1);

            // Act
            double weight = _calculator.CalculateWeight(oneDayAgo);

            // Assert
            Assert.True(weight < 1.0);
            Assert.True(weight > 0.99); // Very close to 1.0
        }

        [Fact]
        public void CalculateWeight_FutureDate_ReturnsOne()
        {
            // Arrange
            var tomorrow = DateTime.UtcNow.AddDays(1);

            // Act
            double weight = _calculator.CalculateWeight(tomorrow);

            // Assert
            Assert.Equal(1.0, weight, 2); // Future dates clamped to 0 days
        }

        [Fact]
        public void CalculateWeightedAverage_CombinesValues()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var measurements = new List<(DateTime, double)>
            {
                (now, 100.0),           // weight = 1.0
                (now.AddDays(-90), 100.0) // weight = 0.5
            };

            // Act
            double weighted = _calculator.CalculateWeightedAverage(measurements);

            // Assert
            // (100 * 1.0 + 100 * 0.5) / (1.0 + 0.5) = 150 / 1.5 = 100
            Assert.Equal(100.0, weighted, 0);
        }

        [Fact]
        public void CalculateWeightedAverage_EmptyList_ReturnsZero()
        {
            // Arrange
            var measurements = new List<(DateTime, double)>();

            // Act
            double weighted = _calculator.CalculateWeightedAverage(measurements);

            // Assert
            Assert.Equal(0.0, weighted);
        }
    }
}
