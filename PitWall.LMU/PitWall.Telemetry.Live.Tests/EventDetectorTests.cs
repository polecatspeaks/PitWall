using System;
using System.Collections.Generic;
using PitWall.Telemetry.Live.Models;
using PitWall.Telemetry.Live.Services;
using Xunit;

namespace PitWall.Telemetry.Live.Tests
{
    /// <summary>
    /// Tests for LapTransitionDetector (#25), PitStopDetector (#26),
    /// DamageDetector (#27), and FlagChangeDetector (#28).
    /// </summary>
    public class EventDetectorTests
    {
        #region Helpers

        private static TelemetrySnapshot CreateSnapshot(
            string sessionId = "session-1",
            int vehicleId = 0,
            int lapNumber = 1,
            double lastLapTime = 0,
            double bestLapTime = 0,
            double fuel = 50.0,
            int pitState = 0,
            byte[]? dentSeverity = null,
            double lastImpactMagnitude = 0,
            double lastImpactTime = 0,
            bool flatFL = false, bool flatFR = false, bool flatRL = false, bool flatRR = false,
            bool detachedFL = false, bool detachedFR = false, bool detachedRL = false, bool detachedRR = false,
            int[]? sectorFlags = null,
            int yellowFlagState = 0,
            int vehicleFlag = 0)
        {
            var snapshot = new TelemetrySnapshot
            {
                Timestamp = DateTime.UtcNow,
                SessionId = sessionId,
                PlayerVehicle = new VehicleTelemetry
                {
                    VehicleId = vehicleId,
                    IsPlayer = true,
                    Fuel = fuel,
                    DentSeverity = dentSeverity,
                    LastImpactMagnitude = lastImpactMagnitude,
                    LastImpactTime = lastImpactTime,
                    Wheels = new[]
                    {
                        new WheelData { Flat = flatFL, Detached = detachedFL },
                        new WheelData { Flat = flatFR, Detached = detachedFR },
                        new WheelData { Flat = flatRL, Detached = detachedRL },
                        new WheelData { Flat = flatRR, Detached = detachedRR }
                    }
                },
                Scoring = new ScoringInfo
                {
                    SectorFlags = sectorFlags ?? new[] { 0, 0, 0 },
                    YellowFlagState = yellowFlagState,
                    Vehicles = new List<VehicleScoringInfo>
                    {
                        new VehicleScoringInfo
                        {
                            VehicleId = vehicleId,
                            LapNumber = lapNumber,
                            LastLapTime = lastLapTime,
                            BestLapTime = bestLapTime,
                            Flag = vehicleFlag,
                            PitState = pitState
                        }
                    }
                }
            };

            snapshot.AllVehicles.Add(snapshot.PlayerVehicle);
            return snapshot;
        }

        private static TelemetrySnapshot AddVehicle(
            TelemetrySnapshot snapshot,
            int vehicleId,
            int lapNumber,
            int pitState = 0,
            int flag = 0,
            double lastLapTime = 0)
        {
            var vehicle = new VehicleTelemetry
            {
                VehicleId = vehicleId,
                IsPlayer = false
            };
            snapshot.AllVehicles.Add(vehicle);
            snapshot.Scoring!.Vehicles.Add(new VehicleScoringInfo
            {
                VehicleId = vehicleId,
                LapNumber = lapNumber,
                PitState = pitState,
                Flag = flag,
                LastLapTime = lastLapTime
            });
            return snapshot;
        }

        #endregion

        #region #25: LapTransitionDetector

        [Fact]
        public void LapDetector_FirstSnapshot_NoEvents()
        {
            var detector = new LapTransitionDetector();
            var snapshot = CreateSnapshot(lapNumber: 1);

            var events = detector.Detect(snapshot);

            Assert.Empty(events);
        }

        [Fact]
        public void LapDetector_SameLap_NoEvents()
        {
            var detector = new LapTransitionDetector();
            detector.Detect(CreateSnapshot(lapNumber: 3));

            var events = detector.Detect(CreateSnapshot(lapNumber: 3));

            Assert.Empty(events);
        }

        [Fact]
        public void LapDetector_LapIncrement_GeneratesLapComplete()
        {
            var detector = new LapTransitionDetector();
            detector.Detect(CreateSnapshot(lapNumber: 2, fuel: 48.0));

            var events = detector.Detect(CreateSnapshot(
                lapNumber: 3,
                lastLapTime: 92.456,
                bestLapTime: 91.200,
                fuel: 45.5));

            Assert.Single(events);
            var ev = events[0];
            Assert.Equal("lap_complete", ev.EventType);
            Assert.Equal("session-1", ev.SessionId);
            Assert.Equal(0, ev.VehicleId);
            Assert.Contains("92.456", ev.EventDataJson);
            Assert.Contains("\"lap_number\":2", ev.EventDataJson);
        }

