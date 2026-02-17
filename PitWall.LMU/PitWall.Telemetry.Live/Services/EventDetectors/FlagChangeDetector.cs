using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PitWall.Telemetry.Live.Models;

namespace PitWall.Telemetry.Live.Services
{
    /// <summary>
    /// Detects flag state changes across sectors, global yellow, and per-vehicle flags.
    /// Monitors ScoringInfo.SectorFlags, ScoringInfo.YellowFlagState, and VehicleScoringInfo.Flag.
    /// </summary>
    public class FlagChangeDetector : IEventDetector
    {
        private int[]? _lastSectorFlags;
        private int _lastYellowFlagState = -1; // -1 = uninitialized
        private readonly Dictionary<int, int> _lastVehicleFlags = new();

        /// <inheritdoc/>
        public IReadOnlyList<TelemetryEvent> Detect(TelemetrySnapshot snapshot)
        {
            var events = new List<TelemetryEvent>();

            if (snapshot.Scoring == null) return events;

            // Sector flag changes
            if (_lastSectorFlags == null)
            {
                // First time — store state, no events
                _lastSectorFlags = snapshot.Scoring.SectorFlags.ToArray();
            }
            else
            {
                for (int i = 0; i < snapshot.Scoring.SectorFlags.Length && i < _lastSectorFlags.Length; i++)
                {
                    if (snapshot.Scoring.SectorFlags[i] != _lastSectorFlags[i])
                    {
                        var eventData = JsonSerializer.Serialize(new
                        {
                            flag_type = "sector",
                            sector = i + 1,
                            old_state = _lastSectorFlags[i],
                            new_state = snapshot.Scoring.SectorFlags[i]
                        });

                        events.Add(new TelemetryEvent
                        {
                            SessionId = snapshot.SessionId,
                            VehicleId = 0, // Global event
                            Timestamp = snapshot.Timestamp,
                            EventType = "flag_change",
                            EventDataJson = eventData
                        });
                    }
                }
                _lastSectorFlags = snapshot.Scoring.SectorFlags.ToArray();
            }

            // Global yellow flag
            if (_lastYellowFlagState == -1)
            {
                _lastYellowFlagState = snapshot.Scoring.YellowFlagState;
            }
            else if (snapshot.Scoring.YellowFlagState != _lastYellowFlagState)
            {
                var eventData = JsonSerializer.Serialize(new
                {
                    flag_type = "yellow_flag",
                    old_state = _lastYellowFlagState,
                    new_state = snapshot.Scoring.YellowFlagState
                });

                events.Add(new TelemetryEvent
                {
                    SessionId = snapshot.SessionId,
                    VehicleId = 0,
                    Timestamp = snapshot.Timestamp,
                    EventType = "flag_change",
                    EventDataJson = eventData
                });

                _lastYellowFlagState = snapshot.Scoring.YellowFlagState;
            }

            // Per-vehicle flag changes
            if (snapshot.Scoring.Vehicles != null)
            {
                foreach (var vehicle in snapshot.Scoring.Vehicles)
                {
                    if (!_lastVehicleFlags.TryGetValue(vehicle.VehicleId, out var prevFlag))
                    {
                        _lastVehicleFlags[vehicle.VehicleId] = vehicle.Flag;
                        continue;
                    }

                    if (vehicle.Flag != prevFlag)
                    {
                        var eventData = JsonSerializer.Serialize(new
                        {
                            flag_type = "vehicle",
                            vehicle_id = vehicle.VehicleId,
                            old_state = prevFlag,
                            new_state = vehicle.Flag
                        });

                        events.Add(new TelemetryEvent
                        {
                            SessionId = snapshot.SessionId,
                            VehicleId = vehicle.VehicleId,
                            Timestamp = snapshot.Timestamp,
                            EventType = "flag_change",
                            EventDataJson = eventData
                        });

                        _lastVehicleFlags[vehicle.VehicleId] = vehicle.Flag;
                    }
                }
            }

            return events;
        }
    }
}
