# LMU Telemetry JSON Schema Reference

> **Source**: 2.44 GB telemetry capture from Le Mans Ultimate — 3-lap GT3 race at Monza Curva Grande Circuit with 25 AI cars, deliberate damage and flag conditions.
>
> **Analyzed by**: `PitWall.JsonAnalyzer` — 175 samples across full file, stride 20.
>
> **Date**: 2026-02-15

---

## Overview

| Metric | Value |
|--------|-------|
| **Unique Leaf Fields** | 231 |
| **Max Nesting Depth** | 7 |
| **Vehicle Array Size** | 128 slots (25 active + 103 empty) |
| **Wheels per Vehicle** | 4 |
| **Temperature Zones per Tyre** | 3 (inner / middle / outer) |
| **Orientation Matrix** | 3×3 (3 row-vectors of X/Y/Z) |

### Update Frequency Key

| Frequency | Threshold | Description |
|-----------|-----------|-------------|
| **PerSample** | >80% of samples | Changes every physics tick (~100 Hz) |
| **Frequent** | 10–80% | Changes multiple times per lap |
| **PerLap** | 1–10% | Changes roughly once per lap |
| **Sparse** | <1% | Rare events (pit entry, flag change) |
| **Static** | 0% | Constant for entire session |

---

## Root JSON Structure

```
{
  "session": { ... },              ← depth 1 — recorded once at session start
  "samples": [                     ← array of telemetry snapshots (~100 Hz)
    {
      "TimestampUtc": "...",       ← depth 1
      "Telemetry": { ... },        ← depth 1 — physics/vehicle data
      "Scoring": { ... },          ← depth 1 — timing/position/flags
      "Electronics": { ... }       ← depth 1 — driver aids
    },
    ...
  ]
}
```

### Nesting Depth Map

```
Depth 1: session, TimestampUtc, Telemetry, Scoring, Electronics
Depth 2: Telemetry.Vehicles, Scoring.ScoringInfo, Scoring.Vehicles
Depth 3: Telemetry.Vehicles[i], Scoring.Vehicles[i], ScoringInfo.Wind
Depth 4: Vehicles[i].Position, .LocalVelocity, .LastImpactPosition, .Wheels
Depth 5: Vehicles[i].Wheels[j], Vehicles[i].Orientation[k]
Depth 6: Wheels[j].Temperature, Wheels[j].TyreInnerLayerTemperature
Depth 7: Temperature[z] (inner/mid/outer zone values)  ← MAX DEPTH
```

---

## Fields by Category

### Session / Metadata — 10 fields

| Field | Type | Range / Values | Update Rate | Notes |
|-------|------|----------------|-------------|-------|
| `session.StartTimeUtc` | string | ISO 8601 timestamp | Once | Session recording start |
| `session.SessionType` | string | `10` | Once | Race = 10 |
| `session.TrackName` | string | `Monza Curva Grande Circuit` | Once | Human-readable |
| `session.CarName` | string | `Unknown` | Once | Player car (not always populated) |
| `Scoring.ScoringInfo.Session` | number | 0..10 | PerLap | Session type enum |
| `Scoring.ScoringInfo.NumVehicles` | number | 0..25 | PerLap | Active car count |
| `Scoring.ScoringInfo.TrackName` | string | base64-encoded | PerLap | Raw sim string (padded) |
| `Scoring.Vehicles[*].DriverName` | string | base64-encoded | PerLap | Driver name |
| `Scoring.Vehicles[*].VehicleClass` | string | base64-encoded e.g. `GT3` | PerLap | Class identifier |
| `Telemetry.NumVehicles` | number | 0..25 | PerLap | Active car count |

> **Note**: Many string fields are base64-encoded because LMU exposes raw C structs with fixed-size `char[]` buffers. Decode with `Convert.FromBase64String()` then `Encoding.UTF8.GetString()`, trimming null bytes.

---

