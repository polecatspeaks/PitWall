using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Core.Models;
using PitWall.Core.Services;
using PitWall.Telemetry.Live.Models;

namespace PitWall.Telemetry.Live.Services
{
    /// <summary>
    /// Bridges <see cref="ISharedMemoryReader"/> (PitWall.Core) to
    /// <see cref="ITelemetryDataSource"/> (PitWall.Telemetry.Live).
    /// Reads from LMU shared memory and converts the simple
    /// <see cref="TelemetrySample"/> into a rich <see cref="TelemetrySnapshot"/>.
    /// </summary>
    public class SharedMemoryDataSource : ITelemetryDataSource, IDisposable
    {
        private readonly ISharedMemoryReader _reader;
        private readonly ILogger<SharedMemoryDataSource> _logger;
        private string? _sessionId;

        /// <summary>
        /// Create a SharedMemoryDataSource wrapping the given shared memory reader.
        /// </summary>
        public SharedMemoryDataSource(
            ISharedMemoryReader reader,
            ILogger<SharedMemoryDataSource>? logger = null)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _logger = logger ?? NullLogger<SharedMemoryDataSource>.Instance;
        }

        /// <inheritdoc />
        public bool IsAvailable() => _reader.IsConnected;

        /// <inheritdoc />
        public Task<TelemetrySnapshot?> ReadSnapshotAsync()
        {
            var sample = _reader.GetLatestTelemetry();
            if (sample is null)
            {
                _logger.LogDebug("No telemetry sample available from shared memory");
                return Task.FromResult<TelemetrySnapshot?>(null);
            }

            // Ensure stable session ID across reads
            _sessionId ??= $"lmu_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}";

            var snapshot = MapToSnapshot(sample);
            return Task.FromResult<TelemetrySnapshot?>(snapshot);
        }

        /// <summary>
        /// Map a <see cref="TelemetrySample"/> to a <see cref="TelemetrySnapshot"/>.
        /// </summary>
        private TelemetrySnapshot MapToSnapshot(TelemetrySample sample)
        {
            var playerVehicle = MapPlayerVehicle(sample);
            var playerScoring = MapPlayerScoring(sample);
            var allVehicles = new List<VehicleTelemetry> { playerVehicle };
            var scoringVehicles = new List<VehicleScoringInfo> { playerScoring };

            // Map other vehicles if present
            if (sample.OtherVehicles is { Count: > 0 })
            {
                foreach (var other in sample.OtherVehicles)
                {
                    allVehicles.Add(MapOtherVehicle(other));
                    scoringVehicles.Add(MapOtherScoring(other));
                }
            }

            var numVehicles = sample.NumVehicles > 0 ? sample.NumVehicles : allVehicles.Count;

            return new TelemetrySnapshot
            {
                Timestamp = sample.Timestamp,
                SessionId = _sessionId!,
                Session = new SessionInfo
                {
                    StartTimeUtc = sample.Timestamp,
                    TrackName = sample.TrackName ?? string.Empty,
                    SessionType = sample.SessionType ?? string.Empty,
                    CarName = sample.VehicleClass ?? string.Empty,
                    TrackLength = sample.TrackLength,
                    NumVehicles = numVehicles
                },
                PlayerVehicle = playerVehicle,
                AllVehicles = allVehicles,
                Scoring = new ScoringInfo
                {
                    NumVehicles = numVehicles,
                    SectorFlags = sample.SectorFlags ?? new int[3],
                    YellowFlagState = sample.YellowFlagState,
                    Vehicles = scoringVehicles
                }
            };
        }

        /// <summary>
        /// Map the core telemetry sample fields to a player <see cref="VehicleTelemetry"/>.
        /// </summary>
        private static VehicleTelemetry MapPlayerVehicle(TelemetrySample sample)
        {
            var vehicle = new VehicleTelemetry
            {
                VehicleId = 0,
                IsPlayer = true,
                Speed = sample.SpeedKph,
                Fuel = sample.FuelLiters,
                Brake = sample.Brake,
                Throttle = sample.Throttle,
                Steering = sample.Steering,
                PosX = sample.Latitude,
                PosY = sample.PosY,
                PosZ = sample.Longitude,
                ElapsedTime = sample.ElapsedTime,
                Rpm = sample.Rpm,
                Gear = sample.Gear,
                LastImpactMagnitude = sample.LastImpactMagnitude,
                LastImpactTime = sample.LastImpactTime,
                DentSeverity = DecodeDentSeverity(sample.DentSeverity)
            };

            // Map tyre temps to wheel data
            MapTyreTemps(vehicle.Wheels, sample.TyreTempsC);
            MapWheelArrays(vehicle.Wheels, sample);

            return vehicle;
        }

