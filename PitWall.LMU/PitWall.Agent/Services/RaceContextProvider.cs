using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PitWall.Agent.Models;
using PitWall.Core.Storage;
using PitWall.Strategy;
using PitWall.Telemetry.Live.Models;

namespace PitWall.Agent.Services
{
    public class RaceContextProvider : IRaceContextProvider
    {
        private const double DefaultAvgFuelPerLap = 1.8;
        private readonly ITelemetryWriter _writer;
        private readonly StrategyEngine _strategyEngine;
        private readonly ILiveTelemetryProvider? _liveProvider;

        public RaceContextProvider(ITelemetryWriter writer, StrategyEngine strategyEngine, ILiveTelemetryProvider? liveProvider = null)
        {
            _writer = writer;
            _strategyEngine = strategyEngine;
            _liveProvider = liveProvider;
        }

        public Task<RaceContext> BuildAsync(AgentRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Try live telemetry first
            var snapshot = _liveProvider?.GetLatestSnapshot();
            if (snapshot?.PlayerVehicle != null)
            {
                var context = BuildFromLiveSnapshot(snapshot, request);
                return Task.FromResult(context);
            }

            // Fall back to stored telemetry + request dictionary
            var fallbackContext = BuildFromStoredTelemetry(request);
            return Task.FromResult(fallbackContext);
        }

        /// <summary>
        /// Builds RaceContext from a live TelemetrySnapshot, supplemented by request context for fields
        /// not available in the snapshot.
        /// </summary>
        internal RaceContext BuildFromLiveSnapshot(TelemetrySnapshot snapshot, AgentRequest request)
        {
            var player = snapshot.PlayerVehicle!;
            var scoring = snapshot.Scoring;
            var session = snapshot.Session;

            // Find player's scoring info
            VehicleScoringInfo? playerScoring = null;
            if (scoring?.Vehicles != null)
            {
                playerScoring = scoring.Vehicles.FirstOrDefault(v => v.VehicleId == player.VehicleId);
            }

            var context = new RaceContext
            {
                IsLiveData = true,
                TrackName = session?.TrackName ?? TryGetString(request.Context, "trackName") ?? string.Empty,
                CarName = session?.CarName ?? TryGetString(request.Context, "carName") ?? string.Empty,
                FuelLevel = player.Fuel,
                AvgFuelPerLap = DefaultAvgFuelPerLap,
                FuelLapsRemaining = player.Fuel / DefaultAvgFuelPerLap,
                FuelCapacity = TryGetDouble(request.Context, "fuelCapacity") ?? 0.0,

                // Per-wheel live data
                Speed = player.Speed,
                Rpm = player.Rpm,
                Gear = player.Gear,
                TireTemps = player.Wheels.Select(w => w.TempMid).ToArray(),
                TireWear = player.Wheels.Select(w => w.Wear).ToArray(),
                BrakeTemps = player.Wheels.Select(w => w.BrakeTemp).ToArray(),
                AverageTireWear = player.Wheels.Average(w => w.Wear),

                // Damage
                DamageLevel = player.LastImpactMagnitude,
            };

            // Scoring-derived fields
            if (playerScoring != null)
            {
                context.Position = playerScoring.Place;
                context.CurrentLap = playerScoring.LapNumber;
                context.LastLapTime = playerScoring.LastLapTime;
                context.BestLapTime = playerScoring.BestLapTime;
                context.GapToAhead = playerScoring.TimeBehindNext;
                context.GapToBehind = FindGapBehind(scoring!, playerScoring);
                context.InPitLane = playerScoring.PitState != 0;
            }

            // Scoring-level fields
            if (scoring != null)
            {
                context.YellowFlagState = scoring.YellowFlagState;
            }

            // Fields only available from request context
            context.TotalLaps = (int)(TryGetDouble(request.Context, "totalLaps") ?? 0.0);
            context.OptimalPitLap = (int)(TryGetDouble(request.Context, "optimalPitLap") ?? 0.0);
            context.TireLapsOnSet = (int)(TryGetDouble(request.Context, "tireLapsOnSet") ?? 0.0);
            context.CurrentWeather = TryGetString(request.Context, "currentWeather") ?? "Clear";
            context.TrackTemp = TryGetDouble(request.Context, "trackTemp") ?? 0.0;

            // Strategy confidence from engine using stored telemetry
            var sessionId = TryGetString(request.Context, "sessionId") ?? TryGetString(request.Context, "SessionId");
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                var samples = _writer.GetSamples(sessionId);
                if (samples.Count > 0)
                {
                    var evaluation = _strategyEngine.EvaluateWithConfidence(samples[samples.Count - 1]);
                    context.StrategyConfidence = evaluation.Confidence;
                }
            }

            return context;
        }

        /// <summary>
        /// Finds the gap of the car behind the player.
        /// </summary>
        private static double FindGapBehind(ScoringInfo scoring, VehicleScoringInfo playerScoring)
        {
            var carBehind = scoring.Vehicles
                .Where(v => v.Place == playerScoring.Place + 1)
                .FirstOrDefault();
            return carBehind?.TimeBehindNext ?? 0.0;
        }

        private RaceContext BuildFromStoredTelemetry(AgentRequest request)
        {
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

            return context;
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
