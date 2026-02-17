using System.Collections.Generic;
using System.Text.Json;
using PitWall.Telemetry.Live.Models;

namespace PitWall.Telemetry.Live.Services
{
    /// <summary>
    /// Detects pit entry and exit events by monitoring VehicleScoringInfo.PitState transitions.
    /// PitState values: 0=none, 1=request, 2=entering, 4=stopped, 5=exiting.
    /// Generates pit_entry when moving from 0 to any pit state, and pit_exit when returning to 0.
    /// </summary>
    public class PitStopDetector : IEventDetector
    {
        private readonly Dictionary<int, int> _lastPitState = new();

        /// <inheritdoc/>
        public IReadOnlyList<TelemetryEvent> Detect(TelemetrySnapshot snapshot)
        {
            var events = new List<TelemetryEvent>();

            if (snapshot.Scoring?.Vehicles == null) return events;

            foreach (var scoring in snapshot.Scoring.Vehicles)
            {
                if (!_lastPitState.TryGetValue(scoring.VehicleId, out var prevPitState))
                {
                    // First time seeing this vehicle — store state, no event
                    _lastPitState[scoring.VehicleId] = scoring.PitState;
                    continue;
                }

                if (scoring.PitState == prevPitState)
                    continue;

                // Detect pit entry: was not in pit (0), now entering (1, 2, 4, 5)
                if (prevPitState == 0 && scoring.PitState > 0)
                {
                    var eventData = JsonSerializer.Serialize(new
                    {
                        lap = scoring.LapNumber,
                        pit_state = scoring.PitState
                    });

                    events.Add(new TelemetryEvent
                    {
                        SessionId = snapshot.SessionId,
                        VehicleId = scoring.VehicleId,
                        Timestamp = snapshot.Timestamp,
                        EventType = "pit_entry",
                        EventDataJson = eventData
                    });
                }
                // Detect pit exit: was in pit (>0), now back on track (0)
                else if (prevPitState > 0 && scoring.PitState == 0)
                {
                    var eventData = JsonSerializer.Serialize(new
                    {
                        lap = scoring.LapNumber,
                        previous_pit_state = prevPitState
                    });

                    events.Add(new TelemetryEvent
                    {
                        SessionId = snapshot.SessionId,
                        VehicleId = scoring.VehicleId,
                        Timestamp = snapshot.Timestamp,
                        EventType = "pit_exit",
                        EventDataJson = eventData
                    });
                }

                _lastPitState[scoring.VehicleId] = scoring.PitState;
            }

            return events;
        }
    }
}