### Timing / Lap Data — 24 fields

#### Per-Vehicle Timing

| Field | Type | Range | Update Rate | Notes |
|-------|------|-------|-------------|-------|
| `Telemetry.Vehicles[*].ElapsedTime` | number | 0..1375 s | PerSample | Session elapsed time |
| `Telemetry.Vehicles[*].LapStartTime` | number | 0..1350 s | PerLap | Timestamp lap started |
| `Telemetry.Vehicles[*].Position.X` | number | -317..941 m | PerSample | World X coordinate |
| `Telemetry.Vehicles[*].Position.Y` | number | -5..8 m | PerSample | World Y (elevation) |
| `Telemetry.Vehicles[*].Position.Z` | number | -941..1229 m | PerSample | World Z coordinate |
| `Scoring.Vehicles[*].Position.X/Y/Z` | number | (same ranges) | PerSample | Redundant position |
| `Scoring.Vehicles[*].LapDist` | number | -2..5744 m | PerSample | Distance along centerline |
| `Scoring.Vehicles[*].TimeBehindLeader` | number | 0..103 s | PerSample | Gap to P1 |
| `Scoring.Vehicles[*].TimeBehindNext` | number | -39..101 s | PerSample | Gap to car ahead |
| `Scoring.Vehicles[*].LapsBehindLeader` | number | 0..1 | Sparse | Laps behind leader |
| `Scoring.Vehicles[*].LapsBehindNext` | number | 0..1 | Sparse | Laps behind car ahead |

#### Sector / Lap Times

| Field | Type | Range | Update Rate |
|-------|------|-------|-------------|
| `Scoring.Vehicles[*].BestLapTime` | number | -1..118.4 s | PerLap |
| `Scoring.Vehicles[*].LastLapTime` | number | -1..118.4 s | PerLap |
| `Scoring.Vehicles[*].EstimatedLapTime` | number | 0..104.5 s | PerLap |
| `Scoring.Vehicles[*].BestSector1` | number | -1..27.5 s | PerLap |
| `Scoring.Vehicles[*].BestSector2` | number | -1..94.6 s | PerLap |
| `Scoring.Vehicles[*].LastSector1` | number | -1..27.7 s | PerLap |
| `Scoring.Vehicles[*].LastSector2` | number | -1..94.6 s | PerLap |
| `Scoring.Vehicles[*].CurSector1` | number | -1..27.7 s | PerLap |
| `Scoring.Vehicles[*].CurSector2` | number | -1..94.6 s | PerLap |
| `Scoring.Vehicles[*].BestLapSector1` | number | -1..27.4 s | PerLap |
| `Scoring.Vehicles[*].BestLapSector2` | number | -1..79.6 s | PerLap |

> **Note**: `-1` indicates "not yet recorded" (no valid sector/lap completed).

#### Session Clock

| Field | Type | Range | Update Rate |
|-------|------|-------|-------------|
| `Scoring.ScoringInfo.CurrentET` | number | 0..1375 s | PerSample |
| `Scoring.ScoringInfo.LapDist` | number | 5745 m | PerLap |

---

### Driver Controls — 8 fields

| Field | Type | Range | Update Rate | Notes |
|-------|------|-------|-------------|-------|
| `Telemetry.Vehicles[*].FilteredThrottle` | number | 0..1 | Frequent | 0–100% throttle |
| `Telemetry.Vehicles[*].FilteredBrake` | number | 0..1 | Frequent | 0–100% brake |
| `Telemetry.Vehicles[*].FilteredSteering` | number | -1..1 | Frequent | Full lock left to right |
| `Telemetry.Vehicles[*].UnfilteredThrottle` | number | 0..1 | Frequent | Raw pedal input |
| `Telemetry.Vehicles[*].UnfilteredBrake` | number | 0..1 | Frequent | Raw pedal input |
| `Telemetry.Vehicles[*].UnfilteredSteering` | number | -1..1 | Frequent | Raw wheel input |
| `Telemetry.Vehicles[*].SteeringShaftTorque` | number | -77..76 Nm | Frequent | Force feedback torque |
| `Telemetry.Vehicles[*].Gear` | number | -1..6 | Frequent | -1=R, 0=N, 1–6 |
| `Telemetry.Vehicles[*].VisualSteeringWheelRange` | number | 0..719° | PerLap | Steering lock range |

