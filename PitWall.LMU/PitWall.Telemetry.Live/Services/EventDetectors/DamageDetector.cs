using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PitWall.Telemetry.Live.Models;

namespace PitWall.Telemetry.Live.Services
{
    /// <summary>
    /// Detects damage events by monitoring impact data, flat tires, and wheel detachments.
    /// Classifies impact severity: minor (&lt;100), moderate (100-1000), serious (&gt;1000).
    /// Tracks tire/wheel state changes to avoid duplicate events.
    /// </summary>
    public class DamageDetector : IEventDetector
    {
        private static readonly string[] WheelNames = { "FL", "FR", "RL", "RR" };

        private readonly Dictionary<int, VehicleDamageState> _vehicleStates = new();

        private class VehicleDamageState
        {
            public double LastImpactTime { get; set; }
            public bool[] WheelFlat { get; set; } = new bool[4];
            public bool[] WheelDetached { get; set; } = new bool[4];
        }

        /// <inheritdoc/>
        public IReadOnlyList<TelemetryEvent> Detect(TelemetrySnapshot snapshot)
        {
            var events = new List<TelemetryEvent>();

            // Process all vehicles with telemetry data
            foreach (var vehicle in snapshot.AllVehicles)
            {
                if (!_vehicleStates.TryGetValue(vehicle.VehicleId, out var prev))
                {
                    // First time — store initial state, no events
                    prev = new VehicleDamageState
                    {
                        LastImpactTime = vehicle.LastImpactTime
                    };
                    for (int i = 0; i < 4 && i < vehicle.Wheels.Length; i++)
                    {
                        prev.WheelFlat[i] = vehicle.Wheels[i].Flat;
                        prev.WheelDetached[i] = vehicle.Wheels[i].Detached;
                    }
                    _vehicleStates[vehicle.VehicleId] = prev;
                    continue;
                }

                // Check for new impact (LastImpactTime changed)
                if (vehicle.LastImpactTime != prev.LastImpactTime && vehicle.LastImpactMagnitude > 0)
                {
                    string severity = vehicle.LastImpactMagnitude switch
                    {
                        < 100 => "minor",
                        < 1000 => "moderate",
                        _ => "serious"
                    };

                    var eventDataObj = new Dictionary<string, object>
                    {
                        ["severity"] = severity,
                        ["magnitude"] = vehicle.LastImpactMagnitude,
                        ["impact_time"] = vehicle.LastImpactTime
                    };

                    // Include dent severity if available
                    if (vehicle.DentSeverity != null && vehicle.DentSeverity.Length > 0)
                    {
                        eventDataObj["dent_severity"] = vehicle.DentSeverity.Select(b => (int)b).ToArray();
                    }

                    events.Add(new TelemetryEvent
                    {
                        SessionId = snapshot.SessionId,
                        VehicleId = vehicle.VehicleId,
                        Timestamp = snapshot.Timestamp,
                        EventType = "damage",
                        EventDataJson = JsonSerializer.Serialize(eventDataObj)
                    });

                    prev.LastImpactTime = vehicle.LastImpactTime;
                }

                // Check for flat tire changes
                for (int i = 0; i < 4 && i < vehicle.Wheels.Length; i++)
                {
                    if (vehicle.Wheels[i].Flat && !prev.WheelFlat[i])
                    {
                        var eventData = JsonSerializer.Serialize(new
                        {
                            wheel = WheelNames[i],
                            vehicle_id = vehicle.VehicleId
                        });

                        events.Add(new TelemetryEvent
                        {
                            SessionId = snapshot.SessionId,
                            VehicleId = vehicle.VehicleId,
                            Timestamp = snapshot.Timestamp,
                            EventType = "flat_tire",
                            EventDataJson = eventData
                        });
                    }
                    prev.WheelFlat[i] = vehicle.Wheels[i].Flat;
                }

                // Check for wheel detachment changes
                for (int i = 0; i < 4 && i < vehicle.Wheels.Length; i++)
                {
                    if (vehicle.Wheels[i].Detached && !prev.WheelDetached[i])
                    {
                        var eventData = JsonSerializer.Serialize(new
                        {
                            wheel = WheelNames[i],
                            vehicle_id = vehicle.VehicleId
                        });

                        events.Add(new TelemetryEvent
                        {
                            SessionId = snapshot.SessionId,
                            VehicleId = vehicle.VehicleId,
                            Timestamp = snapshot.Timestamp,
                            EventType = "wheel_detached",
                            EventDataJson = eventData
                        });
                    }
                    prev.WheelDetached[i] = vehicle.Wheels[i].Detached;
                }
            }

            return events;
        }
    }
}
