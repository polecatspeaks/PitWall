# PitWall LMU - Pit Wall UI (MVP/POC Release)

## Overview
Windows desktop UI for PitWall LMU race engineer telemetry and AI assistant.

## Published Executable
**Location:** `PitWall.LMU/PitWall.UI/bin/Release/net9.0/win-x64/publish/PitWall.UI.exe`

## Requirements
- Windows x64 (10 or later recommended)
- PitWall API/Agent running at `http://localhost:5000` (default)
- LMU: Le Mans Ultimate sim running (optional, for live telemetry)

## Features
### Telemetry Panels
- **Fuel**: Displays fuel liters and estimated laps remaining
- **Tires**: Shows all 4 tire temperatures (FL, FR, RL, RR)
- **Timing**: Current lap, last lap time, best lap time, delta
- **Strategy**: AI-generated strategy recommendations with confidence
- **Alerts**: Real-time warnings for critical conditions:
  - Fuel critical (<2L)
  - Tire overheat (>110°C)
  - Wheel lock risk
  - Brake/throttle overlap
  - Heavy brake + steering

### AI Assistant
- Text-based chat interface for race engineer queries
- Examples: "Should I pit soon?", "What's wrong with my pace?", "Tire strategy?"
- Responses from AI agent running on backend

### Agent Settings
- Configure LLM provider (Ollama, OpenAI, Anthropic)
- Set endpoints, models, API keys
- Enable/disable LLM discovery on local network
- Load/save configuration to backend

## Environment Variables
- `PITWALL_API_BASE`: Override API endpoint (default: `http://localhost:5000`)
- `PITWALL_SESSION_ID`: Session ID for telemetry (default: `1`)

## Running the UI
1. **Start the API/Agent**: Ensure PitWall API is running on port 5000
   ```
   cd PitWall.LMU/PitWall.Api
   dotnet run
   ```

2. **Launch the UI**: Double-click `PitWall.UI.exe` in the publish folder, or run:
   ```
   PitWall.LMU\PitWall.UI\bin\Release\net9.0\win-x64\publish\PitWall.UI.exe
   ```

3. **Connect to LMU**: If LMU is running with shared memory enabled, telemetry will stream automatically via the API

## UI Testing
Comprehensive test suite with **95 automated tests** ensuring stability and preventing crashes:

### Core API Tests (42 tests)
- Telemetry processing and DuckDB storage
- Strategy engine (fuel, tires, pit windows, wheel lock, brake/throttle overlap)
- LLM discovery service (network scanning, health checks)
- Cloud LLM services (OpenAI, Anthropic)
- Agent service (rule-based fallbacks, LLM integration)

### UI Tests (53 tests)
**Settings Panel Interaction (18 tests)**
- All checkboxes: Enable LLM, Require Pit for LLM, Enable LLM Discovery
- Provider combo box: Ollama, OpenAI, Anthropic selection
- All 12 text inputs: endpoint, model, timeout, discovery settings, cloud API keys
- Edge cases: invalid numeric inputs, empty strings, null values
- Simultaneous operations: all checkboxes toggled, all textboxes populated

**AI Assistant Interaction (11 tests)**
- Input acceptance: normal text, empty strings, long text (1000 chars), special characters
- Message operations: add single, add multiple, clear
- UI controls: Send button exists, input textbox exists, messages ItemsControl exists

**Smoke Tests (9 tests)**
- All panels render: Fuel, Tires, Timing, Alerts, Strategy, AI Assistant, Settings
- Lap display visible
- All navigation buttons present

**Unit Tests (15 tests)**
- ViewModels: telemetry updates, fuel/tire display, alert building, settings load/save, AI query handling
- HTTP Clients: AgentQuery, AgentConfig, Recommendation
- Parsers: telemetry messages, recommendations

**Run tests:**
```bash
cd PitWall.LMU/PitWall.UI.Tests
dotnet test                          # All 53 UI tests
```

```bash
cd PitWall.LMU
dotnet test                          # All 95 tests (42 API + 53 UI)
```

## Architecture
- **Framework**: Avalonia 11.3.11 (cross-platform desktop UI)
- **Pattern**: MVVM with CommunityToolkit.Mvvm
- **Telemetry Stream**: WebSocket to `/ws/state` endpoint
- **AI Queries**: HTTP POST to `/agent/query`
- **Configuration**: HTTP GET/PUT to `/agent/config`

## Known Limitations (MVP/POC)
- UI-only: Requires separate API/Agent process
- No embedded API: Must run PitWall.Api separately
- Static mock data for position, gap, pit window (not yet wired to telemetry)
- Weather panel hardcoded (not dynamic)
- Competitors tab not yet implemented

## Next Steps (Post-MVP)
- Embed API server in UI process (single executable)
- Wire remaining telemetry fields (position, gap intervals, weather)
- Implement competitors panel with live leaderboard
- Add charts/graphs for tire temps, fuel consumption trends
- Support multiple sessions/replays

## Troubleshooting
**UI shows "--" placeholders:**
- Check that PitWall.Api is running
- Verify API base URL is correct
- Ensure LMU shared memory is accessible to API

**AI Assistant not responding:**
- Check agent settings: LLM provider, endpoint, model
- Verify LLM server is running (Ollama/OpenAI/Anthropic)
- Check API logs for query errors

**Settings not saving:**
- Ensure `/agent/config` endpoint is responsive
- Check write permissions for config file on API side

## Build Information
- **Build Date**: 2026-02-09
- **.NET Version**: 9.0.5
- **Target**: Windows x64 self-contained
- **Tests**: 25 passed (Avalonia headless + xUnit)

## License
Internal project (PitWall LMU race engineer)