---

### Engine / Drivetrain — 15 fields

| Field | Type | Range | Update Rate | Notes |
|-------|------|-------|-------------|-------|
| `Telemetry.Vehicles[*].EngineRPM` | number | 0..9018 | PerSample | Current RPM |
| `Telemetry.Vehicles[*].EngineMaxRPM` | number | 0..9400 | PerLap | Redline |
| `Telemetry.Vehicles[*].ClutchRPM` | number | -595..9018 | PerSample | Clutch output RPM |
| `Telemetry.Vehicles[*].TurboBoostPressure` | number | 0..103,307 Pa | PerSample | Turbo pressure |
| `Telemetry.Vehicles[*].FilteredClutch` | number | 0..1 | PerLap | Clutch engagement |
| `Electronics.EngineMap` | number | 0 | Static | Engine map setting |

#### Not Populated (always 0 for AI cars)

| Field | Notes |
|-------|-------|
| `Telemetry.Vehicles[*].EngineTorque` | Only filled for player car |
| `Telemetry.Vehicles[*].EngineOilTemp` | Only filled for player car |
| `Telemetry.Vehicles[*].EngineWaterTemp` | Only filled for player car |
| `Telemetry.Vehicles[*].ElectricBoostMotorRPM` | Hybrid system (inactive on GT3) |
| `Telemetry.Vehicles[*].ElectricBoostMotorTorque` | Hybrid system |
| `Telemetry.Vehicles[*].ElectricBoostMotorTemperature` | Hybrid system |
| `Telemetry.Vehicles[*].ElectricBoostMotorState` | Hybrid system |
| `Telemetry.Vehicles[*].ElectricBoostWaterTemperature` | Hybrid system |
| `Telemetry.Vehicles[*].UnfilteredClutch` | Not exposed |

---

### General Telemetry — Motion Physics — 30 fields

#### Vehicle Dynamics (per car, × 25 active)

| Field | Type | Range | Update Rate |
|-------|------|-------|-------------|
| `Telemetry.Vehicles[*].LocalVelocity.X` | number | -30..44 m/s | PerSample |
| `Telemetry.Vehicles[*].LocalVelocity.Y` | number | -0.8..4.7 m/s | PerSample |
| `Telemetry.Vehicles[*].LocalVelocity.Z` | number | -77..12 m/s | PerSample |
| `Telemetry.Vehicles[*].LocalAcceleration.X` | number | -25..40 m/s² | PerSample |
| `Telemetry.Vehicles[*].LocalAcceleration.Y` | number | -8..19 m/s² | PerSample |
| `Telemetry.Vehicles[*].LocalAcceleration.Z` | number | -28..87 m/s² | PerSample |
| `Telemetry.Vehicles[*].LocalRotationalSpeed.X` | number | -0.22..0.29 rad/s | PerSample |
| `Telemetry.Vehicles[*].LocalRotationalSpeed.Y` | number | -1.8..3.4 rad/s | PerSample |
| `Telemetry.Vehicles[*].LocalRotationalSpeed.Z` | number | -1.1..0.56 rad/s | PerSample |
| `Telemetry.Vehicles[*].LocalRotationalAcceleration.X/Y/Z` | number | varies | PerSample |

> Scoring duplicates: `Scoring.Vehicles[*].LocalVelocity`, `.LocalAcceleration`, `.LocalRotationalAcceleration`, `.LocalRotation` — same data from Scoring plugin.

#### Fuel

