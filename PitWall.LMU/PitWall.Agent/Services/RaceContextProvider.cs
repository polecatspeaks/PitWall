using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PitWall.Agent.Models;
using PitWall.Core.Storage;
using PitWall.Strategy;

namespace PitWall.Agent.Services
{
    public class RaceContextProvider : IRaceContextProvider
    {
        private const double DefaultAvgFuelPerLap = 1.8;
        private readonly ITelemetryWriter _writer;
        private readonly StrategyEngine _strategyEngine;

        public RaceContextProvider(ITelemetryWriter writer, StrategyEngine strategyEngine)
        {
            _writer = writer;
            _strategyEngine = strategyEngine;
        }

        public Task<RaceContext> BuildAsync(AgentRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var context = new RaceContext();
            var sessionId = TryGetString(request.Context, "sessionId")
                ?? TryGetString(request.Context, "SessionId");

            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                var samples = _writer.GetSamples(sessionId);
                if (samples.Count > 0)
                {
                    var latest = samples[samples.Count - 1];
                    var evaluation = _strategyEngine.EvaluateWithConfidence(latest);

                    context.FuelLevel = latest.FuelLiters;
                    context.FuelCapacity = TryGetDouble(request.Context, "fuelCapacity") ?? 0.0;
                    context.AvgFuelPerLap = DefaultAvgFuelPerLap;
                    context.FuelLapsRemaining = latest.FuelLiters / DefaultAvgFuelPerLap;
                    context.StrategyConfidence = evaluation.Confidence;
                    context.LastLapTime = TryGetDouble(request.Context, "lastLapTime") ?? 0.0;
                    context.BestLapTime = TryGetDouble(request.Context, "bestLapTime") ?? 0.0;
                    context.GapToAhead = TryGetDouble(request.Context, "gapToAhead") ?? 0.0;
                    context.GapToBehind = TryGetDouble(request.Context, "gapToBehind") ?? 0.0;
                    context.CurrentLap = (int)(TryGetDouble(request.Context, "currentLap") ?? 0.0);
                    context.TotalLaps = (int)(TryGetDouble(request.Context, "totalLaps") ?? 0.0);
                    context.Position = (int)(TryGetDouble(request.Context, "position") ?? 0.0);
                    context.OptimalPitLap = (int)(TryGetDouble(request.Context, "optimalPitLap") ?? 0.0);
                    context.TrackName = TryGetString(request.Context, "trackName") ?? string.Empty;
                    context.CarName = TryGetString(request.Context, "carName") ?? string.Empty;
                    context.CurrentWeather = TryGetString(request.Context, "currentWeather") ?? "Clear";
                    context.TrackTemp = TryGetDouble(request.Context, "trackTemp") ?? 0.0;
                    context.InPitLane = TryGetBool(request.Context, "inPitLane") ?? false;
                    context.AverageTireWear = TryGetDouble(request.Context, "averageTireWear") ?? 0.0;
                    context.TireLapsOnSet = (int)(TryGetDouble(request.Context, "tireLapsOnSet") ?? 0.0);
                }
            }

            return Task.FromResult(context);
        }

        private static string? TryGetString(Dictionary<string, object> context, string key)
        {
            return context.TryGetValue(key, out var value) ? value?.ToString() : null;
        }

        private static double? TryGetDouble(Dictionary<string, object> context, string key)
        {
            if (!context.TryGetValue(key, out var value) || value == null)
                return null;

            return value switch
            {
                double d => d,
                float f => f,
                int i => i,
                long l => l,
                string s when double.TryParse(s, out var result) => result,
                _ => null
            };
        }

        private static bool? TryGetBool(Dictionary<string, object> context, string key)
        {
            if (!context.TryGetValue(key, out var value) || value == null)
                return null;

            return value switch
            {
                bool b => b,
                string s when bool.TryParse(s, out var result) => result,
                _ => null
            };
        }
    }
}
