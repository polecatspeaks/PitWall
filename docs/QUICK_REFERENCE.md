# Quick Reference

Fast access to common tasks and information for PitWall development.

## 🚀 Quick Start

```bash
# Clone
git clone https://github.com/polecatspeaks/PitWall.git
cd PitWall

# Setup SimHub DLLs (first time only)
# Copy from C:\Program Files (x86)\SimHub\ to SimHub/
# - SimHub.Plugins.dll
# - GameReaderCommon.dll  
# - WoteverCommon.dll

# Build
dotnet build --configuration Release

# Test
dotnet test
```

## 🔨 Common Commands

```bash
# Development
dotnet build                    # Build in Debug mode
dotnet build --configuration Release
dotnet watch test              # Auto-run tests on file changes
dotnet test --logger "console;verbosity=detailed"

# Testing
dotnet test                    # Run all tests
dotnet test /p:CollectCoverage=true  # With coverage
dotnet test --filter "FullyQualifiedName~FuelStrategy"  # Specific tests

# Cleanup
dotnet clean
Remove-Item -Recurse -Force bin, obj, PitWall.Tests\bin, PitWall.Tests\obj
```

## 📁 Key Files

| File | Purpose |
|------|---------|
| `PitWallPlugin.cs` | Main plugin entry point - implement features here |
| `Core/*.cs` | Interfaces for strategy engine and telemetry |
| `Models/*.cs` | Data structures (Telemetry, Recommendation, etc.) |
| `PitWall.Tests/` | All unit tests - write tests first! |
| `docs/TDD_ROADMAP.md` | Development phases and acceptance criteria |

## 📋 TDD Workflow

1. **Red** - Write failing test
   ```csharp
   [Fact]
   public void CalculateFuelUsed_ReturnsCorrectValue()
   {
       var strategy = new FuelStrategy();
       var result = strategy.CalculateFuelUsed(100.0, 95.0);
       Assert.Equal(5.0, result, 0.01);
   }
   ```

2. **Green** - Minimal implementation
   ```csharp
   public double CalculateFuelUsed(double start, double end)
   {
       return start - end;
   }
   ```

3. **Refactor** - Clean up while tests pass

## 🎯 Performance Targets

| Metric | Target | Current |
|--------|--------|---------|
| CPU Usage | <5% | ✅ <1% |
| DataUpdate | <10ms | ✅ <1ms |
| Memory | <50MB | ✅ <10MB |
| Test Suite | <5s | ✅ 1.8s |

## 📊 Project Status

| Phase | Status | Description |
|-------|--------|-------------|
| Phase 0 | ✅ | Environment & scaffolding |
| Phase 1 | 🚧 | Fuel strategy |
| Phase 2 | 📋 | Audio system |
| Phase 3 | 📋 | Tyre strategy |
| Phase 4 | 📋 | Multi-class awareness |

## 🧪 Test Naming

```
MethodName_Scenario_ExpectedResult

Examples:
- CalculateFuelUsed_ValidInput_ReturnsCorrectValue
- ShouldPit_FuelCritical_ReturnsTrue
- RecordLap_InvalidData_ThrowsException
```

## 📦 Mock Data

```csharp
// GT3 car
var telemetry = MockTelemetryBuilder.GT3()
    .WithFuelRemaining(50.0)
    .WithLapTime(120.5)
    .Build();

// LMP2 car
var telemetry = MockTelemetryBuilder.LMP2()
    .WithCurrentLap(10)
    .InPit()
    .Build();
```

## 🔍 Debugging

```bash
# Run specific test with debugging
dotnet test --filter "MethodName" --logger "console;verbosity=detailed"

# Check build warnings
dotnet build --warnaserror

# Clean build
dotnet clean && dotnet build --configuration Release
```

## 📖 Documentation

- [README.md](../README.md) - Project overview
- [docs/CONTRIBUTING.md](CONTRIBUTING.md) - How to contribute
- [docs/TDD_ROADMAP.md](TDD_ROADMAP.md) - Development plan
- [docs/PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) - Repo layout

## 🐛 Troubleshooting

**Build fails with missing DLLs:**
```bash
# Copy SimHub DLLs from installation
Copy-Item "C:\Program Files (x86)\SimHub\*.dll" SimHub\
```

**Tests fail to discover:**
```bash
# Rebuild test project
dotnet clean PitWall.Tests
dotnet build PitWall.Tests --configuration Release
```

**Performance test fails:**
- Ensure Release build: `dotnet test --configuration Release`
- Close other applications
- Check if CPU is throttled

## 🔗 Links

- [GitHub Repo](https://github.com/polecatspeaks/PitWall)
- [SimHub](https://www.simhubdash.com/)
- [iRacing](https://www.iracing.com/)

## 💡 Tips

- Always write tests first (TDD)
- Keep methods under 30 lines
- Run tests before committing
- Use descriptive variable names
- Add XML doc comments to public APIs
- Check coverage: aim for >85%

---

*Quick reference for PitWall development - Updated: Dec 5, 2025*