| Field | Type | Range | Update Rate | Notes |
|-------|------|-------|-------------|-------|
| `Telemetry.Vehicles[*].Fuel` | number | 0..95 kg | PerSample | Current fuel load |
| `Telemetry.Vehicles[*].FuelCapacity` | number | 0..120 kg | PerLap | Tank capacity |

#### Temperatures

| Field | Type | Range | Update Rate | Notes |
|-------|------|-------|-------------|-------|
| `Scoring.ScoringInfo.AmbientTemp` | number | 0..33°C | Frequent | Air temperature |
| `Scoring.ScoringInfo.TrackTemp` | number | 0..43°C | PerLap | Track surface temp |

#### Brake Data (per wheel, × 4)

| Field | Type | Range | Update Rate |
|-------|------|-------|-------------|
| `Telemetry.Vehicles[*].Wheels[*].BrakeTemp` | number | 0..1184°C | PerSample |
| `Telemetry.Vehicles[*].Wheels[*].BrakePressure` | number | 0..0.525 | Frequent |
| `Telemetry.Vehicles[*].Wheels[*].Pressure` | number | 0..180 kPa | Frequent |
| `Telemetry.Vehicles[*].Wheels[*].Temperature[*]` | number | 0..640°C | Frequent |

> `Temperature[*]` has 3 elements: inner, middle, outer tread zones.

---

### Tyres / Wheels — 10 fields

| Field | Type | Range | Update Rate | Notes |
|-------|------|-------|-------------|-------|
| `Telemetry.Vehicles[*].Wheels[*].Wear` | number | 0..1 | PerSample | 0=new, 1=destroyed |
| `Telemetry.Vehicles[*].Wheels[*].TyreCarcassTemperature` | number | 0..385°C | Frequent | Internal carcass temp |
| `Telemetry.Vehicles[*].Wheels[*].TyreInnerLayerTemperature[*]` | number | 0..395°C | Frequent | 3 zones |
| `Telemetry.Vehicles[*].Wheels[*].VerticalTyreDeflection` | number | -0.001..0.073 m | PerSample | Load deflection |
| `Telemetry.Vehicles[*].Wheels[*].SurfaceType` | number | 0..6 | Frequent | Surface enum |
| `Telemetry.Vehicles[*].FrontTyreCompoundName` | string | base64 e.g. `Medium` | PerLap | Compound name |
| `Telemetry.Vehicles[*].RearTyreCompoundName` | string | base64 e.g. `Medium` | PerLap | Compound name |
| `Telemetry.Vehicles[*].FrontTyreCompoundIndex` | number | 0 | Static | Compound enum |
| `Telemetry.Vehicles[*].RearTyreCompoundIndex` | number | 0 | Static | Compound enum |
| `Telemetry.Vehicles[*].Wheels[*].TyreLoad` | number | 0 | Static | Not populated |

---

### Aero / Suspension — 14 fields

#### Active Data (per wheel, × 4)

| Field | Type | Range | Update Rate |
|-------|------|-------|-------------|
| `Telemetry.Vehicles[*].Wheels[*].SuspensionDeflection` | number | -0.177..0.140 m | PerSample |
| `Telemetry.Vehicles[*].Wheels[*].SuspForce` | number | -1355..19,923 N | PerSample |
| `Telemetry.Vehicles[*].Wheels[*].RideHeight` | number | -0.019..0.225 m | PerSample |

#### Active Data (per car)

| Field | Type | Range | Update Rate |
|-------|------|-------|-------------|
| `Telemetry.Vehicles[*].Front3rdDeflection` | number | 0..0.090 m | Frequent |
| `Telemetry.Vehicles[*].Rear3rdDeflection` | number | 0..0.106 m | Frequent |

#### Not Populated (always 0)