        [Fact]
        public void LapDetector_MultipleLapTransitions_GeneratesMultipleEvents()
        {
            var detector = new LapTransitionDetector();
            detector.Detect(CreateSnapshot(lapNumber: 1));

            detector.Detect(CreateSnapshot(lapNumber: 2, lastLapTime: 93.0));
            var events = detector.Detect(CreateSnapshot(lapNumber: 3, lastLapTime: 91.5));

            Assert.Single(events);
            Assert.Contains("\"lap_number\":2", events[0].EventDataJson);
        }

        [Fact]
        public void LapDetector_MultipleVehicles_TracksEachSeparately()
        {
            var detector = new LapTransitionDetector();
            var snap1 = CreateSnapshot(lapNumber: 5);
            AddVehicle(snap1, vehicleId: 1, lapNumber: 4);
            detector.Detect(snap1);

            var snap2 = CreateSnapshot(lapNumber: 6, lastLapTime: 90.0);
            AddVehicle(snap2, vehicleId: 1, lapNumber: 4);
            var events = detector.Detect(snap2);

            // Only player (vehicle 0) transitions
            Assert.Single(events);
            Assert.Equal(0, events[0].VehicleId);
        }

        [Fact]
        public void LapDetector_LapDecrement_NoEvent_SessionReset()
        {
            var detector = new LapTransitionDetector();
            detector.Detect(CreateSnapshot(lapNumber: 10));

            // Lap goes backwards (session reset / teleport)
            var events = detector.Detect(CreateSnapshot(lapNumber: 1));

            Assert.Empty(events);
        }

        [Fact]
        public void LapDetector_LapJumpsMultiple_SingleEvent()
        {
            var detector = new LapTransitionDetector();
            detector.Detect(CreateSnapshot(lapNumber: 3));

            // Lap jumps from 3 to 6 (reconnect scenario)
            var events = detector.Detect(CreateSnapshot(lapNumber: 6, lastLapTime: 89.0));

            // Should only generate one event for the most recent completed lap
            Assert.Single(events);
            Assert.Contains("\"lap_number\":5", events[0].EventDataJson);
        }

        [Fact]
        public void LapDetector_IncludesFuelData()
        {
            var detector = new LapTransitionDetector();
            detector.Detect(CreateSnapshot(lapNumber: 1, fuel: 50.0));

            var events = detector.Detect(CreateSnapshot(
                lapNumber: 2, lastLapTime: 95.0, fuel: 47.5));

            Assert.Single(events);
            Assert.Contains("fuel_at_start", events[0].EventDataJson);
            Assert.Contains("fuel_at_end", events[0].EventDataJson);
        }

        #endregion

        #region #26: PitStopDetector

        [Fact]
        public void PitDetector_FirstSnapshot_NoEvents()
        {
            var detector = new PitStopDetector();
            var snapshot = CreateSnapshot(pitState: 0);

            var events = detector.Detect(snapshot);

            Assert.Empty(events);
        }

        [Fact]
        public void PitDetector_NoPitStateChange_NoEvents()
        {
            var detector = new PitStopDetector();
            detector.Detect(CreateSnapshot(pitState: 0));

            var events = detector.Detect(CreateSnapshot(pitState: 0));

            Assert.Empty(events);
        }

        [Fact]
        public void PitDetector_PitEntry_GeneratesPitEntryEvent()
        {
            var detector = new PitStopDetector();
            detector.Detect(CreateSnapshot(pitState: 0));

            // PitState transitions: 0=none → 2=entering
            var events = detector.Detect(CreateSnapshot(pitState: 2, lapNumber: 5));

            Assert.Single(events);
            Assert.Equal("pit_entry", events[0].EventType);
            Assert.Contains("\"lap\":5", events[0].EventDataJson);
        }

        [Fact]
        public void PitDetector_PitExit_GeneratesPitExitEvent()
        {
            var detector = new PitStopDetector();
            detector.Detect(CreateSnapshot(pitState: 4)); // stopped in pit

            // PitState transitions: 4=stopped → 0=none (exited)
            var events = detector.Detect(CreateSnapshot(pitState: 0, lapNumber: 6));

            Assert.Single(events);
            Assert.Equal("pit_exit", events[0].EventType);
            Assert.Contains("\"lap\":6", events[0].EventDataJson);
        }

