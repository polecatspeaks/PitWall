# GitHub Copilot Instructions for PitWall LMU Race Engineer

You are working in the **PitWall LMU Race Engineer** solution. Prioritize **correctness**, **determinism**, and **clear race-engineer communication**. Keep changes minimal and aligned with existing patterns.

## Repository Structure

**Root:** `PitWall.LMU/`

```
PitWall.LMU/
├── PitWall.Core/          # Telemetry models, adapters, and storage services
├── PitWall.Strategy/      # Strategy engine and rule-based logic
├── PitWall.Api/           # ASP.NET Core API + WebSocket server
├── PitWall.Agent/         # AI agent + LLM integration
├── PitWall.UI/            # Avalonia UI (MVVM pattern)
├── PitWall.Tests/         # xUnit unit tests
├── PitWall.UI.Tests/      # xUnit UI tests
└── Tools/                 # Scripts and utilities
```

## Key Commands

### Build
```bash
cd PitWall.LMU
dotnet build PitWall.LMU.sln
```

### Test
```bash
cd PitWall.LMU
dotnet test
```

### Code Coverage
Use VS Code task: `coverage (PitWall.LMU)`
- Report generated at: `PitWall.LMU/index.html`
- Open in browser to view coverage details

## Global Guidelines

### Performance & Architecture
- **Prefer simple, deterministic logic** — Avoid blocking the telemetry pipeline (100Hz data rate)
- **Use async/await for I/O** — Never block threads; critical for real-time telemetry streaming
- **Keep methods focused** — Extract pure logic to testable helper methods when needed
- **Performance budget:** Target <10% CPU, <200MB RAM (runs alongside LMU game)
- **UI refresh rate:** 10Hz budget; avoid layout thrashing in UI updates

### Code Quality
- **Add XML documentation** for public APIs when touching them
- **Avoid new dependencies** unless absolutely necessary
- **Follow existing patterns:**
  - MVVM in UI (ViewModels inherit from `ObservableObject`)
  - Dependency Injection in services (constructor injection)
  - Repository pattern for data access
  - Strategy pattern for rule engine

### Race Engineer Communication
- **Outputs must be concise, specific, and actionable**
- Example good: "Fuel critical: 2.1 laps remaining. Pit now."
- Example bad: "The fuel level appears to be getting low and you might want to consider pitting soon."
- **Use racing terminology** (stint, compound, delta, gap, etc.)
- **Include confidence levels** when making predictions

### Async Patterns
- Telemetry pipeline must never block
- Use `IAsyncEnumerable<T>` for streaming data
- Batch database writes (500-2000 samples per batch)
- Use bounded queues with health monitoring

## Testing Expectations

### Coverage Goals
- **Target:** 90% line/branch coverage
- **Current priority:** Address coverage debt in:
  - `PitWall.UI.Services` (high-risk hotspots)
  - `PitWall.UI.ViewModels` (high-risk hotspots)

### Test Types
- **Prefer unit tests** — Fast, isolated, deterministic
- **Mock external dependencies:**
  - HTTP clients (use `HttpMessageHandler` mocks)
  - LLM services (mock responses)
  - Telemetry inputs (use synthetic data generators)
- **Integration tests only when needed** — For critical paths requiring real dependencies

### Test Requirements
- **Any logic changes MUST include tests**
- Tests should be:
  - Fast (<100ms per test)
  - Deterministic (no flaky tests)
  - Isolated (no shared state between tests)
- Use xUnit test framework
- Follow AAA pattern (Arrange, Act, Assert)

### Current Test Priorities
1. Fix existing UI test failures first
2. Add tests for new features
3. Improve coverage in hotspot areas

## Current Priorities

1. **Coverage debt** — Goal: 90% line/branch coverage
2. **Fix existing UI test failures** — Do this before adding new features
3. **Address high-risk hotspots:**
   - `PitWall.UI.Services` — Service layer integration points
   - `PitWall.UI.ViewModels` — UI state management and data binding

## Technology Stack

- **.NET 9.0** — Latest LTS version
- **Avalonia 11.x** — Cross-platform UI framework
- **xUnit** — Test framework
- **CommunityToolkit.Mvvm** — MVVM helpers and source generators
- **ASP.NET Core** — REST API and WebSocket server
- **DuckDB** (optional) — Analytics and session storage

## Important Constraints

### DO NOT
- ❌ **Delete coverage artifacts** unless explicitly asked
- ❌ **Change public APIs** without updating consumers and tests
- ❌ **Introduce flaky tests** or non-deterministic behavior
- ❌ **Add blocking operations** in telemetry pipeline
- ❌ **Remove or modify working tests** without clear justification
- ❌ **Add new dependencies** without discussing trade-offs

### DO
- ✅ **Write tests first** (TDD approach preferred)
- ✅ **Update XML docs** when modifying public APIs
- ✅ **Use existing patterns** and conventions
- ✅ **Keep changes minimal** and surgical
- ✅ **Profile performance** for telemetry-critical code
- ✅ **Add comments** only when logic is complex or non-obvious

## Development Workflow