| Field | Notes |
|-------|-------|
| `Telemetry.Vehicles[*].FrontWingHeight` | Not exposed |
| `Telemetry.Vehicles[*].FrontRideHeight` | Use per-wheel RideHeight instead |
| `Telemetry.Vehicles[*].RearRideHeight` | Use per-wheel RideHeight instead |
| `Telemetry.Vehicles[*].Drag` | Aero not exposed |
| `Telemetry.Vehicles[*].FrontDownforce` | Aero not exposed |
| `Telemetry.Vehicles[*].RearDownforce` | Aero not exposed |
| `Telemetry.Vehicles[*].FrontFlapActivated` | DRS state |
| `Telemetry.Vehicles[*].RearFlapActivated` | DRS state |
| `Telemetry.Vehicles[*].RearFlapLegalStatus` | DRS legal zone |

---

### Damage / Impact — 10 fields

| Field | Type | Range | Update Rate | Notes |
|-------|------|-------|-------------|-------|
| `Telemetry.Vehicles[*].LastImpactMagnitude` | number | 0..18,855 | Frequent | Collision force |
| `Telemetry.Vehicles[*].LastImpactTime` | number | 0..875 s | Frequent | When last hit occurred |
| `Telemetry.Vehicles[*].LastImpactPosition.X` | number | -1.05..1.10 | Frequent | Impact point (car-local) |
| `Telemetry.Vehicles[*].LastImpactPosition.Y` | number | -0.28..0.80 | Frequent | Impact point (car-local) |
| `Telemetry.Vehicles[*].LastImpactPosition.Z` | number | -2.54..2.42 | Frequent | Impact point (car-local) |
| `Telemetry.Vehicles[*].DentSeverity` | string | base64 byte array | PerLap | 8-zone body damage |
| `Telemetry.Vehicles[*].Detached` | number | 0 | Static | Body parts detached (bitmask) |
| `Telemetry.Vehicles[*].Overheating` | number | 0 | Static | Engine overheat flag |
| `Telemetry.Vehicles[*].Wheels[*].Detached` | number | 0..1 | Sparse | Wheel ripped off |
| `Telemetry.Vehicles[*].Wheels[*].Flat` | number | 0..1 | PerLap | Puncture |

> **Key insight**: `LastImpactMagnitude` spikes up to ~18,855 units during collisions. Values >1000 indicate serious contact. `DentSeverity` is a base64-encoded byte array with 8 body zones — all zeros = no damage, non-zero = dent severity per zone.

---

### Flags / Penalties — 5 fields

| Field | Type | Values | Update Rate | Notes |
|-------|------|--------|-------------|-------|
| `Scoring.ScoringInfo.SectorFlag[*]` | number | 0, 1, 2, 11 | PerLap | Per-sector flag state (3 sectors) |
| `Scoring.ScoringInfo.YellowFlagState` | number | 0 | Static | Global yellow flag |
| `Scoring.Vehicles[*].Flag` | number | 0, 6 | PerLap | Per-vehicle flag |
| `Scoring.Vehicles[*].CountLapFlag` | number | 0, 1, 2 | PerLap | Counted-lap flag status |
| `Scoring.Vehicles[*].UnderYellow` | number | 0 | Static | Vehicle under yellow |

> **SectorFlag values**: 0 = none, 2 = green, 11 = unknown (possibly yellow/red). `Flag` value 6 = unknown (needs more session data to decode).

---

### Weather / Environment — 9 fields

| Field | Type | Value | Update Rate | Notes |
|-------|------|-------|-------------|-------|
| `Scoring.ScoringInfo.AmbientTemp` | number | 0..33°C | Frequent | Air temperature |
| `Scoring.ScoringInfo.TrackTemp` | number | 0..43°C | PerLap | Surface temperature |
| `Scoring.ScoringInfo.Raining` | number | 0 | Static | Rain state |
| `Scoring.ScoringInfo.DarkCloud` | number | 0 | Static | Cloud cover |
| `Scoring.ScoringInfo.Wind.X/Y/Z` | number | 0 | Static | Wind vector (always 0 in dry) |
| `Scoring.ScoringInfo.MinPathWetness` | number | 0 | Static | Track wetness min |
| `Scoring.ScoringInfo.MaxPathWetness` | number | 0 | Static | Track wetness max |
| `Scoring.ScoringInfo.AvgPathWetness` | number | 0 | Static | Track wetness avg |
| `Telemetry.Vehicles[*].Wheels[*].TerrainName` | string | base64 e.g. `ROAD` | Frequent | Surface under wheel |