        [Fact]
        public void PitDetector_PitStopSequence_GeneratesEntryAndExit()
        {
            var detector = new PitStopDetector();
            detector.Detect(CreateSnapshot(pitState: 0, lapNumber: 5));

            // Enter pit
            var entryEvents = detector.Detect(CreateSnapshot(pitState: 2, lapNumber: 5));
            Assert.Single(entryEvents);
            Assert.Equal("pit_entry", entryEvents[0].EventType);

            // In pit (stopped)
            var stoppedEvents = detector.Detect(CreateSnapshot(pitState: 4, lapNumber: 5));
            Assert.Empty(stoppedEvents); // No event for intermediate states

            // Exit pit
            var exitEvents = detector.Detect(CreateSnapshot(pitState: 0, lapNumber: 6));
            Assert.Single(exitEvents);
            Assert.Equal("pit_exit", exitEvents[0].EventType);
        }

        [Fact]
        public void PitDetector_MultipleVehicles_TracksEachSeparately()
        {
            var detector = new PitStopDetector();
            var snap1 = CreateSnapshot(pitState: 0);
            AddVehicle(snap1, vehicleId: 1, lapNumber: 3, pitState: 0);
            detector.Detect(snap1);

            // Only vehicle 1 enters pit
            var snap2 = CreateSnapshot(pitState: 0);
            AddVehicle(snap2, vehicleId: 1, lapNumber: 3, pitState: 2);
            var events = detector.Detect(snap2);

            Assert.Single(events);
            Assert.Equal(1, events[0].VehicleId);
            Assert.Equal("pit_entry", events[0].EventType);
        }

        #endregion

        #region #27: DamageDetector

        [Fact]
        public void DamageDetector_FirstSnapshot_NoEvents()
        {
            var detector = new DamageDetector();
            var snapshot = CreateSnapshot();

            var events = detector.Detect(snapshot);

            Assert.Empty(events);
        }

        [Fact]
        public void DamageDetector_MinorContact_GeneratesMinorDamageEvent()
        {
            var detector = new DamageDetector();
            detector.Detect(CreateSnapshot(lastImpactMagnitude: 0, lastImpactTime: 0));

            var events = detector.Detect(CreateSnapshot(
                lastImpactMagnitude: 50,
                lastImpactTime: 10.0));

            Assert.Single(events);
            Assert.Equal("damage", events[0].EventType);
            Assert.Contains("\"severity\":\"minor\"", events[0].EventDataJson);
        }

        [Fact]
        public void DamageDetector_ModerateImpact_GeneratesModerateEvent()
        {
            var detector = new DamageDetector();
            detector.Detect(CreateSnapshot(lastImpactMagnitude: 0, lastImpactTime: 0));

            var events = detector.Detect(CreateSnapshot(
                lastImpactMagnitude: 500,
                lastImpactTime: 15.0));

            Assert.Single(events);
            Assert.Contains("\"severity\":\"moderate\"", events[0].EventDataJson);
        }

        [Fact]
        public void DamageDetector_SeriousCollision_GeneratesSeriousEvent()
        {
            var detector = new DamageDetector();
            detector.Detect(CreateSnapshot(lastImpactMagnitude: 0, lastImpactTime: 0));

            var events = detector.Detect(CreateSnapshot(
                lastImpactMagnitude: 5000,
                lastImpactTime: 20.0));

            Assert.Single(events);
            Assert.Contains("\"severity\":\"serious\"", events[0].EventDataJson);
            Assert.Contains("5000", events[0].EventDataJson);
        }

        [Fact]
        public void DamageDetector_SameImpactTime_NoNewEvent()
        {
            var detector = new DamageDetector();
            detector.Detect(CreateSnapshot(lastImpactMagnitude: 500, lastImpactTime: 10.0));

            // Same impact time = same event, don't duplicate
            var events = detector.Detect(CreateSnapshot(
                lastImpactMagnitude: 500,
                lastImpactTime: 10.0));

            Assert.Empty(events);
        }

        [Fact]
        public void DamageDetector_FlatTire_GeneratesFlatTireEvent()
        {
            var detector = new DamageDetector();
            detector.Detect(CreateSnapshot());

            var events = detector.Detect(CreateSnapshot(flatFL: true));

            Assert.Single(events);
            Assert.Equal("flat_tire", events[0].EventType);
            Assert.Contains("\"wheel\":\"FL\"", events[0].EventDataJson);
        }

        [Fact]
        public void DamageDetector_MultipleFlatTires_GeneratesMultipleEvents()
        {
            var detector = new DamageDetector();
            detector.Detect(CreateSnapshot());

            var events = detector.Detect(CreateSnapshot(flatFL: true, flatFR: true));

            Assert.Equal(2, events.Count);
            Assert.All(events, e => Assert.Equal("flat_tire", e.EventType));
        }

        [Fact]
        public void DamageDetector_WheelDetached_GeneratesDetachedEvent()
        {
            var detector = new DamageDetector();
            detector.Detect(CreateSnapshot());

            var events = detector.Detect(CreateSnapshot(detachedRL: true));

            Assert.Single(events);
            Assert.Equal("wheel_detached", events[0].EventType);
            Assert.Contains("\"wheel\":\"RL\"", events[0].EventDataJson);
        }

