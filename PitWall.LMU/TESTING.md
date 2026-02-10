# PitWall Full Stack Testing Guide

## Quick Start

### Option 1: VS Code Tasks (Recommended)
Press `Ctrl+Shift+P` and run:
- **"Tasks: Run Task"** → **"Launch Full PitWall Stack"**

This automatically starts:
1. PitWall Agent (API) on http://localhost:5000
2. PitWall UI (Desktop App)

### Option 2: Manual Launch

**Terminal 1 - Start Agent:**
```powershell
cd PitWall.LMU\PitWall.Agent
dotnet run
# Wait for "Now listening on: http://localhost:5000"
```

**Terminal 2 - Start UI:**
```powershell
cd PitWall.LMU\PitWall.UI
dotnet run
```

## Testing Discovery Feature

With both Agent and UI running:

1. **Navigate to Settings panel** in UI
2. **Enable LLM Discovery** checkbox
3. **Configure:**
   - Port: `11434` (Ollama default)
   - Max Threads: `10`
   - Subnet: `192.168.1` (or your local subnet like `10.0.0`)
4. **Click "Save Config"** - sends settings to Agent
5. **Click "Run Discovery"** - Agent scans network
6. **View results** below button

## VS Code Tasks Available

- `Launch Full PitWall Stack` - Start Agent + UI together
- `Build PitWall Solution` - Build entire solution
- `Test PitWall Solution` - Run all 102 tests
- `Publish PitWall UI (Windows x64)` - Create standalone .exe
- `Start PitWall Agent (API)` - Start backend only
- `Start PitWall UI` - Start frontend only

## Verify Agent is Running

```powershell
Invoke-WebRequest http://localhost:5000/agent/config
```

Should return: `StatusCode: 200` with JSON config

## Architecture

```
┌─────────────────┐         HTTP          ┌──────────────────┐
│   PitWall UI    │ ◄──────────────────► │  PitWall Agent   │
│  (Desktop App)  │   localhost:5000     │    (API/Backend) │
│                 │                       │                  │
│  - Settings     │   GET /agent/config  │  - LLM Service   │
│  - Telemetry    │   PUT /agent/config  │  - Discovery     │
│  - AI Chat      │   POST /agent/query  │  - Strategy      │
│  - Discovery    │   GET /agent/llm/    │  - Telemetry     │
│                 │       discover        │                  │
└─────────────────┘                       └──────────────────┘
                                                    │
                                                    ▼
                                          ┌──────────────────┐
                                          │  Local Network   │
                                          │  LLM Endpoints   │
                                          │  (Ollama, etc.)  │
                                          └──────────────────┘
```

## Port Configuration

**Agent:** Port 5000 (configured in `PitWall.Agent/Properties/launchSettings.json`)
**UI:** Connects to `http://localhost:5000` (configurable via `PITWALL_API_BASE` env var)

## Current Status

✅ Agent running on port 5000
✅ 102 tests passing (42 API + 60 UI)
✅ Discovery feature implemented and tested
✅ Windows x64 executable published

## Troubleshooting

**"Discovery failed: No connection could be made"**
- Ensure Agent is running on port 5000
- Check `Invoke-WebRequest http://localhost:5000/agent/config`

**UI can't connect to Agent**
- Verify Agent port: should be 5000
- Check `launchSettings.json` has correct port

**Discovery finds no endpoints**
- Verify subnet is correct for your network
- Ensure LLM service (Ollama) is actually running on target machines
- Port 11434 is Ollama default, adjust if using different service
