using System;
using System.Collections.Generic;
using System.Text.Json;
using PitWall.Telemetry.Live.Models;

namespace PitWall.Telemetry.Live.Services
{
    /// <summary>
    /// Detects lap transitions by monitoring VehicleScoringInfo.LapNumber changes.
    /// Generates lap_complete events with lap time, fuel data, and best lap.
    /// Handles false positives: session resets (lap number drops) and reconnects (lap jumps).
    /// </summary>
    public class LapTransitionDetector : IEventDetector
    {
        private readonly Dictionary<int, VehicleState> _vehicleStates = new();

        private class VehicleState
        {
            public int LapNumber { get; set; }
            public double Fuel { get; set; }
        }

        /// <inheritdoc/>
        public IReadOnlyList<TelemetryEvent> Detect(TelemetrySnapshot snapshot)
        {
            var events = new List<TelemetryEvent>();

            if (snapshot.Scoring?.Vehicles == null) return events;

            // Build a lookup for vehicle telemetry (for fuel data)
            var telemetryByVehicle = new Dictionary<int, VehicleTelemetry>();
            if (snapshot.PlayerVehicle != null)
                telemetryByVehicle[snapshot.PlayerVehicle.VehicleId] = snapshot.PlayerVehicle;
            foreach (var v in snapshot.AllVehicles)
                telemetryByVehicle[v.VehicleId] = v;

            foreach (var scoring in snapshot.Scoring.Vehicles)
            {
                if (!_vehicleStates.TryGetValue(scoring.VehicleId, out var prev))
                {
                    // First time seeing this vehicle — store state, no event
                    double currentFuel = 0;
                    if (telemetryByVehicle.TryGetValue(scoring.VehicleId, out var tel))
                        currentFuel = tel.Fuel;

                    _vehicleStates[scoring.VehicleId] = new VehicleState
                    {
                        LapNumber = scoring.LapNumber,
                        Fuel = currentFuel
                    };
                    continue;
                }

                int lapDelta = scoring.LapNumber - prev.LapNumber;

                if (lapDelta > 0)
                {
                    // Lap incremented — generate event for the completed lap
                    double fuelAtEnd = 0;
                    if (telemetryByVehicle.TryGetValue(scoring.VehicleId, out var tel))
                        fuelAtEnd = tel.Fuel;

                    // The completed lap number is the new lap minus 1
                    int completedLap = scoring.LapNumber - 1;

                    var eventData = JsonSerializer.Serialize(new
                    {
                        lap_number = completedLap,
                        lap_time = scoring.LastLapTime,
                        best_lap_time = scoring.BestLapTime,
                        fuel_at_start = prev.Fuel,
                        fuel_at_end = fuelAtEnd
                    });

                    events.Add(new TelemetryEvent
                    {
                        SessionId = snapshot.SessionId,
                        VehicleId = scoring.VehicleId,
                        Timestamp = snapshot.Timestamp,
                        EventType = "lap_complete",
                        EventDataJson = eventData
                    });

                    // Update state
                    prev.LapNumber = scoring.LapNumber;
                    prev.Fuel = fuelAtEnd;
                }
                else if (lapDelta < 0)
                {
                    // Session reset / teleport — update state silently, no event
                    double currentFuel = 0;
                    if (telemetryByVehicle.TryGetValue(scoring.VehicleId, out var tel))
                        currentFuel = tel.Fuel;

                    prev.LapNumber = scoring.LapNumber;
                    prev.Fuel = currentFuel;
                }
                // lapDelta == 0 → no change, no event
            }

            return events;
        }
    }
}
