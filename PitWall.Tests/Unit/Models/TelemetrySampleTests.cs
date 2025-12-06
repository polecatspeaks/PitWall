using System;
using System.Collections.Generic;
using Xunit;
using PitWall.Models.Telemetry;

namespace PitWall.Tests.Unit.Models
{
    /// <summary>
    /// Unit tests for TelemetrySample - represents a single 60Hz sample with all channel data
    /// </summary>
    public class TelemetrySampleTests
    {
        [Fact]
        public void Constructor_InitializesAllProperties()
        {
            // Arrange
            var timestamp = DateTime.UtcNow;
            var lapNumber = 5;
            var speed = 250.5f;
            var throttle = 0.85f;
            var brake = 0.0f;
            var steeringAngle = 0.15f;
            var engineRpm = 7500;
            var gear = 3;
            var fuelLevel = 25.5f;
            var engineTemp = 95.0f;
            var oilTemp = 88.0f;
            var oilPressure = 65.0f;

            // Act
            var sample = new TelemetrySample
            {
                Timestamp = timestamp,
                LapNumber = lapNumber,
                Speed = speed,
                Throttle = throttle,
                Brake = brake,
                SteeringAngle = steeringAngle,
                EngineRpm = engineRpm,
                Gear = gear,
                FuelLevel = fuelLevel,
                EngineTemp = engineTemp,
                OilTemp = oilTemp,
                OilPressure = oilPressure
            };

            // Assert
            Assert.Equal(timestamp, sample.Timestamp);
            Assert.Equal(lapNumber, sample.LapNumber);
            Assert.Equal(speed, sample.Speed);
            Assert.Equal(throttle, sample.Throttle);
            Assert.Equal(brake, sample.Brake);
            Assert.Equal(steeringAngle, sample.SteeringAngle);
            Assert.Equal(engineRpm, sample.EngineRpm);
            Assert.Equal(gear, sample.Gear);
            Assert.Equal(fuelLevel, sample.FuelLevel);
            Assert.Equal(engineTemp, sample.EngineTemp);
            Assert.Equal(oilTemp, sample.OilTemp);
            Assert.Equal(oilPressure, sample.OilPressure);
        }

        [Fact]
        public void Sample_CanRepresentValidChannelData()
        {
            // Arrange & Act
            var sample = new TelemetrySample
            {
                Speed = 300.0f,
                Throttle = 1.0f,
                Brake = 0.0f,
                EngineRpm = 8000,
                Gear = 5,
                FuelLevel = 50.0f,
                EngineTemp = 100.0f
            };

            // Assert
            Assert.Equal(300.0f, sample.Speed);
            Assert.Equal(1.0f, sample.Throttle);
            Assert.Equal(0.0f, sample.Brake);
            Assert.Equal(8000, sample.EngineRpm);
            Assert.Equal(5, sample.Gear);
            Assert.Equal(50.0f, sample.FuelLevel);
            Assert.Equal(100.0f, sample.EngineTemp);
        }

        [Fact]
        public void Sample_CanBeStoredInCollection()
        {
            // Arrange
            var samples = new List<TelemetrySample>();
            var timestamp = DateTime.UtcNow;

            // Act
            for (int i = 0; i < 60; i++) // 1 second of 60Hz data
            {
                samples.Add(new TelemetrySample
                {
                    Timestamp = timestamp.AddMilliseconds(i * 16.667), // ~60Hz interval
                    LapNumber = 1,
                    Speed = 200.0f + (i * 0.5f),
                    Throttle = 0.5f + (i * 0.002f),
                    EngineRpm = 5000 + (i * 10)
                });
            }

            // Assert
            Assert.Equal(60, samples.Count);
            Assert.Equal(1, samples[0].LapNumber);
            Assert.Equal(1, samples[59].LapNumber); // All samples same lap
        }
    }
}
