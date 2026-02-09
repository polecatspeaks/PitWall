# PitWall.LMU - Standalone Race Engineer (TDD)

A fresh, TDD-first race engineering application for LeMans Unlimited (LMU) that provides real-time strategic recommendations.

## Vision

PitWall.LMU runs on a 2nd monitor or tablet and provides race engineers with accurate, actionable, real-time strategy recommendations:
- Tyre life, temperature & wear tracking
- Fuel consumption & pit stop timing
- Lock detection & brake coaching
- Stint length optimization
- Pit strategy recommendations

## Architecture (TDD)

```
PitWall.Api (ASP.NET Core Minimal API)
  ├─ WebSocket /ws/state (real-time push)
  └─ REST /api/recommend (on-demand queries)
  
PitWall.Strategy
  ├─ StrategyEngine (rule-based + ML-ready)
  ├─ TyreTempRule, FuelProjectionRule, etc.
  └─ Confidence scoring

PitWall.Core
  ├─ Models (TelemetrySample, RaceState, etc.)
  ├─ LMU.LMUAdapter (LMU telemetry ingestion)
  └─ Storage.ITelemetryWriter (batch persistence)

PitWall.Tests (xUnit + TDD)
  ├─ StrategyTests (tyre/fuel rules)
  ├─ TelemetryWriterTests (storage)
  └─ Integration tests (coming)
```

## Development Status

### ✅ Completed
- [x] Orphan branch (`feature/lmu-tdd`) isolated from mainline
- [x] Project scaffolding (4 projects: Api, Core, Strategy, Tests)
- [x] TelemetrySample model (7 fields: timestamp, speed, temps, fuel, brakes, throttle, steering)
- [x] LMUAdapter (synthetic sample streaming for TDD)
- [x] StrategyEngine
  - [x] Tyre overheat detection (>110°C threshold)
  - [x] Fuel projection (laps remaining calculation)
- [x] InMemoryTelemetryWriter (batch storage interface)
- [x] Unit tests: **4/4 passing** (no regressions)

### 🚧 In Progress
- [ ] API + WebSocket integration tests
- [ ] CI pipeline (GitHub Actions)
- [ ] Performance tests (throughput, latency)
- [ ] Developer docs & runbook

### 📋 Planned
- [ ] DuckDB writer (optional; currently in-memory)
- [ ] ML inference layer (confidence scores)
- [ ] UI stub (Electron or web PWA)
- [ ] Session replay & replay analysis
- [ ] Historical profile seeding

## Quick Start

### Prerequisites
- .NET 9.0 SDK
- Windows PowerShell 5.1+

### Build & Test

```bash
cd PitWall.LMU
dotnet build
dotnet test --no-build
```

Expected output:
```
Test summary: total: 4, failed: 0, succeeded: 4, skipped: 0, duration: 0.6s
```

### Project Structure

```
PitWall.LMU/
├── PitWall.Api/              # Web API & WebSocket server
├── PitWall.Core/             # Models, adapters, storage
│   ├── Models/
│   │   └── TelemetrySample.cs
│   ├── LMU/
│   │   └── LMUAdapter.cs
│   └── Storage/
│       ├── ITelemetryWriter.cs
│       └── InMemoryTelemetryWriter.cs
├── PitWall.Strategy/         # Rule engine & strategy logic
│   └── StrategyEngine.cs
├── PitWall.Tests/            # Unit + integration tests
│   ├── UnitTest1.cs (StrategyTests)
│   └── TelemetryWriterTests.cs
└── PitWall.LMU.sln
```

## TDD Workflow

1. **RED**: Write a failing test that describes desired behavior
2. **GREEN**: Implement minimal code to pass the test
3. **REFACTOR**: Improve code clarity while keeping tests green
4. **COMMIT**: Save progress with descriptive message

### Example: Adding a New Rule

```csharp
// 1. RED: Add failing test
[Fact]
public void LockDetection_WarnsOnWheelLock()
{
    var engine = new StrategyEngine();
    var sample = new TelemetrySample(..., brake: 0.95, ...);
    
    var result = engine.Evaluate(sample);
    
    Assert.Contains("Wheel lock", result);
}

// 2. GREEN: Implement minimal logic
public string Evaluate(TelemetrySample sample)
{
    if (sample.Brake >= 0.9) return "Wheel lock detected: ease braking";
    ...
}

// 3. REFACTOR: Extract rule into method
private string CheckLockDetection(TelemetrySample sample) { ... }

// 4. COMMIT
git add -A && git commit -m "feat: add wheel lock detection rule"
```

## Performance Targets

- CPU: <10% (typical gaming rig)
- Memory: <200MB
- Telemetry latency: <100ms
- Recommendation frequency: 1/second (configurable)
- Test suite: <1 second

## Testing

### Run All Tests
```bash
dotnet test --no-build
```

### Run Specific Test
```bash
dotnet test --no-build --filter "TyreTempRule"
```

### Watch Mode (auto-run on save)
```bash
dotnet watch test
```

## Git Workflow

Branch: `feature/lmu-tdd`
- Orphan (no parent commit history)
- Isolated from mainline `vibrant-meninsky` or `main`
- PR to main when MVP complete

### Commit Conventions

- `feat:` New feature (rule, storage, API endpoint)
- `test:` New tests or test improvements
- `fix:` Bug fix in existing feature
- `refactor:` Code cleanup, no behavior change
- `docs:` Documentation or README updates
- `chore:` Build, CI, dependency updates

Example:
```bash
git commit -m "feat: add pit window recommendation

- Calculate optimal pit timing based on fuel/tyre/gap data
- Add PitWindowRecommendation rule to StrategyEngine
- Unit tests: 2 new tests (on-time, early, late pit windows)"
```

## Next Steps

1. **API Integration** - Add HTTP endpoints and WebSocket for real-time UI push
2. **CI Pipeline** - GitHub Actions: build, test, code coverage
3. **Performance Tests** - Throughput & latency benchmarks
4. **Docs & Runbook** - Setup guide, troubleshooting, deployment

## Resource Constraints

- LMU runs on same machine as PitWall
- Target: <10% CPU, <200MB RAM
- Mitigation:
  - Batch telemetry writes (500-2000 samples/batch)
  - Async IO with worker threads
  - Decimate high-rate channels for heavy computation
  - Limit ML inference to 1/sec or on-demand
  - Bounded queues + health monitoring

## Contributing

Follow TDD strictly:
1. Write a failing test first
2. Implement minimal code to pass
3. Refactor for clarity
4. Commit with descriptive message
5. Ensure all tests pass before pushing

## License

MIT

---

**Status**: Early TDD development. 4/100+ features planned.  
**Last Updated**: 2026-02-09  
**Next Review**: Post-API integration