---

### Electronics / Assists — 15 fields

| Field | Type | Value | Update Rate | Notes |
|-------|------|-------|-------------|-------|
| `Electronics.TractionControl` | number | -1 | Static | TC setting (-1 = disabled) |
| `Electronics.TractionControlSlip` | number | 0 | Static | TC slip threshold |
| `Electronics.TractionControlCut` | number | 0 | Static | TC cut level |
| `Electronics.AntiLockBrakes` | number | 0 | Static | ABS setting |
| `Telemetry.Vehicles[*].RearBrakeBias` | number | 0..0.4975 | PerLap | Brake bias (0.5 = 50% rear) |
| `Telemetry.Vehicles[*].SpeedLimiter` | number | 0..1 | PerLap | Pit limiter active |
| `Telemetry.Vehicles[*].SpeedLimiterAvailable` | number | 0..1 | PerLap | Has pit limiter |
| `Telemetry.Vehicles[*].AntiStallActivated` | number | 0 | Static | Anti-stall state |
| `Telemetry.VersionUpdateBegin` | number | monotonic | PerSample | Data version counter |
| `Telemetry.VersionUpdateEnd` | number | monotonic | PerSample | Data version counter |
| `Scoring.VersionUpdateBegin` | number | monotonic | PerSample | Data version counter |
| `Scoring.VersionUpdateEnd` | number | monotonic | PerSample | Data version counter |
| `Scoring.Vehicles[*].ServerScored` | number | 0..1 | PerLap | Server scoring enabled |
| `TimestampUtc` | string | ISO 8601 | PerSample | Capture timestamp |
| `Scoring.ScoringInfo.MaxPlayers` | number | 0 | Static | Server max players |

---

### Position / GPS — 6 fields

| Field | Type | Range | Update Rate | Notes |
|-------|------|-------|-------------|-------|
| `Scoring.ScoringInfo.LapDist` | number | 5745 m | PerLap | Total track length |
| `Scoring.Vehicles[*].LapDist` | number | -2..5744 m | PerSample | Position on track centerline |
| `Scoring.Vehicles[*].PathLateral` | number | -18..18 m | PerSample | Lateral offset from centerline |
| `Scoring.Vehicles[*].TrackEdge` | number | -7.8..13.6 m | Frequent | Distance to track edge |
| `Scoring.Vehicles[*].PitLapDist` | number | 0..226 m | PerLap | Pit lane entry distance |
| `Telemetry.Vehicles[*].Wheels[*].mWheelYLocation` | number | -0.12..0.05 m | PerSample | Wheel lateral position |

---

### Race Management / Uncategorized — 75 fields

#### High-Value Race State

