# Pit Wall - AI Race Engineer for SimHub

[![CI](https://github.com/polecatspeaks/PitWall/actions/workflows/ci.yml/badge.svg)](https://github.com/polecatspeaks/PitWall/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> AI-powered race engineer providing real-time strategy recommendations through audio messages for sim racing.

## Overview

Pit Wall is a SimHub plugin that acts as your virtual race engineer, analyzing telemetry data in real-time and providing strategic recommendations through audio messages. Keep your eyes on the track while your AI engineer handles the strategy.

### Features (Planned)

- 🎯 **Fuel Strategy** - Real-time fuel consumption tracking and pit stop recommendations
- 🏁 **Tyre Management** - Lap time degradation analysis for optimal pit windows
- 🚗 **Multi-Class Awareness** - Traffic management for mixed-class racing
- 🔊 **Audio-First Experience** - Clear voice recommendations that don't distract
- 📊 **Profile Learning** - Adapts to your driving style over time
- ⚡ **High Performance** - <5% CPU usage, <10ms processing per update

### Current Status

**Phase 0 Complete** ✅ - Development environment and plugin skeleton ready  
**Phase 1 In Progress** 🚧 - Fuel strategy implementation

See [TDD_ROADMAP.md](docs/TDD_ROADMAP.md) for detailed development plan.

## Installation

### Prerequisites

- [SimHub](https://www.simhubdash.com/) v9.x or later
- Windows 10/11
- .NET Framework 4.8 (usually pre-installed)

### From Release (Coming Soon)

1. Download the latest release from the [Releases](https://github.com/polecatspeaks/PitWall/releases) page
2. Extract the ZIP file
3. Copy `PitWall.dll` to your SimHub plugins folder
4. Restart SimHub
5. Enable "Pit Wall Race Engineer" in Settings → Plugins

### From Source

See [CONTRIBUTING.md](docs/CONTRIBUTING.md) for development setup instructions.

## Usage

Once installed and enabled:

1. Start SimHub
2. Load your favorite sim (iRacing, ACC, etc.)
3. Begin a race session
4. Listen for audio recommendations from your race engineer

### Configuration

Access plugin settings in SimHub:
- **Settings → Plugins → Pit Wall Race Engineer**
  - Volume control
  - Message cooldown period
  - Enable/disable specific recommendation types
  - Custom audio folder path

## Supported Simulators

**Primary Target:**
- iRacing ✅

**Planned Support:**
- Assetto Corsa Competizione
- Automobilista 2
- rFactor 2

## Documentation

- [Development Roadmap](docs/TDD_ROADMAP.md) - Phase-by-phase development plan
- [Phase 0 Complete](docs/PHASE_0_COMPLETE.md) - Environment setup summary
- [Contributing Guide](docs/CONTRIBUTING.md) - How to contribute
- [Architecture](docs/ARCHITECTURE.md) - Technical design (coming soon)

## Development

### Quick Start

```bash
# Clone the repository
git clone https://github.com/polecatspeaks/PitWall.git
cd PitWall

# Copy SimHub DLLs (see SimHub/README.md)
# Then restore and build
dotnet restore
dotnet build --configuration Release

# Run tests
dotnet test
```

### Project Structure

```
PitWall/
├── Core/              # Core interfaces and abstractions
├── Models/            # Data models
├── Strategy/          # Strategy engine logic (Phase 1+)
├── Audio/             # Audio playback system (Phase 2+)
├── PitWallPlugin.cs   # Main plugin entry point
└── PitWall.Tests/     # Unit and integration tests
```

### Testing

We follow Test-Driven Development (TDD):

```bash
# Run all tests
dotnet test --configuration Release

# Run with coverage
dotnet test /p:CollectCoverage=true

# Watch mode (auto-run on changes)
dotnet watch test
```

Current test coverage: **>85%**

## Performance

Target performance metrics:
- CPU Usage: <5% during active race
- DataUpdate: <10ms per call (100Hz update rate)
- Memory: <50MB total
- Test Suite: <3 seconds

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](docs/CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

### Development Phases

- ✅ **Phase 0** - Development Environment & Scaffolding
- 🚧 **Phase 1** - Basic Telemetry → Strategy Logic (Fuel)
- 📋 **Phase 2** - Audio Playback & Message Queueing
- 📋 **Phase 3** - Tyre Degradation Tracking
- 📋 **Phase 4** - Multi-Class Race Awareness
- 📋 **Phase 5+** - Advanced features

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [SimHub](https://www.simhubdash.com/) - The amazing sim racing telemetry platform
- iRacing community for testing and feedback
- All contributors to this project

## Contact

- **Issues**: [GitHub Issues](https://github.com/polecatspeaks/PitWall/issues)
- **Discussions**: [GitHub Discussions](https://github.com/polecatspeaks/PitWall/discussions)

---

**Note**: This plugin is in active development. Features are being added incrementally following TDD principles. Check the [roadmap](docs/TDD_ROADMAP.md) for current status.
