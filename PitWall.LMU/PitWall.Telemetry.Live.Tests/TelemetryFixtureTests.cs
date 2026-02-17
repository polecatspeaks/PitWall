using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using PitWall.Telemetry.Live.Models;
using PitWall.Telemetry.Live.Services;
using Xunit;

namespace PitWall.Telemetry.Live.Tests
{
    /// <summary>
    /// Tests using JSON fixture files for realistic telemetry scenarios (#30).
    /// Validates deserialization and exercises event detectors with real-world data shapes.
    /// </summary>
    public class TelemetryFixtureTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        #region Fixture Loading Helper

        /// <summary>
        /// Loads a single TelemetrySnapshot from an embedded JSON fixture resource.
        /// </summary>
        private static TelemetrySnapshot LoadFixture(string fixtureName)
        {
            var json = LoadFixtureJson(fixtureName);
            var snapshot = JsonSerializer.Deserialize<TelemetrySnapshot>(json, JsonOptions);
            Assert.NotNull(snapshot);

            // Ensure AllVehicles contains the player vehicle (fixtures leave it empty for brevity)
            if (snapshot!.PlayerVehicle != null && snapshot.AllVehicles.Count == 0)
                snapshot.AllVehicles.Add(snapshot.PlayerVehicle);

            return snapshot;
        }

        /// <summary>
        /// Loads a sequence of TelemetrySnapshots from an embedded JSON array fixture.
        /// </summary>
        private static List<TelemetrySnapshot> LoadFixtureSequence(string fixtureName)
        {
            var json = LoadFixtureJson(fixtureName);
            var snapshots = JsonSerializer.Deserialize<List<TelemetrySnapshot>>(json, JsonOptions);
            Assert.NotNull(snapshots);

            foreach (var snapshot in snapshots!)
            {
                if (snapshot.PlayerVehicle != null && snapshot.AllVehicles.Count == 0)
                    snapshot.AllVehicles.Add(snapshot.PlayerVehicle);
            }

            return snapshots;
        }

