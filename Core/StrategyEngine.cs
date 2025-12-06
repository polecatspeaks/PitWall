using System;
using System.Threading.Tasks;
using PitWall.Models;
using PitWall.Storage;

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
        private readonly UndercutStrategy _undercutStrategy;
        private readonly IProfileDatabase? _profileDatabase;
        private DriverProfile? _currentProfile;
        private const double TyreThreshold = 30.0; // percent wear remaining trigger
        private const double PitStopDuration = 25.0; // Default pit stop time in seconds

        public StrategyEngine(FuelStrategy fuelStrategy) : this(fuelStrategy, new TyreDegradation(), new TrafficAnalyzer(), null)
        {
        }

        public StrategyEngine(FuelStrategy fuelStrategy, TyreDegradation tyreDegradation) 
            : this(fuelStrategy, tyreDegradation, new TrafficAnalyzer(), null)
        {
        }

        public StrategyEngine(FuelStrategy fuelStrategy, TyreDegradation tyreDegradation, TrafficAnalyzer trafficAnalyzer)
            : this(fuelStrategy, tyreDegradation, trafficAnalyzer, null)
        {
        }

        public StrategyEngine(FuelStrategy fuelStrategy, TyreDegradation tyreDegradation, TrafficAnalyzer trafficAnalyzer, IProfileDatabase? profileDatabase)
        {
            _fuelStrategy = fuelStrategy;
            _tyreDegradation = tyreDegradation;
            _trafficAnalyzer = trafficAnalyzer;
            _undercutStrategy = new UndercutStrategy();
            _profileDatabase = profileDatabase;
        }

        public async Task LoadProfile(string driver, string track, string car)
        {
            if (_profileDatabase != null)
            {
                _currentProfile = await _profileDatabase.GetProfile(driver, track, car);
            }
        }

        public Recommendation GetRecommendation(SimHubTelemetry telemetry)
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

            // Use profile data for improved predictions if available
            double avgFuelPerLap = _currentProfile?.AverageFuelPerLap ?? _fuelStrategy.GetAverageFuelPerLap();
            int lapsRemaining = avgFuelPerLap > 0 
                ? (int)Math.Floor(telemetry.FuelRemaining / avgFuelPerLap)
                : _fuelStrategy.PredictLapsRemaining(telemetry.FuelRemaining);

            // CRITICAL: Fuel below 2 laps - must pit immediately
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

            // WARNING: Tyre critical - pit if any tyre at/below threshold or projected under threshold within 2 laps
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

            // TACTICAL: Check for undercut/overcut opportunities when fuel/tyres are not critical
            var undercutRec = CheckUndercutOpportunity(telemetry, lapsRemaining);
            if (undercutRec != null)
            {
                return undercutRec;
            }

            return new Recommendation
            {
                ShouldPit = false,
                Type = RecommendationType.None,
                Priority = Priority.Info,
                Message = string.Empty
            };
        }

        public void RecordLap(SimHubTelemetry telemetry)
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

        private Recommendation? CheckUndercutOpportunity(SimHubTelemetry telemetry, int lapsRemaining)
        {
            // Need opponent data and at least 5 laps of fuel remaining to consider undercut
            if (telemetry.Opponents == null || telemetry.Opponents.Count == 0 || lapsRemaining < 5)
            {
                return null;
            }

            // Find car directly ahead and behind
            var carAhead = FindCarAhead(telemetry);
            var carBehind = FindCarBehind(telemetry);

            if (carAhead == null && carBehind == null)
            {
                return null; // No cars nearby to race
            }

            // Calculate tyre age and advantage
            int currentTyreLaps = telemetry.CurrentLap; // Simplified: assume tyres from start
            double tyreDegPerLap = _currentProfile?.TypicalTyreDegradation ?? 0.15;
            double freshTyreAdvantage = _undercutStrategy.EstimateFreshTyreAdvantage(currentTyreLaps, tyreDegPerLap);

            // Check undercut opportunity
            if (carAhead != null)
            {
                // GapSeconds is negative for car ahead, take absolute value
                double gapAhead = Math.Abs(carAhead.GapSeconds);
                
                if (gapAhead > 0)
                {
                    var situation = new RaceSituation
                    {
                        GapToCarAhead = gapAhead,
                        PitStopDuration = PitStopDuration,
                        FreshTyreAdvantage = freshTyreAdvantage,
                        CurrentTyreLaps = currentTyreLaps,
                        OpponentTyreAge = carAhead.TyreAge > 0 ? carAhead.TyreAge : currentTyreLaps
                    };

                    if (_undercutStrategy.CanUndercut(situation))
                    {
                        int positionGain = _undercutStrategy.CalculatePositionGain(situation);
                        return new Recommendation
                        {
                            ShouldPit = true,
                            Type = RecommendationType.Undercut,
                            Priority = Priority.Warning,
                            Message = $"Box for undercut - can gain P{telemetry.PlayerPosition - positionGain}"
                        };
                    }
                }
            }

            // Check overcut opportunity
            if (carBehind != null)
            {
                // GapSeconds is positive for car behind
                double gapBehind = Math.Abs(carBehind.GapSeconds);
                
                if (gapBehind > 0)
                {
                    var situation = new RaceSituation
                    {
                        GapToCarBehind = gapBehind,
                        PitStopDuration = PitStopDuration,
                        FreshTyreAdvantage = freshTyreAdvantage,
                        CurrentTyreLaps = currentTyreLaps,
                        OpponentTyreAge = carBehind.TyreAge > 0 ? carBehind.TyreAge : currentTyreLaps
                    };

                    if (_undercutStrategy.CanOvercut(situation))
                    {
                        return new Recommendation
                        {
                            ShouldPit = false,
                            Type = RecommendationType.Overcut,
                            Priority = Priority.Info,
                            Message = $"Stay out - defend P{telemetry.PlayerPosition} with overcut"
                        };
                    }
                }
            }

            return null;
        }

        private OpponentData? FindCarAhead(SimHubTelemetry telemetry)
        {
            if (telemetry.Opponents == null || telemetry.PlayerPosition <= 1)
            {
                return null; // Already in P1
            }

            // Find opponent in position directly ahead
            foreach (var opponent in telemetry.Opponents)
            {
                if (opponent.Position == telemetry.PlayerPosition - 1)
                {
                    return opponent;
                }
            }

            return null;
        }

        private OpponentData? FindCarBehind(SimHubTelemetry telemetry)
        {
            if (telemetry.Opponents == null)
            {
                return null;
            }

            // Find opponent in position directly behind
            foreach (var opponent in telemetry.Opponents)
            {
                if (opponent.Position == telemetry.PlayerPosition + 1)
                {
                    return opponent;
                }
            }

            return null;
        }
    }
}
