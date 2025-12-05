# Phase 1 – Fuel Strategy Complete

## Scope
- Basic fuel strategy brain: consume telemetry, model fuel usage, generate pit calls.
- Components added: `FuelStrategy`, `SimHubTelemetryProvider`, `StrategyEngine`, plugin wiring.

## What was delivered
- Fuel consumption tracking with per-lap recording, averages, and lap prediction.
- Telemetry adapter using `IPluginPropertyProvider` for SimHub-friendly and test-friendly access.
- Strategy engine that issues critical fuel pit recommendations when <2 laps remain.
- Plugin integration: initializes strategy stack in `Init` and executes in `DataUpdate`.

## Tests and performance
- Tests: 23 total, all passing (FuelStrategy, TelemetryProvider, StrategyEngine, performance, lifecycle).
- Performance: DataUpdate <10ms and average <5ms (using mock provider path); Init <1s.

## Usage notes
- Telemetry property names align with SimHub DataCore (`DataCorePlugin.GameData.NewData.*`).
- `PluginManagerPropertyProvider` adapts real PluginManager; mocks implement `IPluginPropertyProvider` to avoid SimHub DLL dependency in tests.
- `FuelStrategy` assumes simple per-lap usage; future phases will refine lap start/end fuel sources.

## Next steps
- Phase 2: Audio playback and message queueing of recommendations.
- Harden edge cases: invalid laps, negative fuel readings, lap resets.
- Extend strategy inputs: tyre wear, damage, weather once telemetry is mapped.