| Field | Type | Range | Update Rate | Notes |
|-------|------|-------|-------------|-------|
| `Scoring.Vehicles[*].Place` | number | 0..25 | Frequent | Race position |
| `Scoring.Vehicles[*].Sector` | number | 0..2 | PerLap | Current track sector |
| `Scoring.Vehicles[*].TotalLaps` | number | 0..12 | PerLap | Completed laps |
| `Scoring.Vehicles[*].InPits` | number | 0..1 | PerLap | In pit lane |
| `Scoring.Vehicles[*].PitState` | number | 0..5 | PerLap | Pit stop phase (0=none, 1=request, 2=entering, 4=stopped, 5=exiting) |
| `Scoring.Vehicles[*].InGarageStall` | number | 0..1 | PerLap | In garage |
| `Scoring.Vehicles[*].FinishStatus` | number | 0..2 | Sparse | 0=running, 1=finished, 2=DNF |
| `Scoring.Vehicles[*].IsPlayer` | number | 0..1 | PerLap | Human player flag |
| `Scoring.Vehicles[*].Control` | number | 0..1 | PerLap | 0=player, 1=AI |
| `Scoring.ScoringInfo.GamePhase` | number | 0..8 | PerLap | Session state machine |
| `Scoring.ScoringInfo.InRealtime` | number | 0..1 | Sparse | Sim running vs paused |
| `Telemetry.Vehicles[*].LapNumber` | number | 0..12 | PerLap | Current lap number |

#### Vehicle State

| Field | Type | Range | Update Rate |
|-------|------|-------|-------------|
| `Telemetry.Vehicles[*].Headlights` | number | 0..1 | Sparse |
| `Telemetry.Vehicles[*].IgnitionStarter` | number | 0..1 | PerLap |
| `Telemetry.Vehicles[*].MaxGears` | number | 0..6 | PerLap |
| `Telemetry.Vehicles[*].ScheduledStops` | number | 0..255 | PerLap |
| `Telemetry.Vehicles[*].BatteryChargeFraction` | number | -0.97..11.9 | Frequent |
| `Telemetry.Vehicles[*].Orientation[*].X/Y/Z` | number | -1..1 | PerSample |
| `Scoring.Vehicles[*].Orientation[*].X/Y/Z` | number | -1..1 | PerSample |
| `Scoring.Vehicles[*].TimeIntoLap` | number | 0..104 s | PerSample |
| `Scoring.Vehicles[*].Qualification` | number | 0..25 | PerLap |
| `Scoring.Vehicles[*].IndividualPhase` | number | 0..5 | PerLap |

#### Wheel Dynamics (per wheel)

| Field | Type | Range | Update Rate |
|-------|------|-------|-------------|
| `Telemetry.Vehicles[*].Wheels[*].Rotation` | number | -248..72 rad/s | PerSample |
| `Telemetry.Vehicles[*].Wheels[*].Camber` | number | -0.15..0.09 rad | Frequent |
| `Telemetry.Vehicles[*].Wheels[*].Toe` | number | -0.34..0.27 rad | Frequent |
| `Telemetry.Vehicles[*].Wheels[*].StaticUndeflectedRadius` | number | 0..36 cm | Frequent |
| `Telemetry.Vehicles[*].Wheels[*].LateralPatchVel` | number | -31..45 m/s | Frequent |
| `Telemetry.Vehicles[*].Wheels[*].LongitudinalPatchVel` | number | -23..24 m/s | Frequent |
| `Telemetry.Vehicles[*].Wheels[*].LateralGroundVel` | number | -31..45 m/s | Frequent |
| `Telemetry.Vehicles[*].Wheels[*].LongitudinalGroundVel` | number | -76..13 m/s | Frequent |

#### Not Populated (per wheel)

| Field | Notes |
|-------|-------|
| `Telemetry.Vehicles[*].Wheels[*].LateralForce` | Force data not exposed |
| `Telemetry.Vehicles[*].Wheels[*].LongitudinalForce` | Force data not exposed |
| `Telemetry.Vehicles[*].Wheels[*].GripFract` | Grip fraction not exposed |

#### Session/Server Config (always 0 in single-player)

| Field | Type |
|-------|------|
| `Scoring.ScoringInfo.ServerPort` | number |
| `Scoring.ScoringInfo.ServerPublicIP` | number |
| `Scoring.ScoringInfo.IsPasswordProtected` | number |
| `Scoring.ScoringInfo.MaxPlayers` | number |
| `Scoring.ScoringInfo.GameMode` | number |
| `Scoring.ScoringInfo.StartLight` | number |

#### Opaque / Internal

