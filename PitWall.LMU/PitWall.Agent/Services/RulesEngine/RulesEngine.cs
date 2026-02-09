using System;
using PitWall.Agent.Models;

namespace PitWall.Agent.Services.RulesEngine
{
    public class RulesEngine : IRulesEngine
    {
        private readonly QueryPatterns _patterns;

        public RulesEngine()
        {
            _patterns = new QueryPatterns();
        }

        public AgentResponse? TryAnswer(string query, RaceContext context)
        {
            var normalized = query.ToLowerInvariant().Trim();
            var startTime = DateTime.UtcNow;

            if (_patterns.IsFuelQuery(normalized))
            {
                return CreateResponse(
                    HandleFuelQuery(context),
                    "RulesEngine",
                    GetResponseTime(startTime),
                    DetermineConfidence(context.FuelLapsRemaining, 3.0));
            }

            if (_patterns.IsPitQuery(normalized))
            {
                return CreateResponse(
                    HandlePitQuery(context),
                    "RulesEngine",
                    GetResponseTime(startTime),
                    context.StrategyConfidence);
            }

            if (_patterns.IsTireQuery(normalized))
            {
                return CreateResponse(
                    HandleTireQuery(context),
                    "RulesEngine",
                    GetResponseTime(startTime),
                    0.9);
            }

            if (_patterns.IsGapQuery(normalized))
            {
                return CreateResponse(
                    HandleGapQuery(context),
                    "RulesEngine",
                    GetResponseTime(startTime),
                    1.0);
            }

            if (_patterns.IsWeatherQuery(normalized))
            {
                return CreateResponse(
                    HandleWeatherQuery(context),
                    "RulesEngine",
                    GetResponseTime(startTime),
                    0.8);
            }

            if (_patterns.IsPaceQuery(normalized))
            {
                return CreateResponse(
                    HandlePaceQuery(context),
                    "RulesEngine",
                    GetResponseTime(startTime),
                    0.95);
            }

            return null;
        }

        private string HandleFuelQuery(RaceContext ctx)
        {
            var laps = ctx.FuelLapsRemaining;

            if (laps < 1.5)
                return $"FUEL CRITICAL! Only {laps:F1} laps remaining. Box immediately!";

            if (laps < 3.0)
                return $"Low fuel. {laps:F1} laps remaining. Plan to pit soon.";

            return $"{laps:F1} laps of fuel remaining. Consuming {ctx.AvgFuelPerLap:F2} liters per lap.";
        }

        private string HandlePitQuery(RaceContext ctx)
        {
            var currentLap = ctx.CurrentLap;
            var optimalLap = ctx.OptimalPitLap;
            var fuelLaps = ctx.FuelLapsRemaining;

            if (fuelLaps < 1.5 || ctx.AverageTireWear < 15)
            {
                return "BOX THIS LAP! Fuel or tire critical.";
            }

            if (currentLap >= optimalLap && currentLap <= optimalLap + 2)
            {
                return $"Box this lap. Optimal window (Fuel: {fuelLaps:F1} laps, Tires: {ctx.AverageTireWear:F0}%)";
            }

            if (currentLap < optimalLap)
            {
                var lapsToOptimal = optimalLap - currentLap;
                return $"Stay out {lapsToOptimal} more laps, then pit on lap {optimalLap}.";
            }

            return "You should have pitted already. Box this lap if possible.";
        }

        private string HandleTireQuery(RaceContext ctx)
        {
            var wear = ctx.AverageTireWear;
            var laps = ctx.TireLapsOnSet;

            var status = $"Tires: {wear:F0}% remaining, {laps} laps old.";

            if (wear < 20)
                status += " Tires critically worn!";
            else if (wear < 40)
                status += " Tires degrading, plan pit stop soon.";

            return status;
        }

        private string HandleGapQuery(RaceContext ctx)
        {
            var pos = ctx.Position;

            if (pos == 1)
                return $"P{pos} - Leading by {ctx.GapToBehind:F1} seconds.";

            return $"P{pos}. Gap ahead: +{ctx.GapToAhead:F1}s, Gap behind: -{ctx.GapToBehind:F1}s";
        }

        private string HandleWeatherQuery(RaceContext ctx)
        {
            return $"{ctx.CurrentWeather}, Track: {ctx.TrackTemp:F0}C";
        }

        private string HandlePaceQuery(RaceContext ctx)
        {
            var delta = ctx.LastLapTime - ctx.BestLapTime;
            var deltaStr = delta >= 0 ? $"+{delta:F3}" : $"{delta:F3}";

            return $"Last lap: {FormatLapTime(ctx.LastLapTime)}, Best: {FormatLapTime(ctx.BestLapTime)}, Delta: {deltaStr}";
        }

        private static string FormatLapTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return $"{ts.Minutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }

        private static double DetermineConfidence(double value, double criticalThreshold)
        {
            if (value < criticalThreshold)
                return 1.0;
            if (value < criticalThreshold * 1.5)
                return 0.9;
            if (value < criticalThreshold * 2.0)
                return 0.8;
            return 0.7;
        }

        private static AgentResponse CreateResponse(
            string answer,
            string source,
            int responseTime,
            double confidence = 0.8)
        {
            return new AgentResponse
            {
                Answer = answer,
                Source = source,
                Confidence = confidence,
                ResponseTimeMs = responseTime,
                Success = true
            };
        }

        private static int GetResponseTime(DateTime startTime)
        {
            return (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        }
    }
}
