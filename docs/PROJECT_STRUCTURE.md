# Project Structure

## Overview

Pit Wall follows a flat project structure optimized for SimHub plugin development. This keeps the plugin simple and easy to navigate.

## Directory Layout

```
PitWall/
├── .github/                    # GitHub-specific files
│   ├── copilot/               # GitHub Copilot instructions
│   │   └── agent.md           # AI agent context and expertise
│   └── workflows/             # GitHub Actions CI/CD
│       └── ci.yml             # Automated build and test workflow
│
├── docs/                       # Documentation
│   ├── README.md              # Documentation index
│   ├── CONTRIBUTING.md        # Contribution guidelines
│   ├── TDD_ROADMAP.md         # Development roadmap
│   └── PHASE_0_COMPLETE.md    # Phase completion summaries
│
├── Core/                       # Core interfaces and abstractions
│   ├── ITelemetryProvider.cs  # Telemetry data interface
│   └── IStrategyEngine.cs     # Strategy engine interface
│
├── Models/                     # Data models
│   ├── Telemetry.cs           # Telemetry data structure
│   └── Recommendation.cs      # Strategy recommendation structure
│
├── Strategy/                   # Strategy implementation (Phase 1+)
│   ├── FuelStrategy.cs        # Fuel consumption tracking
│   └── TyreStrategy.cs        # Tyre degradation analysis (Phase 3)
│
├── Audio/                      # Audio system (Phase 2+)
│   └── AudioPlayer.cs         # Audio playback and queueing
│
├── SimHub/                     # SimHub SDK DLLs (not in git)
│   ├── README.md              # Setup instructions
│   ├── SimHub.Plugins.dll     # (copied from SimHub install)
│   ├── GameReaderCommon.dll   # (copied from SimHub install)
│   └── WoteverCommon.dll      # (copied from SimHub install)
│
├── PitWall.Tests/             # Unit and integration tests
│   ├── Mocks/                 # Mock implementations for testing
│   │   ├── MockPluginManager.cs
│   │   └── MockTelemetryBuilder.cs
│   ├── PluginLifecycleTests.cs
│   ├── MockTelemetryTests.cs
│   ├── PerformanceTests.cs
│   └── PitWall.Tests.csproj
│
├── PitWallPlugin.cs           # Main plugin entry point (IPlugin)
├── PluginManifest.json        # SimHub plugin metadata
├── PitWall.csproj             # Main project file
├── PitWall.sln                # Solution file
├── Directory.Build.props      # Shared build properties
├── .gitignore                 # Git ignore rules
├── LICENSE                    # MIT License
└── README.md                  # Project overview
```

## Key Files

### Entry Points

- **PitWallPlugin.cs** - SimHub plugin interface implementation
  - `Init()` - Plugin initialization
  - `End()` - Cleanup on unload
  - `DataUpdate()` - Called ~100Hz with telemetry data

- **PluginManifest.json** - Plugin metadata for SimHub

### Core Interfaces

- **ITelemetryProvider** - Abstraction for reading simulator data
- **IStrategyEngine** - Strategy recommendation logic interface

### Configuration

- **PitWall.csproj** - Main plugin build configuration
- **PitWall.Tests.csproj** - Test project configuration
- **Directory.Build.props** - Shared MSBuild properties

## Build Output

```
bin/
└── Release/
    └── net48/
        ├── PitWall.dll        # Main plugin DLL (copy to SimHub)
        └── PitWall.pdb        # Debug symbols

PitWall.Tests/
└── bin/
    └── Release/
        └── net48/
            ├── PitWall.Tests.dll
            └── SimHub DLLs (copied for testing)
```

## Design Principles

### Flat Structure
- No deep nesting - easy navigation
- Features organized by folder (Core, Models, Strategy, Audio)
- Tests mirror production structure

### Test-Driven Development
- Each production class has a corresponding test class
- Tests run in <3 seconds
- Coverage >85%

### SimHub Plugin Pattern
- Implements IPlugin and IDataPlugin
- References SimHub SDK (not included in git)
- Targets .NET Framework 4.8

### Performance-First
- DataUpdate() must complete in <10ms
- CPU usage <5% during races
- Memory <50MB total

## Adding New Features

When adding a new feature (e.g., Phase 1 - Fuel Strategy):

1. **Create folder** for the feature area (e.g., `Strategy/`)
2. **Write tests first** in `PitWall.Tests/Strategy/`
3. **Implement classes** in feature folder
4. **Update interfaces** in `Core/` if needed
5. **Wire up** in `PitWallPlugin.cs`

Example for Fuel Strategy:
```
Strategy/
├── FuelStrategy.cs
└── IFuelStrategy.cs

PitWall.Tests/
└── Strategy/
    └── FuelStrategyTests.cs
```

## Dependencies

### Production
- .NET Framework 4.8
- SimHub.Plugins.dll
- GameReaderCommon.dll
- WoteverCommon.dll

### Testing
- xUnit 2.6.2
- Moq 4.20.70
- Microsoft.NET.Test.Sdk 17.8.0

## Notes

- **SimHub DLLs** are NOT committed to git (see .gitignore)
- Each developer must copy DLLs from their SimHub installation
- See `SimHub/README.md` for setup instructions
- Tests require DLLs to be present for proper execution

## Related Documentation

- [TDD Roadmap](TDD_ROADMAP.md) - Development phases and features
- [Contributing Guide](CONTRIBUTING.md) - How to add code
- [Phase 0 Complete](PHASE_0_COMPLETE.md) - Current baseline

---

*Structure optimized for SimHub plugin development with TDD workflow*