        [Fact]
        public void DamageDetector_DentSeverityDecoded_IncludedInEventData()
        {
            var detector = new DamageDetector();
            detector.Detect(CreateSnapshot(
                dentSeverity: new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 },
                lastImpactMagnitude: 0, lastImpactTime: 0));

            var events = detector.Detect(CreateSnapshot(
                dentSeverity: new byte[] { 3, 0, 0, 0, 2, 0, 0, 0 },
                lastImpactMagnitude: 200,
                lastImpactTime: 25.0));

            Assert.True(events.Count >= 1);
            // Damage event should include dent severity zones
            var damageEvent = events[0];
            Assert.Equal("damage", damageEvent.EventType);
            Assert.Contains("dent_severity", damageEvent.EventDataJson);
        }

        [Fact]
        public void DamageDetector_AlreadyFlat_NoRepeatEvent()
        {
            var detector = new DamageDetector();
            detector.Detect(CreateSnapshot(flatFL: true));

            // Still flat — no new event
            var events = detector.Detect(CreateSnapshot(flatFL: true));

            Assert.Empty(events);
        }

        #endregion

        #region #28: FlagChangeDetector

        [Fact]
        public void FlagDetector_FirstSnapshot_NoEvents()
        {
            var detector = new FlagChangeDetector();
            var snapshot = CreateSnapshot();

            var events = detector.Detect(snapshot);

            Assert.Empty(events);
        }

        [Fact]
        public void FlagDetector_NoChange_NoEvents()
        {
            var detector = new FlagChangeDetector();
            detector.Detect(CreateSnapshot(sectorFlags: new[] { 2, 2, 2 }));

            var events = detector.Detect(CreateSnapshot(sectorFlags: new[] { 2, 2, 2 }));

            Assert.Empty(events);
        }

        [Fact]
        public void FlagDetector_SectorFlagChange_GeneratesEvent()
        {
            var detector = new FlagChangeDetector();
            detector.Detect(CreateSnapshot(sectorFlags: new[] { 2, 2, 2 })); // all green

            var events = detector.Detect(CreateSnapshot(
                sectorFlags: new[] { 2, 11, 2 })); // sector 2 changes

            Assert.Single(events);
            Assert.Equal("flag_change", events[0].EventType);
            Assert.Contains("\"sector\":2", events[0].EventDataJson);
            Assert.Contains("\"old_state\":2", events[0].EventDataJson);
            Assert.Contains("\"new_state\":11", events[0].EventDataJson);
        }

        [Fact]
        public void FlagDetector_MultipleSectorChanges_GeneratesMultipleEvents()
        {
            var detector = new FlagChangeDetector();
            detector.Detect(CreateSnapshot(sectorFlags: new[] { 2, 2, 2 }));

            // Sectors 1 and 3 change
            var events = detector.Detect(CreateSnapshot(
                sectorFlags: new[] { 11, 2, 11 }));

            Assert.Equal(2, events.Count);
        }

        [Fact]
        public void FlagDetector_GlobalYellowChange_GeneratesEvent()
        {
            var detector = new FlagChangeDetector();
            detector.Detect(CreateSnapshot(yellowFlagState: 0));

            var events = detector.Detect(CreateSnapshot(yellowFlagState: 1));

            Assert.Single(events);
            Assert.Equal("flag_change", events[0].EventType);
            Assert.Contains("\"flag_type\":\"yellow_flag\"", events[0].EventDataJson);
            Assert.Contains("\"old_state\":0", events[0].EventDataJson);
            Assert.Contains("\"new_state\":1", events[0].EventDataJson);
        }

        [Fact]
        public void FlagDetector_VehicleFlagChange_GeneratesEvent()
        {
            var detector = new FlagChangeDetector();
            detector.Detect(CreateSnapshot(vehicleFlag: 0));

            var events = detector.Detect(CreateSnapshot(vehicleFlag: 6));

            Assert.Single(events);
            Assert.Equal("flag_change", events[0].EventType);
            Assert.Contains("\"flag_type\":\"vehicle\"", events[0].EventDataJson);
            Assert.Contains("\"vehicle_id\":0", events[0].EventDataJson);
        }

        [Fact]
        public void FlagDetector_SectorAndGlobalChange_GeneratesMultipleEvents()
        {
            var detector = new FlagChangeDetector();
            detector.Detect(CreateSnapshot(
                sectorFlags: new[] { 2, 2, 2 },
                yellowFlagState: 0));

            var events = detector.Detect(CreateSnapshot(
                sectorFlags: new[] { 11, 2, 2 },
                yellowFlagState: 1));

            Assert.Equal(2, events.Count);
        }

        #endregion
    }
}
