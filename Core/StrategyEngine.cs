using System;
using PitWall.Models;

namespace PitWall.Core
{
    /// <summary>
    /// Strategy engine combining fuel and tyre signals.
    /// </summary>
    public class StrategyEngine : IStrategyEngine
    {
        private readonly FuelStrategy _fuelStrategy;
        private readonly TyreDegradation _tyreDegradation;
        private readonly TrafficAnalyzer _trafficAnalyzer;
        private const double TyreThreshold = 30.0; // percent wear remaining trigger

        public StrategyEngine(FuelStrategy fuelStrategy) : this(fuelStrategy, new TyreDegradation(), new TrafficAnalyzer())
        {
        }

        public StrategyEngine(FuelStrategy fuelStrategy, TyreDegradation tyreDegradation) 
            : this(fuelStrategy, tyreDegradation, new TrafficAnalyzer())
        {
        }

        public StrategyEngine(FuelStrategy fuelStrategy, TyreDegradation tyreDegradation, TrafficAnalyzer trafficAnalyzer)
        {
            _fuelStrategy = fuelStrategy;
            _tyreDegradation = tyreDegradation;
            _trafficAnalyzer = trafficAnalyzer;
        }

        public Recommendation GetRecommendation(Telemetry telemetry)
        {
            // Update fuel model with latest lap if lap incremented
            if (telemetry.IsLapValid && telemetry.CurrentLap > 0)
            {
                _fuelStrategy.RecordLap(telemetry.CurrentLap, telemetry.FuelCapacity, telemetry.FuelRemaining);
                _tyreDegradation.RecordLap(
                    telemetry.CurrentLap,
                    telemetry.TyreWearFrontLeft,
                    telemetry.TyreWearFrontRight,
                    telemetry.TyreWearRearLeft,
                    telemetry.TyreWearRearRight);
            }

            int lapsRemaining = _fuelStrategy.PredictLapsRemaining(telemetry.FuelRemaining);
            if (lapsRemaining < 2)
            {
                // Check if pit entry is safe before critical fuel call
                if (_trafficAnalyzer.IsPitEntryUnsafe(telemetry.BestLapTime, telemetry.Opponents))
                {
                    return new Recommendation
                    {
                        ShouldPit = false,
                        Type = RecommendationType.Traffic,
                        Priority = Priority.Warning,
                        Message = _trafficAnalyzer.GetTrafficMessage(telemetry.BestLapTime, telemetry.Opponents)
                    };
                }

                return new Recommendation
                {
                    ShouldPit = true,
                    Type = RecommendationType.Fuel,
                    Priority = Priority.Critical,
                    Message = "Box this lap for fuel"
                };
            }

            // Tyre logic: pit if any tyre at/below threshold or projected under threshold within 2 laps
            if (IsTyrePitRecommended(out var tyreMessage))
            {
                // Check if pit entry is safe
                if (_trafficAnalyzer.IsPitEntryUnsafe(telemetry.BestLapTime, telemetry.Opponents))
                {
                    return new Recommendation
                    {
                        ShouldPit = false,
                        Type = RecommendationType.Traffic,
                        Priority = Priority.Info,
                        Message = _trafficAnalyzer.GetTrafficMessage(telemetry.BestLapTime, telemetry.Opponents) + " (tyres need service)"
                    };
                }

                return new Recommendation
                {
                    ShouldPit = true,
                    Type = RecommendationType.Tyres,
                    Priority = Priority.Warning,
                    Message = tyreMessage
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

            _tyreDegradation.RecordLap(
                telemetry.CurrentLap,
                telemetry.TyreWearFrontLeft,
                telemetry.TyreWearFrontRight,
                telemetry.TyreWearRearLeft,
                telemetry.TyreWearRearRight);
        }

        private bool IsTyrePitRecommended(out string message)
        {
            message = string.Empty;
            var positions = new[]
            {
                (TyrePosition.FrontLeft, "front left"),
                (TyrePosition.FrontRight, "front right"),
                (TyrePosition.RearLeft, "rear left"),
                (TyrePosition.RearRight, "rear right")
            };

            foreach (var (pos, label) in positions)
            {
                double latest = _tyreDegradation.GetLatestWear(pos);
                if (latest <= 0)
                {
                    continue; // No data yet
                }
                if (latest <= TyreThreshold)
                {
                    message = $"Box for tyres: {label} at {latest:F1}%";
                    return true;
                }

                int projectedLaps = _tyreDegradation.PredictLapsUntilThreshold(pos, TyreThreshold);
                if (projectedLaps <= 2)
                {
                    message = $"Box soon: {label} tyre wear low (<= {TyreThreshold}% in {projectedLaps} laps)";
                    return true;
                }
            }

            return false;
        }
    }
}
