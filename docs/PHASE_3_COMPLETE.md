# Phase 3 – Tyre Degradation Tracking Complete

## Scope
- Track tyre wear per corner and project laps to a pit window.
- Extend strategy engine to issue tyre-based pit calls while preserving fuel priority.
- Integrate tyre telemetry into plugin and tests.

## What was delivered
- `TyreDegradation`: per-tyre wear history, average wear per lap, and laps-until-threshold projection.
- Telemetry updates: added tyre wear fields; SimHub telemetry provider maps `DataCorePlugin.GameData.NewData.TyreWear*` values.
- Strategy: `StrategyEngine` now records tyre wear, forecasts threshold crossings, and emits tyre pit recommendations (warning priority) while keeping fuel-critical calls as highest priority.
- Plugin: initializes tyre degradation, feeds it each `DataUpdate`, and continues audio queue flow.

## Tests
- Added tyre degradation tests, tyre-aware strategy tests; full suite now 39 passing tests.
- Validated projections, threshold handling, and priority ordering (fuel critical > tyre warnings).

## Notes
- Tyre threshold set to 30% remaining; projected pit call when any tyre predicted to cross threshold within 2 laps.
- Tyres with no data are ignored to avoid false positives.
- Wear values assumed as percentage remaining (0-100); adjust mapping if a sim uses different units.

## Next steps
- Add rate-limiting/priority rules to audio playback for overlapping fuel/tyre calls.
- Surface tyre deltas per stint in recommendations.
- Map sim-specific property names once integrating with live SimHub data.
