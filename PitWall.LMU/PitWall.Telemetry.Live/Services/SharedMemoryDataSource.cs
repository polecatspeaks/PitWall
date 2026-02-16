using System;
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

            return new TelemetrySnapshot
            {
                Timestamp = sample.Timestamp,
                SessionId = _sessionId!,
                Session = new SessionInfo
                {
                    StartTimeUtc = sample.Timestamp,
                    NumVehicles = 1
                },
                PlayerVehicle = playerVehicle,
                AllVehicles = { playerVehicle },
                Scoring = new ScoringInfo
                {
                    NumVehicles = 1,
                    SectorFlags = new int[3]
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
                PosZ = sample.Longitude,
                ElapsedTime = 0
            };

            // Map tyre temps to wheel data
            MapTyreTemps(vehicle.Wheels, sample.TyreTempsC);

            return vehicle;
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

        /// <inheritdoc />
        public void Dispose()
        {
            // We don't own the ISharedMemoryReader — caller manages its lifetime.
            // Nothing to dispose here.
        }
    }
}
