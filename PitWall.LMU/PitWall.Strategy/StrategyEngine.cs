using System;
using PitWall.Core.Models;

namespace PitWall.Strategy
{
    public class StrategyEngine
    {
        private const double TyreOverheatThreshold = 110.0;
        private const double AvgLapFuelConsumption = 1.8; // Liters per lap (configurable)
        private const double CriticalFuelLevel = 2.0; // Liters
        private const int PitWindowLaps = 5;
        private const double WheelLockBrakeThreshold = 0.9;
        private const double WheelLockSpeedKph = 120.0;
        private const double WheelLockThrottleMax = 0.05;
        private const double BrakeOverlapThreshold = 0.6;
        private const double ThrottleOverlapThreshold = 0.2;
        private const double HeavyBrakeSteerThreshold = 0.6;
        private const double ConfidenceHigh = 0.9;
        private const double ConfidenceMedium = 0.75;
        private const double ConfidenceLow = 0.55;
        private const double ConfidenceNone = 0.4;

        public string Evaluate(TelemetrySample sample)
        {
            return EvaluateWithConfidence(sample).Recommendation;
        }

        public StrategyEvaluation EvaluateWithConfidence(TelemetrySample sample)
        {
            if (sample == null)
                return new StrategyEvaluation("Invalid sample", 0.0);

            // Check tyre temperatures
            if (sample.TyreTempsC != null)
            {
                foreach (var t in sample.TyreTempsC)
                {
                    if (t >= TyreOverheatThreshold)
                    {
                        return new StrategyEvaluation("Tyre overheat: reduce pace / pit soon", ConfidenceHigh);
                    }
                }
            }

            // Check fuel levels
            int lapsRemaining = ProjectLapsRemaining(sample, AvgLapFuelConsumption);
            if (sample.FuelLiters <= CriticalFuelLevel)
            {
                return new StrategyEvaluation(
                    $"Critical fuel: pit now (~{lapsRemaining} laps remaining)",
                    ConfidenceHigh);
            }

            // Wheel lock risk: heavy brake, high speed, minimal throttle
            if (sample.Brake >= WheelLockBrakeThreshold
                && sample.SpeedKph >= WheelLockSpeedKph
                && sample.Throttle <= WheelLockThrottleMax)
            {
                return new StrategyEvaluation("Wheel lock risk: ease brake pressure", ConfidenceMedium);
            }

            // Brake/throttle overlap coaching
            if (sample.Brake >= BrakeOverlapThreshold && sample.Throttle >= ThrottleOverlapThreshold)
            {
                return new StrategyEvaluation("Brake/throttle overlap: smooth inputs", ConfidenceLow);
            }

            // Heavy brake while steering can destabilize the car
            if (sample.Brake >= BrakeOverlapThreshold && Math.Abs(sample.Steering) >= HeavyBrakeSteerThreshold)
            {
                return new StrategyEvaluation("Heavy brake while steering: trail brake smoothly", ConfidenceLow);
            }

            if (lapsRemaining <= 3)
            {
                return new StrategyEvaluation(
                    $"Plan pit stop: fuel for ~{lapsRemaining} laps remaining",
                    ConfidenceMedium);
            }

            if (lapsRemaining <= PitWindowLaps)
            {
                return new StrategyEvaluation(
                    $"Pit window open: plan stop within ~{lapsRemaining - 1} laps",
                    ConfidenceLow);
            }

            return new StrategyEvaluation("No immediate action", ConfidenceNone);
        }

        public int ProjectLapsRemaining(TelemetrySample sample, double avgLapFuelLiters)
        {
            if (sample == null) return 0;
            if (avgLapFuelLiters <= 0) return 0;
            return (int)Math.Floor(sample.FuelLiters / avgLapFuelLiters);
        }
    }

    public record StrategyEvaluation(string Recommendation, double Confidence);
}