        private static string LoadFixtureJson(string fixtureName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith($".Fixtures.{fixtureName}.json", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(resourceName);

            using var stream = assembly.GetManifestResourceStream(resourceName!);
            Assert.NotNull(stream);

            using var reader = new StreamReader(stream!);
            return reader.ReadToEnd();
        }

        #endregion

        #region Deserialization Tests

        [Fact]
        public void CleanLap_Deserializes_WithAllFields()
        {
            var snapshot = LoadFixture("clean_lap");

            Assert.Equal("fixture-clean-lap", snapshot.SessionId);
            Assert.Equal("Spa-Francorchamps", snapshot.Session?.TrackName);
            Assert.Equal("Porsche 911 GT3 R", snapshot.Session?.CarName);
            Assert.Equal(25, snapshot.Session?.NumVehicles);
            Assert.Equal(7004.0, snapshot.Session?.TrackLength);

            Assert.NotNull(snapshot.PlayerVehicle);
            Assert.Equal(245.6, snapshot.PlayerVehicle!.Speed);
            Assert.Equal(42.5, snapshot.PlayerVehicle.Fuel);
            Assert.Equal(6, snapshot.PlayerVehicle.Gear);
            Assert.Equal(4, snapshot.PlayerVehicle.Wheels.Length);

            Assert.NotNull(snapshot.Scoring);
            Assert.Equal(15, snapshot.Scoring!.Vehicles[0].LapNumber);
            Assert.Equal(3, snapshot.Scoring.Vehicles[0].Place);
            Assert.Equal(138.456, snapshot.Scoring.Vehicles[0].BestLapTime);
        }

        [Fact]
        public void DamageEvent_Deserializes_WithDamageFields()
        {
            var snapshot = LoadFixture("damage_event");

            Assert.Equal("fixture-damage", snapshot.SessionId);
            Assert.NotNull(snapshot.PlayerVehicle);

            // Impact data
            Assert.Equal(1850.5, snapshot.PlayerVehicle!.LastImpactMagnitude);
            Assert.Equal(2112.3, snapshot.PlayerVehicle.LastImpactTime);

            // Dent severity decoded
            Assert.NotNull(snapshot.PlayerVehicle.DentSeverity);
            Assert.Equal(8, snapshot.PlayerVehicle.DentSeverity!.Length);
            Assert.Equal(3, snapshot.PlayerVehicle.DentSeverity[0]); // Front-left heavy damage
            Assert.Equal(2, snapshot.PlayerVehicle.DentSeverity[1]); // Front-right moderate

            // Tire damage
            Assert.True(snapshot.PlayerVehicle.Wheels[0].Flat);      // FL flat
            Assert.True(snapshot.PlayerVehicle.Wheels[3].Detached);  // RR detached
            Assert.False(snapshot.PlayerVehicle.Wheels[1].Flat);     // FR intact
        }

        [Fact]
        public void FlagChange_Deserializes_WithFlagFields()
        {
            var snapshot = LoadFixture("flag_change");

            Assert.Equal("fixture-flag-change", snapshot.SessionId);
            Assert.NotNull(snapshot.Scoring);

            // Sector flags: sector 2 has yellow (11)
            Assert.Equal(2, snapshot.Scoring!.SectorFlags[0]);
            Assert.Equal(11, snapshot.Scoring.SectorFlags[1]);
            Assert.Equal(2, snapshot.Scoring.SectorFlags[2]);

            // Global yellow
            Assert.Equal(1, snapshot.Scoring.YellowFlagState);

            // Vehicle flag (blue flag = 6)
            Assert.Equal(6, snapshot.Scoring.Vehicles[0].Flag);
        }

        [Fact]
        public void MultiCar_Deserializes_25Vehicles()
        {
            var snapshot = LoadFixture("multi_car");

            Assert.Equal("fixture-multi-car", snapshot.SessionId);
            Assert.NotNull(snapshot.Scoring);
            Assert.Equal(25, snapshot.Scoring!.Vehicles.Count);

            // Verify positions
            var leader = snapshot.Scoring.Vehicles.First(v => v.Place == 1);
            Assert.Equal(0, leader.VehicleId);

            // Multi-class: GT3 and GT4
            var classes = snapshot.Scoring.Vehicles.Select(v => v.VehicleClass).Distinct().ToList();
            Assert.Contains("GT3", classes);
            Assert.Contains("GT4", classes);

            // One vehicle in pit (VehicleId 6, PitState 2)
            var pitVehicle = snapshot.Scoring.Vehicles.First(v => v.VehicleId == 6);
            Assert.Equal(2, pitVehicle.PitState);

            // One vehicle with blue flag (VehicleId 13)
            var blueFlagVehicle = snapshot.Scoring.Vehicles.First(v => v.VehicleId == 13);
            Assert.Equal(6, blueFlagVehicle.Flag);

            // DNF vehicle (VehicleId 24, lap 7)
            var dnfVehicle = snapshot.Scoring.Vehicles.First(v => v.VehicleId == 24);
            Assert.Equal(7, dnfVehicle.LapNumber);
        }

        [Fact]
        public void PitStopSequence_Deserializes_AllSteps()
        {
            var sequence = LoadFixtureSequence("pit_stop_sequence");

            Assert.Equal(4, sequence.Count);

            // Step 0: pre-pit (PitState=0, fuel=12.5, worn tires)
            Assert.Equal(0, sequence[0].Scoring!.Vehicles[0].PitState);
            Assert.Equal(12.5, sequence[0].PlayerVehicle!.Fuel);
            Assert.Equal(0.72, sequence[0].PlayerVehicle.Wheels[0].Wear);

            // Step 1: entering pit (PitState=2)
            Assert.Equal(2, sequence[1].Scoring!.Vehicles[0].PitState);

            // Step 2: stopped in pit (PitState=4, speed=0)
            Assert.Equal(4, sequence[2].Scoring!.Vehicles[0].PitState);
            Assert.Equal(0.0, sequence[2].PlayerVehicle!.Speed);

            // Step 3: exit (PitState=0, fresh tires, full fuel)
            Assert.Equal(0, sequence[3].Scoring!.Vehicles[0].PitState);
            Assert.Equal(75.0, sequence[3].PlayerVehicle!.Fuel);
            Assert.Equal(1.0, sequence[3].PlayerVehicle.Wheels[0].Wear);
        }

        #endregion

        #region Detector Integration with Fixtures

        [Fact]
        public void DamageDetector_WithDamageFixture_DetectsAllDamage()
        {
            var detector = new DamageDetector();

            // First call with clean state to initialize
            var cleanSnap = LoadFixture("clean_lap");
            detector.Detect(cleanSnap);

            // Second call with damage fixture
            var damageSnap = LoadFixture("damage_event");
            var events = detector.Detect(damageSnap);

            // Should detect: 1 damage event (impact), 1 flat tire (FL), 1 detached wheel (RR)
            Assert.Equal(3, events.Count);

            var damageEvent = events.First(e => e.EventType == "damage");
            Assert.Contains("\"severity\":\"serious\"", damageEvent.EventDataJson);
            Assert.Contains("1850.5", damageEvent.EventDataJson);

            var flatEvent = events.First(e => e.EventType == "flat_tire");
            Assert.Contains("\"wheel\":\"FL\"", flatEvent.EventDataJson);

            var detachedEvent = events.First(e => e.EventType == "wheel_detached");
            Assert.Contains("\"wheel\":\"RR\"", detachedEvent.EventDataJson);
        }

        [Fact]
        public void FlagDetector_WithFlagFixture_DetectsChanges()
        {
            var detector = new FlagChangeDetector();

            // Initialize with clean state (all green)
            var cleanSnap = LoadFixture("clean_lap");
            detector.Detect(cleanSnap);

            // Detect flag changes
            var flagSnap = LoadFixture("flag_change");
            var events = detector.Detect(flagSnap);

            // Should detect: sector 2 change (2→11), global yellow (0→1), vehicle flag (0→6)
            Assert.Equal(3, events.Count);
            Assert.All(events, e => Assert.Equal("flag_change", e.EventType));

            var sectorEvent = events.First(e => e.EventDataJson.Contains("\"flag_type\":\"sector\""));
            Assert.Contains("\"sector\":2", sectorEvent.EventDataJson);
            Assert.Contains("\"new_state\":11", sectorEvent.EventDataJson);

            var yellowEvent = events.First(e => e.EventDataJson.Contains("\"flag_type\":\"yellow_flag\""));
            Assert.Contains("\"new_state\":1", yellowEvent.EventDataJson);

            var vehicleEvent = events.First(e => e.EventDataJson.Contains("\"flag_type\":\"vehicle\""));
            Assert.Contains("\"new_state\":6", vehicleEvent.EventDataJson);
        }

        [Fact]
        public void PitDetector_WithPitSequence_DetectsEntryAndExit()
        {
            var detector = new PitStopDetector();
            var sequence = LoadFixtureSequence("pit_stop_sequence");

            // Step 0: initialize (no events)
            var events0 = detector.Detect(sequence[0]);
            Assert.Empty(events0);

            // Step 1: pit entry (PitState 0 → 2)
            var events1 = detector.Detect(sequence[1]);
            Assert.Single(events1);
            Assert.Equal("pit_entry", events1[0].EventType);

            // Step 2: stopped in pit (PitState 2 → 4, no entry/exit event)
            var events2 = detector.Detect(sequence[2]);
            Assert.Empty(events2);

            // Step 3: pit exit (PitState 4 → 0)
            var events3 = detector.Detect(sequence[3]);
            Assert.Single(events3);
            Assert.Equal("pit_exit", events3[0].EventType);
        }

        [Fact]
        public void LapDetector_WithPitSequence_DetectsLapTransition()
        {
            var detector = new LapTransitionDetector();
            var sequence = LoadFixtureSequence("pit_stop_sequence");

            // Steps 0-2: same lap (18), no lap event
            detector.Detect(sequence[0]);
            Assert.Empty(detector.Detect(sequence[1]));
            Assert.Empty(detector.Detect(sequence[2]));

            // Step 3: lap 18→19, should detect lap complete
            var events = detector.Detect(sequence[3]);
            Assert.Single(events);
            Assert.Equal("lap_complete", events[0].EventType);
            Assert.Contains("\"lap_number\":18", events[0].EventDataJson);
        }

        [Fact]
        public void MultiCar_Fixture_HasRealisticFieldDistribution()
        {
            var snapshot = LoadFixture("multi_car");
            var vehicles = snapshot.Scoring!.Vehicles;

            // Verify lap distribution (leaders on lap 10, backmarkers on 9, DNF on 7)
            var laps = vehicles.Select(v => v.LapNumber).Distinct().OrderBy(l => l).ToList();
            Assert.Contains(10, laps);
            Assert.Contains(9, laps);
            Assert.Contains(7, laps); // DNF

            // Verify pit states (most 0, one entering=2, one stopped=4)
            var pitStates = vehicles.Select(v => v.PitState).Distinct().OrderBy(p => p).ToList();
            Assert.Contains(0, pitStates);
            Assert.Contains(2, pitStates);
            Assert.Contains(4, pitStates);

            // Verify flags (most 0, one has 6=blue)
            var flags = vehicles.Select(v => v.Flag).Distinct().ToList();
            Assert.Contains(0, flags);
            Assert.Contains(6, flags);
        }

        #endregion
    }
}