        /// <summary>
        /// Map player-specific scoring/timing data.
        /// </summary>
        private static VehicleScoringInfo MapPlayerScoring(TelemetrySample sample)
        {
            return new VehicleScoringInfo
            {
                VehicleId = 0,
                DriverName = sample.DriverName ?? string.Empty,
                VehicleClass = sample.VehicleClass ?? string.Empty,
                BestLapTime = sample.BestLapTime,
                LastLapTime = sample.LastLapTime,
                Place = sample.Place,
                LapNumber = sample.LapNumber,
                LapDistance = sample.LapDistance,
                TimeBehindLeader = sample.TimeBehindLeader,
                TimeBehindNext = sample.TimeBehindNext,
                Flag = sample.Flag,
                PitState = sample.PitState
            };
        }

        /// <summary>
        /// Map a non-player vehicle's data to <see cref="VehicleTelemetry"/>.
        /// </summary>
        private static VehicleTelemetry MapOtherVehicle(VehicleSampleData other)
        {
            return new VehicleTelemetry
            {
                VehicleId = other.VehicleId,
                IsPlayer = false,
                Speed = other.Speed,
                PosX = other.PosX,
                PosY = other.PosY,
                PosZ = other.PosZ
            };
        }

        /// <summary>
        /// Map a non-player vehicle's scoring data.
        /// </summary>
        private static VehicleScoringInfo MapOtherScoring(VehicleSampleData other)
        {
            return new VehicleScoringInfo
            {
                VehicleId = other.VehicleId,
                DriverName = other.DriverName ?? string.Empty,
                VehicleClass = other.VehicleClass ?? string.Empty,
                Place = other.Place,
                BestLapTime = other.BestLapTime,
                LastLapTime = other.LastLapTime,
                LapNumber = other.LapNumber,
                LapDistance = other.LapDistance,
                TimeBehindLeader = other.TimeBehindLeader,
                TimeBehindNext = other.TimeBehindNext,
                Flag = other.Flag,
                PitState = other.PitState
            };
        }

        /// <summary>
        /// Map tyre temperature array from the core sample into wheel data.
        /// Handles null and short arrays gracefully.
        /// </summary>
        private static void MapTyreTemps(WheelData[] wheels, double[]? tyreTemps)
        {
            if (tyreTemps is null) return;

            for (int i = 0; i < Math.Min(tyreTemps.Length, wheels.Length); i++)
            {
                wheels[i].TempMid = tyreTemps[i];
            }
        }

        /// <summary>
        /// Map per-wheel condition arrays (wear, pressure, flat, detached, brake temp, suspension)
        /// from the core sample into wheel data. Handles null arrays gracefully.
        /// </summary>
        private static void MapWheelArrays(WheelData[] wheels, TelemetrySample sample)
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                if (sample.TyreWear is { Length: >= 4 })
                    wheels[i].Wear = sample.TyreWear[i];

                if (sample.TyrePressure is { Length: >= 4 })
                    wheels[i].Pressure = sample.TyrePressure[i];

                if (sample.TyreFlat is { Length: >= 4 })
                    wheels[i].Flat = sample.TyreFlat[i];

                if (sample.WheelDetached is { Length: >= 4 })
                    wheels[i].Detached = sample.WheelDetached[i];

                if (sample.BrakeTempsC is { Length: >= 4 })
                    wheels[i].BrakeTemp = sample.BrakeTempsC[i];

                if (sample.SuspDeflection is { Length: >= 4 })
                    wheels[i].SuspDeflection = sample.SuspDeflection[i];
            }
        }

        /// <summary>
        /// Decode a base64-encoded dent severity string into a byte array.
        /// Returns null if the input is null or empty.
        /// </summary>
        private static byte[]? DecodeDentSeverity(string? base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // We don't own the ISharedMemoryReader — caller manages its lifetime.
            // Nothing to dispose here.
        }
    }
}
