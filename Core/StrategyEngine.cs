using System;
using PitWall.Models;

namespace PitWall.Core
{
    /// <summary>
    /// Basic fuel-focused strategy engine.
    /// </summary>
    public class StrategyEngine : IStrategyEngine
    {
        private readonly FuelStrategy _fuelStrategy;

        public StrategyEngine(FuelStrategy fuelStrategy)
        {
            _fuelStrategy = fuelStrategy;
        }

        public Recommendation GetRecommendation(Telemetry telemetry)
        {
            // Update fuel model with latest lap if lap incremented
            if (telemetry.IsLapValid && telemetry.CurrentLap > 0)
            {
                // For Phase 1 we only track fuel; assume prior lap consumed average fuel
                _fuelStrategy.RecordLap(telemetry.CurrentLap, telemetry.FuelCapacity, telemetry.FuelRemaining);
            }

            int lapsRemaining = _fuelStrategy.PredictLapsRemaining(telemetry.FuelRemaining);
            if (lapsRemaining < 2)
            {
                return new Recommendation
                {
                    ShouldPit = true,
                    Type = RecommendationType.Fuel,
                    Priority = Priority.Critical,
                    Message = "Box this lap for fuel"
                };
            }

            return new Recommendation
            {
                ShouldPit = false,
                Type = RecommendationType.None,
                Priority = Priority.Info,
                Message = string.Empty
            };
        }

        public void RecordLap(Telemetry telemetry)
        {
            // Assume last lap used (FuelCapacity - FuelRemaining) for simplicity in Phase 1
            double startFuel = telemetry.FuelCapacity;
            double endFuel = telemetry.FuelRemaining;
            _fuelStrategy.RecordLap(telemetry.CurrentLap, startFuel, endFuel);
        }
    }
}