### TDD Approach (Preferred)
1. **RED:** Write a failing test that describes desired behavior
2. **GREEN:** Implement minimal code to pass the test
3. **REFACTOR:** Improve code clarity while keeping tests green
4. **COMMIT:** Save progress with descriptive message

### Commit Conventions
- `feat:` New feature (rule, storage, API endpoint)
- `test:` New tests or test improvements
- `fix:` Bug fix in existing feature
- `refactor:` Code cleanup, no behavior change
- `docs:` Documentation or README updates
- `chore:` Build, CI, dependency updates
- `perf:` Performance improvements

### Example
```bash
git commit -m "feat: add wheel lock detection

- Detect wheel locks when brake pressure >90% and speed delta >5 km/h
- Add WheelLockRule to StrategyEngine
- Unit tests: 3 new tests (normal braking, lock detected, false positive)"
```

## Performance Targets

- **CPU:** <10% on typical gaming rig (runs alongside LMU)
- **Memory:** <200MB
- **Telemetry latency:** <100ms (sensor to UI display)
- **Recommendation frequency:** 1/second (configurable)
- **Test suite:** <1 second for unit tests
- **UI refresh rate:** 60 FPS target, 10Hz minimum for critical updates

## API Endpoints Reference

### AI Agent
- `POST /agent/query` — Send query to AI engineer
- `GET /agent/config` — Get current LLM configuration
- `PUT /agent/config` — Update LLM configuration
- `POST /agent/llm/discover` — Discover available LLM providers

### Strategy Engine
- `GET /api/recommend` — Get current strategy recommendation
- `GET /api/state` — Get current race state

### WebSocket
- `ws://localhost:5104/ws/state` — Real-time telemetry stream (100Hz)

## UI Performance Guidelines

### Avalonia-Specific
- **Avoid layout thrashing:** Batch property changes, use `BeginBatchUpdate()`
- **Use virtualization:** For long lists (>100 items), use `VirtualizingStackPanel`
- **Throttle updates:** Use Rx.NET `Sample(16ms)` for high-frequency updates
- **String pooling:** Cache frequently used formatted strings (lap times, positions)
- **Profile with DevTools:** Press F12 to open Avalonia DevTools

### Data Binding
- Use `[ObservableProperty]` source generator for ViewModels
- Implement `INotifyPropertyChanged` correctly
- Avoid expensive operations in property getters
- Use `OneWay` binding when possible (not `TwoWay`)

## Common Patterns

### Telemetry Pipeline
```csharp
// Stream telemetry asynchronously
public async IAsyncEnumerable<TelemetrySample> StreamTelemetryAsync(
    [EnumeratorCancellation] CancellationToken ct = default)
{
    await foreach (var sample in source.WithCancellation(ct))
    {
        yield return sample;
    }
}
```

### Strategy Rule
```csharp
public class TyreTempRule : IStrategyRule
{
    public RuleResult Evaluate(RaceState state)
    {
        var maxTemp = state.TyreTemps.Max();
        if (maxTemp > 110.0)
        {
            return RuleResult.Critical("Tyre overheat: >110°C. Reduce pace.");
        }
        return RuleResult.Ok();
    }
}
```

### ViewModel Pattern
```csharp
public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private string _lapTime = "0:00.000";
    
    [ObservableProperty]
    private double _fuelRemaining;
    
    partial void OnFuelRemainingChanged(double value)
    {
        // React to property change if needed
    }
}
```

## File Naming Conventions

- **ViewModels:** `{Feature}ViewModel.cs` (e.g., `DashboardViewModel.cs`)
- **Views:** `{Feature}View.axaml` and `{Feature}View.axaml.cs`
- **Services:** `{Purpose}Service.cs` or `{Purpose}Client.cs`
- **Tests:** `{Class}Tests.cs` (e.g., `StrategyEngineTests.cs`)
- **UserControls:** `{Component}Control.axaml` (e.g., `FuelPanelControl.axaml`)

## When Making Changes

### Before You Start
1. Read related tests to understand existing behavior
2. Check for similar implementations in the codebase
3. Verify current build and test status
4. Identify affected public APIs

### During Development
1. Write tests first (if using TDD)
2. Make minimal changes to achieve goal
3. Run tests frequently (`dotnet test --filter YourTest`)
4. Profile performance for telemetry-critical code
5. Update XML docs for modified public APIs

### Before Committing
1. Run full test suite: `dotnet test`
2. Check coverage hasn't decreased significantly
3. Verify no unintended file changes
4. Ensure code follows existing patterns
5. Add descriptive commit message

## Getting Help

### Documentation Locations
- `PitWall.LMU/README.md` — Project overview and quick start
- `PitWall.LMU/docs/` — Detailed documentation
- Architecture diagrams in `docs/` folder
- API documentation via XML comments

### Common Issues
- **DuckDB integration tests fail:** These are skipped by default (require native binaries)
- **UI tests flaky:** Check for timing issues; use proper async/await patterns
- **WebSocket connection fails:** Verify API is running on `localhost:5104`
- **LLM discovery fails:** Check firewall settings and Ollama installation

## Summary

**Remember:** This is a real-time race engineering system. **Determinism**, **performance**, and **clear communication** are paramount. Test everything, avoid blocking the telemetry pipeline, and keep the race engineer's needs front and center.