| Field | Type | Notes |
|-------|------|-------|
| `Scoring.ScoringInfo.Pointer1` | string | Memory pointer (base64) |
| `Scoring.ScoringInfo.Pointer2` | string | Memory pointer (base64) |
| `Scoring.ScoringInfo.Expansion` | string | Reserved expansion field |
| `Scoring.Vehicles[*].Expansion` | string | Reserved expansion field |
| `Telemetry.Vehicles[*].Expansion` | string | Reserved expansion field |
| `Scoring.Vehicles[*].UpgradePack` | string | Vehicle upgrade identifier |
| `Scoring.Vehicles[*].PitGroup` | string | Pit assignment group |
| `Telemetry.Vehicles[*].PhysicsToGraphicsOffset[*]` | number | Rendering offset (3 values) |
| `Telemetry.Vehicles[*].Unused` | string | Unused padding |
| `Telemetry.BytesUpdatedHint` | number | Bytes changed since last update |
| `Scoring.BytesUpdatedHint` | number | Bytes changed since last update |

---

## Summary by Update Frequency

| Frequency | Approx. Count | Storage Strategy |
|-----------|:-------------:|-----------------|
| **PerSample** | ~60 fields | Time-series columns in DuckDB — high-frequency ingest |
| **Frequent** | ~50 fields | Time-series columns — moderate change rate |
| **PerLap** | ~60 fields | Per-lap aggregate table or event-based |
| **Sparse** | ~10 fields | Event-driven storage (pit entries, flags, DNFs) |
| **Static** | ~55 fields | Session metadata table — store once per session |

---

## Data Quality Notes

1. **128 vehicle slots**: Only 25 are active; indices 25–127 contain all-zero data. Filter by `Telemetry.Vehicles[i].ID` or `Scoring.Vehicles[i].Control` to identify active cars.

2. **Base64 strings**: LMU exposes raw rFactor 2 shared memory structs. String fields like `TrackName`, `DriverName`, `VehicleClass`, `TerrainName`, and compound names are base64-encoded fixed-size `char[]` buffers padded with null bytes.

3. **Duplicate data**: Position, velocity, acceleration, and orientation appear in both `Telemetry.Vehicles[*]` and `Scoring.Vehicles[*]` — the Telemetry version updates at physics rate, while Scoring updates at scoring rate. Prefer Telemetry for analysis.

4. **Unpopulated fields**: Several fields (`EngineTorque`, `EngineOilTemp`, `EngineWaterTemp`, `FrontDownforce`, `RearDownforce`, `Drag`, `LateralForce`, `LongitudinalForce`, `TyreLoad`, `GripFract`) are always 0. These are rFactor 2 struct placeholders that LMU doesn't populate for AI cars. They may be populated for the player car in some modes.

5. **Sample rate**: The JSON logger captures at ~100 Hz, but the physics simulation may not update every sample. Consecutive samples can be identical. Use `TimestampUtc` or stride-based sampling for analysis.

6. **Sector times**: Monza Curva Grande has 3 sectors. `CurSector1/CurSector2` accumulate during the lap; `LastSector1/LastSector2` finalize at lap end. `BestSector1/BestSector2` track session-best per sector.

---

## Coordinate System

- **X**: Lateral (positive = right when facing track direction)
- **Y**: Vertical (positive = up)
- **Z**: Longitudinal (positive = forward/track direction)
- **Origin**: Track-specific world origin

Velocities and accelerations use the **car-local** coordinate frame:
- **X**: Lateral (positive = right)
- **Y**: Vertical (positive = up)
- **Z**: Longitudinal (negative = forward — note the sign convention)

The `Orientation` field is a 3×3 rotation matrix stored as 3 row-vectors, each with X/Y/Z components.

---

*Generated from analysis by PitWall.JsonAnalyzer — see `PitWall.LMU/PitWall.JsonAnalyzer/` for the tool.*
