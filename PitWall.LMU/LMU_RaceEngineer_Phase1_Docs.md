# LMU Race Engineer - Phases 1-4: Complete Development Roadmap
## Architecture, Implementation, and Strategy Guide

**Version:** 1.1 | **Date:** February 9, 2026  
**Platform:** Windows | **Framework:** .NET 9.0 | **IDE:** VS Code + Copilot  
**Status:** Phase 1 & 2 COMPLETE ✅ | Phase 3 IN PROGRESS | Phase 4 PLANNED

---

## 📋 Table of Contents

1. [Quick Reference](#quick-reference)
2. [Project Setup](#project-setup)
3. [Core Architecture](#core-architecture)
4. [Data Models - Full Code](#data-models)
5. [Shared Memory Reader - Full Code](#shared-memory-reader)
6. [Historical Data Ingestion](#historical-data-ingestion)
7. [Reverse Engineering Guide](#reverse-engineering-guide)
8. [Testing & Validation](#testing-validation)
9. [Copilot Development Tips](#copilot-tips)
10. [Next Steps](#next-steps)

---

## 🚀 Quick Reference

### What You're Building
Phase 1 creates the **data acquisition foundation**:
- ✅ Read LMU shared memory at 100Hz
- ✅ Parse & validate telemetry data
- ✅ Ingest historical DuckDB telemetry
- ✅ Provide clean data model for future phases

### Key Technologies
```
.NET 9.0 + C#
├── System.IO.MemoryMappedFiles (shared memory)
├── DuckDB.NET.Data v1.0.0 (analytics backend)
├── ASP.NET Core Minimal API (web endpoints)
├── Serilog (logging)
├── System.Reactive (event streaming)
└── xUnit + Moq (testing)
```

### Success Criteria
- [x] Connect to LMU shared memory
- [x] Read all critical fields (fuel, tires, timing)
- [x] Parse 72-byte telemetry structure
- [x] Real SharedMemoryReader implementation (async streaming)
- [x] DuckDB backend with schema and batch ingestion
- [x] GET /api/recommend endpoint with RecommendationService
- [x] 18+ unit tests (all passing)
- [ ] WebSocket /ws/state for real-time streaming (Phase 3)
- [ ] Extended strategy rules (Phase 3)
- [ ] Confidence scoring (Phase 3)
- [ ] Local LLM integration (Phase 4)

---

## � Current Implementation Status

### ✅ Phase 1: Data Acquisition Foundation (COMPLETE)
**Objective:** Read LMU shared memory and provide clean telemetry data model.

**Completed:**
- **SharedMemoryReader** - Real implementation parsing 72-byte LMU telemetry structure
  - Async `StreamSamples()` enumerator for non-blocking ingestion
  - Memory-mapped file I/O with graceful fallback
  - Exact field offsets documented (see Memory Structure Reference below)
  - 6 unit tests passing (initialization, events, disposal, streaming)
  
- **Data Models** - Complete telemetry structures
  - TelemetrySample with Speed, Fuel, Brake, Throttle, Steering, TyreTemperatures
  - LMU-specific adapter pattern for event format compatibility

### ✅ Phase 2: Analytics Backend (COMPLETE)
**Objective:** Persist telemetry and enable historical analysis.

**Completed:**
- **DuckDB Integration** - OLAP database for efficient analytics
  - IDuckDbConnector interface with schema creation
  - DuckDbConnector implementation with transaction support
  - DuckDbTelemetryWriter wrapping connector (implements ITelemetryWriter)
  - Telemetry table schema: SessionId, Timestamp, Speed, Fuel, Brake, Throttle, Steering, TyreTemps (FL/FR/RL/RR)
  - Batch insert logic with configurable flush intervals
  - 6 unit tests + 3 integration tests (skipped pending native binaries)

- **RecommendationService** - Orchestration layer
  - Wires StrategyEngine + TelemetryWriter + DuckDB
  - Fetches latest samples, evaluates strategy, returns RecommendationResponse
  - 5 unit tests covering overheated tyres, low fuel, no data scenarios

### 🔄 Phase 3: Real-Time API & WebSockets (IN PROGRESS)
**Objective:** Expose recommendations and real-time updates to clients.

**Completed:**
- **GET /api/recommend** - REST endpoint for strategy recommendations
  - sessionId query parameter validation
  - Returns RecommendationResponse with: recommendation text, confidence (0.85 placeholder), timestamp
  - Service injection and error handling
  - 5 unit tests passing
  - 2 integration tests scaffolded (skipped pending WebApplicationFactory)

- **Enhanced StrategyEngine**
  - Constants: TyreOverheatThreshold (110°C), CriticalFuelLevel (2.0L), AvgLapFuelConsumption (1.8L/lap)
  - Logic: tyre temp check → fuel level check → readable recommendations
  - Examples: "Pit now - overheated tyres", "Plan pit in 3 laps", "No action needed"

**In Progress:**
- WebSocket `/ws/state` endpoint for real-time recommendation push

### 📋 Phase 4: AI & Intelligence (PLANNED - Q2 2026)
**Objective:** Local LLM integration for complex reasoning.
- Rules engine for 90% of queries (instant, free)
- Local LLM server support (Ollama/LM Studio on secondary PC)
- Optional cloud LLM integration (OpenAI/Anthropic)
- See LLM Strategy section below for details

### 🧪 Test Coverage
**Current:** 23 tests (18 passing, 5 skipped)
- RecommendationService: 5 passing
- SharedMemoryReader: 6 passing  
- StrategyEngine: 2 passing
- TelemetryWriter/DuckDB: 5 passing
- DuckDB Integration: 3 skipped (needs native binaries)
- API Integration: 2 skipped (needs WebApplicationFactory)

---

## �📁 Project Setup

### Directory Structure
```
LMURaceEngineer/
├── src/
│   ├── LMURaceEngineer.Core/          # Core telemetry reading
│   │   ├── Models/                     # Data structures
│   │   ├── Services/                   # Shared memory reader
│   │   ├── Utilities/                  # Validation, helpers
│   │   └── Configuration/              # Settings
│   │
│   ├── LMURaceEngineer.Ingestion/     # Historical data loading
│   │   ├── Services/                   # DuckDB reader
│   │   ├── Models/                     # Historical data models
│   │   └── Configuration/
│   │
│   ├── LMURaceEngineer.Tests/         # Unit & integration tests
│   └── LMURaceEngineer.Tools/         # Development tools
│       ├── MemoryDumper/              # Reverse engineering
│       ├── TelemetrySimulator/        # Mock data generator
│       └── DuckDBExplorer/            # Schema discovery
│
├── docs/                               # Documentation
├── data/                               # Test data
└── .vscode/                            # VS Code config
```

### NuGet Packages

**Core Project (`LMURaceEngineer.Core.csproj`):**
```xml
<ItemGroup>
  <PackageReference Include="Serilog" Version="3.1.1" />
  <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
  <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
  <PackageReference Include="System.Reactive" Version="6.0.0" />
</ItemGroup>
```

**Ingestion Project (`LMURaceEngineer.Ingestion.csproj`):**
```xml
<ItemGroup>
  <PackageReference Include="DuckDB.NET.Data" Version="1.0.0" />
  <PackageReference Include="Serilog" Version="3.1.1" />
</ItemGroup>
```

**Tests Project (`LMURaceEngineer.Tests.csproj`):**
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
  <PackageReference Include="xunit" Version="2.6.2" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4" />
  <PackageReference Include="Moq" Version="4.20.70" />
</ItemGroup>
```

### Initial Setup Commands
```bash
# Create solution
dotnet new sln -n LMURaceEngineer

# Create projects
dotnet new classlib -n LMURaceEngineer.Core -f net8.0
dotnet new classlib -n LMURaceEngineer.Ingestion -f net8.0
dotnet new xunit -n LMURaceEngineer.Tests -f net8.0
dotnet new console -n MemoryDumper -f net8.0
dotnet new console -n TelemetrySimulator -f net8.0

# Add to solution
dotnet sln add src/LMURaceEngineer.Core
dotnet sln add src/LMURaceEngineer.Ingestion
dotnet sln add src/LMURaceEngineer.Tests
dotnet sln add src/LMURaceEngineer.Tools/MemoryDumper
dotnet sln add src/LMURaceEngineer.Tools/TelemetrySimulator

# Add project references
dotnet add src/LMURaceEngineer.Tests reference src/LMURaceEngineer.Core
dotnet add src/LMURaceEngineer.Ingestion reference src/LMURaceEngineer.Core
```

---

## 📍 Memory Structure Reference

### LMU Shared Memory Layout (72 bytes)
This is the ACTUAL structure we discovered and implemented:

```
OFFSET    SIZE   TYPE      FIELD              DESCRIPTION
─────────────────────────────────────────────────────────
0x00      8      double    Speed              Vehicle speed in m/s
0x08      8      double    Fuel               Fuel level in liters
0x10      8      double    Brake              Brake pedal input (0.0-1.0)
0x18      8      double    Throttle           Throttle pedal input (0.0-1.0)
0x20      8      double    Steering           Steering input (-1.0 to 1.0)
0x28      8      double    TyreFLTemp         Front-left tire temperature (°C)
0x30      8      double    TyreFRTemp         Front-right tire temperature (°C)
0x38      8      double    TyreRLTemp         Rear-left tire temperature (°C)
0x40      8      double    TyreRRTemp         Rear-right tire temperature (°C)
─────────────────────────────────────────────────────────
TOTAL: 72 bytes
```

### Reading the Memory Structure
```csharp
// In SharedMemoryReader.ReadSample(MemoryMappedFile mmf)
var buffer = new byte[72];
using (var accessor = mmf.CreateViewAccessor(0, 72))
{
    accessor.ReadArray(0, buffer, 0, 72);
    
    var speed = BitConverter.ToDouble(buffer, 0x00);
    var fuel = BitConverter.ToDouble(buffer, 0x08);
    var brake = BitConverter.ToDouble(buffer, 0x10);
    var throttle = BitConverter.ToDouble(buffer, 0x18);
    var steering = BitConverter.ToDouble(buffer, 0x20);
    var tyreFLTemp = BitConverter.ToDouble(buffer, 0x28);
    var tyreFRTemp = BitConverter.ToDouble(buffer, 0x30);
    var tyreRLTemp = BitConverter.ToDouble(buffer, 0x38);
    var tyreRRTemp = BitConverter.ToDouble(buffer, 0x40);
    
    return new TelemetrySample { ... };
}
```

### DuckDB Storage Schema
```sql
CREATE TABLE IF NOT EXISTS Telemetry (
    Id INTEGER PRIMARY KEY,
    SessionId VARCHAR,
    Timestamp TIMESTAMP,
    SpeedMps DOUBLE,
    FuelLitres DOUBLE,
    BrakePedal DOUBLE,
    ThrottlePedal DOUBLE,
    SteeringInput DOUBLE,
    TyreFLTempC DOUBLE,
    TyreFRTempC DOUBLE,
    TyreRLTempC DOUBLE,
    TyreRRTempC DOUBLE
);

CREATE INDEX idx_telemetry_session ON Telemetry(SessionId);
CREATE INDEX idx_telemetry_timestamp ON Telemetry(Timestamp);
```

### Connector Configuration
```csharp
// DuckDbConnector defaults
private const string DefaultPath = ":memory:";
private const int DefaultBatchSize = 1000;
private const int DefaultFlushIntervalMs = 5000;

// Custom initialization
var connector = new DuckDbConnector(
    databasePath: "telemetry.db",
    batchSize: 500,       // Smaller batches for real-time accuracy
    flushIntervalMs: 2000 // More frequent flushes
);
```

---

## 🏗️ Core Architecture

### Component Diagram
```
┌──────────────────────────────────────────┐
│       LMU SHARED MEMORY (100Hz)          │
└─────────────────┬────────────────────────┘
                  │
                  ▼
┌──────────────────────────────────────────┐
│    SharedMemoryReader Service            │
│  • Connect to memory-mapped file         │
│  • Poll at 100Hz                         │
│  • Parse binary structure                │
│  • Validate data                         │
└─────────────────┬────────────────────────┘
                  │
                  ▼
┌──────────────────────────────────────────┐
│    TelemetryData Model (Validated)       │
└─────────────────┬────────────────────────┘
                  │
       ┌──────────┴──────────┬──────────────┐
       ▼                     ▼              ▼
┌─────────────┐   ┌──────────────┐   ┌──────────────┐
│ Broadcaster │   │ DuckDB       │   │ Future:      │
│ (Events)    │   │ Logger       │   │ • ML Engine  │
└─────────────┘   └──────────────┘   │ • UI Display │
                                      └──────────────┘
```

### Data Flow
```
Real-Time:  LMU → Shared Memory → Reader → Validation → Broadcast → UI
Historical: DuckDB Files → Ingestion → Parser → Training Data
```

---

## 📊 Data Models

### Complete Code - Copy to Your Project

**`src/LMURaceEngineer.Core/Models/TelemetryData.cs`**
```csharp
using System;
using System.Runtime.InteropServices;

namespace LMURaceEngineer.Core.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TelemetryData
    {
        public DateTime Timestamp { get; set; }
        public int VersionNumber { get; set; }
        public SessionInfo Session { get; set; }
        public VehicleData Vehicle { get; set; }
        public TimingData Timing { get; set; }
        public TireSet Tires { get; set; }
        public WeatherData Weather { get; set; }
        public GameState State { get; set; }
        public OpponentInfo[] Opponents { get; set; }
        public bool IsValid { get; set; }
    }
}
```

**`src/LMURaceEngineer.Core/Models/VehicleData.cs`**
```csharp
namespace LMURaceEngineer.Core.Models
{
    public struct VehicleData
    {
        // Speed & Motion
        public float Speed { get; set; }  // m/s
        public float SpeedKph => Speed * 3.6f;
        public float SpeedMph => Speed * 2.23694f;
        
        // Engine
        public float RPM { get; set; }
        public float MaxRPM { get; set; }
        public int Gear { get; set; }
        public float EngineTemp { get; set; }
        
        // Inputs (0-1 normalized)
        public float Throttle { get; set; }
        public float Brake { get; set; }
        public float Clutch { get; set; }
        public float Steering { get; set; }  // -1 to 1
        
        // Fuel (CRITICAL)
        public float FuelLevel { get; set; }
        public float FuelCapacity { get; set; }
        public float FuelLevelPercent => (FuelLevel / FuelCapacity) * 100f;
        
        // Damage
        public DamageData Damage { get; set; }
        
        // Position
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
    }
    
    public struct DamageData
    {
        public float Aerodynamics { get; set; }
        public float Engine { get; set; }
        public float Transmission { get; set; }
        public float Suspension { get; set; }
    }
    
    public struct Vector3
    {
        public float X, Y, Z { get; set; }
    }
}
```

**`src/LMURaceEngineer.Core/Models/TireData.cs`**
```csharp
namespace LMURaceEngineer.Core.Models
{
    public struct TireSet
    {
        public TireData FrontLeft { get; set; }
        public TireData FrontRight { get; set; }
        public TireData RearLeft { get; set; }
        public TireData RearRight { get; set; }
        
        public float AverageWear => (FrontLeft.Wear + FrontRight.Wear + 
                                      RearLeft.Wear + RearRight.Wear) / 4f;
        public float AverageTemp => (FrontLeft.Temperature + FrontRight.Temperature + 
                                      RearLeft.Temperature + RearRight.Temperature) / 4f;
    }
    
    public struct TireData
    {
        // Temperature (critical for grip)
        public float Temperature { get; set; }  // Surface, Celsius
        public float TempInner { get; set; }
        public float TempMiddle { get; set; }
        public float TempOuter { get; set; }
        
        // Pressure
        public float Pressure { get; set; }  // kPa
        
        // Wear (CRITICAL for strategy)
        public float Wear { get; set; }  // 0-100%, 100=fresh
        
        // Compound
        public TireCompound Compound { get; set; }
        public int LapsOnTire { get; set; }
        
        // Condition
        public bool Detached { get; set; }
        public bool Flat { get; set; }
        
        public bool IsInOptimalTemp => Temperature >= 85f && Temperature <= 105f;
        public bool NeedsAttention => Wear < 20f || Flat || Detached;
    }
    
    public enum TireCompound
    {
        Unknown, Hard, Medium, Soft, Intermediate, Wet
    }
}
```

**`src/LMURaceEngineer.Core/Models/TimingData.cs`**
```csharp
namespace LMURaceEngineer.Core.Models
{
    public struct TimingData
    {
        // Position
        public int Position { get; set; }
        public int PositionInClass { get; set; }
        public int TotalVehicles { get; set; }
        
        // Laps
        public int LapsCompleted { get; set; }
        public float CurrentLapTime { get; set; }
        public float LastLapTime { get; set; }
        public float BestLapTime { get; set; }
        
        // Sectors
        public float[] CurrentSectorTimes { get; set; }
        public float[] LastSectorTimes { get; set; }
        public float[] BestSectorTimes { get; set; }
        public int CurrentSector { get; set; }
        
        // Gaps (CRITICAL for strategy)
        public float GapToLeader { get; set; }
        public float GapToAhead { get; set; }
        public float GapToBehind { get; set; }
        
        // Validity
        public bool LastLapValid { get; set; }
        public bool CurrentLapValid { get; set; }
    }
}
```

**`src/LMURaceEngineer.Core/Models/WeatherData.cs`**
```csharp
namespace LMURaceEngineer.Core.Models
{
    public struct WeatherData
    {
        public float TrackTemp { get; set; }
        public float AirTemp { get; set; }
        public WeatherType CurrentWeather { get; set; }
        public float RainIntensity { get; set; }
        public float Wetness { get; set; }
        public int TimeOfDay { get; set; }
        
        public bool NeedsWetTires => RainIntensity > 0.3f || Wetness > 0.5f;
        public bool IsDrying => Wetness < 0.5f && Wetness > 0.2f && RainIntensity < 0.1f;
    }
    
    public enum WeatherType
    {
        Clear, PartlyCloudy, Overcast, LightRain, HeavyRain, Storm
    }
}
```

**`src/LMURaceEngineer.Core/Models/SessionInfo.cs` & `GameState.cs`**
```csharp
namespace LMURaceEngineer.Core.Models
{
    public struct SessionInfo
    {
        public SessionType Type { get; set; }
        public SessionPhase Phase { get; set; }
        public string TrackName { get; set; }
        public float TrackLength { get; set; }
        public int MaxLaps { get; set; }
        public string CarName { get; set; }
    }
    
    public enum SessionType { Practice, Qualify, Race }
    public enum SessionPhase { Garage, Formation, GreenFlag, Finished }
    
    public struct GameState
    {
        public bool InSession { get; set; }
        public FlagType CurrentFlag { get; set; }
        public bool InPitLane { get; set; }
        public PitState PitState { get; set; }
        public bool HasPenalty { get; set; }
    }
    
    public enum FlagType { None, Green, Yellow, Blue, Checkered }
    public enum PitState { None, Entering, InPit, Servicing, Exiting }
}
```

---

## 🔧 Shared Memory Reader

### Interface

**`src/LMURaceEngineer.Core/Services/ISharedMemoryReader.cs`**
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using LMURaceEngineer.Core.Models;

namespace LMURaceEngineer.Core.Services
{
    public interface ISharedMemoryReader : IDisposable
    {
        bool IsConnected { get; }
        int PollingFrequency { get; }
        
        Task StartAsync(int frequencyHz = 100, CancellationToken token = default);
        Task StopAsync();
        TelemetryData? GetLatestTelemetry();
        
        event EventHandler<TelemetryData> OnTelemetryUpdate;
        event EventHandler<bool> OnConnectionStateChanged;
        event EventHandler<Exception> OnError;
    }
}
```

### Implementation Core

**`src/LMURaceEngineer.Core/Services/SharedMemoryReader.cs`** (Key Methods)
```csharp
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Serilog;

namespace LMURaceEngineer.Core.Services
{
    public class SharedMemoryReader : ISharedMemoryReader
    {
        private const string MEMORY_NAME = "Local\\LMU_Telemetry";  // TBD
        private const int MEMORY_SIZE = 8192;  // TBD
        
        private MemoryMappedFile? _memoryFile;
        private MemoryMappedViewAccessor? _accessor;
        private TelemetryData? _latestTelemetry;
        
        public event EventHandler<TelemetryData>? OnTelemetryUpdate;
        
        private async Task<bool> TryConnectAsync()
        {
            try
            {
                _memoryFile = MemoryMappedFile.OpenExisting(MEMORY_NAME);
                _accessor = _memoryFile.CreateViewAccessor(0, MEMORY_SIZE, 
                                                            MemoryMappedFileAccess.Read);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }
        
        private TelemetryData? ReadTelemetry()
        {
            if (_accessor == null) return null;
            
            var buffer = new byte[MEMORY_SIZE];
            _accessor.ReadArray(0, buffer, 0, MEMORY_SIZE);
            
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var data = Marshal.PtrToStructure<TelemetryData>(
                    handle.AddrOfPinnedObject());
                data.Timestamp = DateTime.UtcNow;
                return data;
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
```

### Data Validator

**`src/LMURaceEngineer.Core/Utilities/DataValidator.cs`**
```csharp
using LMURaceEngineer.Core.Models;

namespace LMURaceEngineer.Core.Utilities
{
    public static class DataValidator
    {
        public static bool IsValid(TelemetryData data)
        {
            // Speed check (0-120 m/s = ~430 kph max)
            if (data.Vehicle.Speed < 0 || data.Vehicle.Speed > 120)
                return false;
            
            // RPM check
            if (data.Vehicle.RPM < 0 || data.Vehicle.RPM > 25000)
                return false;
            
            // Fuel check
            if (data.Vehicle.FuelLevel < 0 || 
                data.Vehicle.FuelLevel > data.Vehicle.FuelCapacity)
                return false;
            
            // Tire wear (0-100%)
            if (data.Tires.FrontLeft.Wear < 0 || data.Tires.FrontLeft.Wear > 100)
                return false;
            
            // Input range (0-1)
            if (data.Vehicle.Throttle < 0 || data.Vehicle.Throttle > 1)
                return false;
            
            return true;
        }
        
        public static bool IsSessionActive(TelemetryData data)
        {
            return data.State.InSession && 
                   data.Session.Phase != SessionPhase.Garage;
        }
    }
}
```

---

## 💾 Historical Data Ingestion

### DuckDB Reader

**`src/LMURaceEngineer.Ingestion/Services/DuckDBTelemetryReader.cs`**
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using LMURaceEngineer.Ingestion.Models;
using Serilog;

namespace LMURaceEngineer.Ingestion.Services
{
    public class DuckDBTelemetryReader
    {
        private readonly string _telemetryPath;
        
        public DuckDBTelemetryReader()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _telemetryPath = Path.Combine(userProfile, 
                "Documents", "Le Mans Ultimate", "Telemetry");
        }
        
        public async Task<List<HistoricalSession>> DiscoverSessionsAsync()
        {
            if (!Directory.Exists(_telemetryPath))
                return new List<HistoricalSession>();
            
            var sessions = new List<HistoricalSession>();
            var files = Directory.GetFiles(_telemetryPath, "*.duckdb", 
                                           SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                var session = await ExtractSessionMetadataAsync(file);
                if (session != null)
                    sessions.Add(session);
            }
            
            return sessions.OrderByDescending(s => s.StartTime).ToList();
        }
        
        private async Task<HistoricalSession?> ExtractSessionMetadataAsync(string filePath)
        {
            using var connection = new DuckDBConnection($"Data Source={filePath};");
            await connection.OpenAsync();
            
            using var cmd = connection.CreateCommand();
            // NOTE: SQL query will be determined after reverse engineering
            cmd.CommandText = "SELECT session_id, track_name, car_name FROM sessions LIMIT 1";
            
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new HistoricalSession
                {
                    SessionId = reader.GetString(0),
                    FilePath = filePath,
                    TrackName = reader.GetString(1),
                    CarName = reader.GetString(2)
                };
            }
            
            return null;
        }
    }
}
```

### Historical Models

**`src/LMURaceEngineer.Ingestion/Models/HistoricalSession.cs`**
```csharp
namespace LMURaceEngineer.Ingestion.Models
{
    public class HistoricalSession
    {
        public string SessionId { get; set; }
        public string FilePath { get; set; }
        public string TrackName { get; set; }
        public string CarName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalLaps { get; set; }
        public float BestLapTime { get; set; }
    }
    
    public class LapSummary
    {
        public int LapNumber { get; set; }
        public float LapTime { get; set; }
        public float FuelUsed { get; set; }
        public float TireWearDelta { get; set; }
        public float TrackTemp { get; set; }
        public bool IsValid { get; set; }
        
        // For ML training
        public Dictionary<string, float> ToFeatureVector()
        {
            return new Dictionary<string, float>
            {
                ["fuel_used"] = FuelUsed,
                ["tire_wear_delta"] = TireWearDelta,
                ["track_temp"] = TrackTemp
            };
        }
    }
}
```

---

## 🔍 Reverse Engineering Reference

> ⚠️ **Phase 5 Future Tools** - The reverse engineering tools below are planned for Phase 5 development. The LMU memory structure has already been discovered and implemented (see [Memory Structure Reference](#-memory-structure-reference) above). This section documents the methodology for future updates or extending to other simulators.

### Already Discovered: LMU Shared Memory Structure

The 72-byte telemetry structure has been reverse-engineered and is now in production use:
- **Location:** `Local\LMU_Telemetry` shared memory map
- **Update Rate:** 100Hz (10ms intervals)
- **Content:** Speed, Fuel, Controls (Brake, Throttle, Steering), Tire Temps (4x)
- **Full Reference:** See [Memory Structure Reference](#-memory-structure-reference) section

### Reverse Engineering Methodology (for Reference)

This section describes the process used to discover the structure. These tools are planned for Phase 5 to enable:
- Discovery of new telemetry fields
- Support for other simulators (rFactor2, Assetto Corsa, etc.)
- Real-time memory inspection during development

#### 1. MemoryDumper Tool (Phase 5 - Not Yet Implemented)

**Planned Purpose:** Automated memory dump collection for binary analysis

```csharp
// PLANNED IMPLEMENTATION - Phase 5
// Tools planned but not yet created in this repository
using System;
using System.IO;
using System.IO.MemoryMappedFiles;

class Program
{
    // This demonstrates the intended design for Phase 5 tooling
}
```

**Intended Usage:**
1. Run MemoryDumper while LMU is active
2. Performs known actions in LMU:
   - Accelerate → speed field changes
   - Brake hard → brake field changes  
   - Pit → fuel consumption
3. Export dumps as binary files
4. Use hex editor (HxD, 010 Editor) to compare before/after dumps
5. Document field offsets as discovered

#### 2. Hex Analysis Process

#### 3. Example Memory Map

```
OFFSET    SIZE   TYPE    FIELD
0x0000    4      int32   Version
0x0010    4      float   Speed (m/s)
0x0014    4      float   RPM
0x0020    4      float   Fuel Level
0x0024    4      float   Fuel Capacity
0x0100    4      float   Tire FL Temperature
0x0104    4      float   Tire FL Pressure
0x0108    4      float   Tire FL Wear
...
```

#### 4. Update Models with Field Offsets

```csharp
[StructLayout(LayoutKind.Explicit)]
public struct TelemetryData
{
    [FieldOffset(0x0000)] public int VersionNumber;
    [FieldOffset(0x0010)] public float Speed;
    [FieldOffset(0x0014)] public float RPM;
    [FieldOffset(0x0020)] public float FuelLevel;
    // ... continue mapping
}
```

### Telemetry Simulator (for testing without LMU)

**`src/LMURaceEngineer.Tools/TelemetrySimulator/Program.cs`**
```csharp
using System.IO.MemoryMappedFiles;
using LMURaceEngineer.Core.Models;

class Program
{
    static void Main()
    {
        const string MEMORY_NAME = "Local\\LMU_Telemetry";
        const int MEMORY_SIZE = 8192;
        
        using var mmf = MemoryMappedFile.CreateNew(MEMORY_NAME, MEMORY_SIZE);
        using var accessor = mmf.CreateViewAccessor();
        
        Console.WriteLine("Simulating LMU telemetry at 100Hz...");
        
        var simulator = new RaceSimulator();
        while (true)
        {
            var data = simulator.GenerateFrame();
            WriteTelemetry(accessor, data);
            
            Console.Write($"\rLap {data.Timing.LapsCompleted} | " +
                         $"Fuel: {data.Vehicle.FuelLevel:F1}L | " +
                         $"Speed: {data.Vehicle.SpeedKph:F0} kph");
            
            Thread.Sleep(10); // 100Hz
        }
    }
}

class RaceSimulator
{
    private float _lapTime = 0, _fuel = 80f, _tireWear = 100f;
    private int _lap = 0;
    
    public TelemetryData GenerateFrame()
    {
        _lapTime += 0.01f;
        if (_lapTime > 90f)
        {
            _lap++;
            _lapTime = 0;
            _fuel -= 3.2f;
            _tireWear -= 1.5f;
        }
        
        return new TelemetryData
        {
            Vehicle = new VehicleData
            {
                Speed = 50 + new Random().Next(0, 60),
                RPM = 7000,
                FuelLevel = _fuel,
                FuelCapacity = 90f
            },
            Timing = new TimingData
            {
                LapsCompleted = _lap,
                CurrentLapTime = _lapTime
            },
            Tires = new TireSet
            {
                FrontLeft = new TireData { Wear = _tireWear }
            }
        };
    }
}
```

---

## ✅ Testing & Validation

### Unit Tests

**`src/LMURaceEngineer.Tests/Unit/DataValidatorTests.cs`**
```csharp
using Xunit;
using LMURaceEngineer.Core.Models;
using LMURaceEngineer.Core.Utilities;

public class DataValidatorTests
{
    [Fact]
    public void ValidTelemetry_PassesValidation()
    {
        var data = CreateValidData();
        Assert.True(DataValidator.IsValid(data));
    }
    
    [Theory]
    [InlineData(-10)]
    [InlineData(150)]
    public void InvalidSpeed_FailsValidation(float speed)
    {
        var data = CreateValidData();
        data.Vehicle.Speed = speed;
        Assert.False(DataValidator.IsValid(data));
    }
    
    [Theory]
    [InlineData(-5)]
    [InlineData(105)]
    public void InvalidTireWear_FailsValidation(float wear)
    {
        var data = CreateValidData();
        data.Tires.FrontLeft.Wear = wear;
        Assert.False(DataValidator.IsValid(data));
    }
    
    private TelemetryData CreateValidData()
    {
        return new TelemetryData
        {
            Vehicle = new VehicleData
            {
                Speed = 60f,
                RPM = 7000,
                Throttle = 0.8f,
                FuelLevel = 50f,
                FuelCapacity = 90f
            },
            Tires = new TireSet
            {
                FrontLeft = new TireData { Wear = 85f }
            }
        };
    }
}
```

### Integration Test

**`src/LMURaceEngineer.Tests/Integration/SharedMemoryIntegrationTests.cs`**
```csharp
using Xunit;
using LMURaceEngineer.Core.Services;

public class SharedMemoryIntegrationTests
{
    [Fact]
    public async Task Reader_ConnectsToSimulator()
    {
        // Start simulator in separate process
        // Start reader
        // Verify connection
        // Verify data reception
    }
}
```

### Running Tests
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true

# Run specific test
dotnet test --filter "FullyQualifiedName~DataValidatorTests"
```

---

## 💡 Copilot Development Tips

### Effective Prompts

#### Creating Data Models
```
Create a C# struct for tire telemetry with:
- Temperature (surface, inner, middle, outer)
- Pressure in kPa
- Wear as 0-100%
- Tire compound enum
- Boolean flags for flat/detached
- Helper property IsInOptimalTemp (85-105°C)
```

#### Implementing Services
```
Implement ISharedMemoryReader in C# that:
- Opens memory-mapped file "Local\\LMU_Telemetry"
- Polls at configurable Hz (default 100)
- Marshals binary data to TelemetryData struct
- Validates data before broadcasting
- Handles reconnection with exponential backoff
- Thread-safe latest telemetry getter
```

#### Writing Tests
```
Create xUnit tests for DataValidator that verify:
- Valid telemetry passes all checks
- Speed outside 0-120 m/s fails
- RPM outside 0-25000 fails
- Tire wear outside 0-100% fails
- Fuel above capacity fails
Use Theory with InlineData for boundary testing
```

### Copilot Workflow

1. **Start with interfaces** - Copilot excels at implementing against contracts
2. **Use XML comments** - Copilot uses them as context
3. **Write tests first** - Copilot can generate implementation from tests
4. **Incremental development** - Small, focused classes work best
5. **Refactor prompts** - If output isn't right, rephrase and try again

### Example Session
```csharp
// 1. Define interface with XML comments
/// <summary>
/// Reads historical telemetry from LMU DuckDB files
/// </summary>
public interface IHistoricalTelemetryReader
{
    /// <summary>Load all sessions from telemetry directory</summary>
    Task<List<HistoricalSession>> DiscoverSessionsAsync(string path);
}

// 2. Type "public class Duck" and let Copilot autocomplete
// 3. It will suggest full implementation based on interface + comments
```

---

## 🎯 Implementation Roadmap

### Week 1: Foundation
- [ ] Create solution structure
- [ ] Define all data models
- [ ] Write data validator with tests
- [ ] Create configuration classes

### Week 2: Memory Reading
- [ ] Build MemoryDumper tool
- [ ] Run LMU and dump memory
- [ ] Analyze dumps (find speed, RPM, fuel offsets)
- [ ] Create TelemetrySimulator for testing

### Week 3: Core Service
- [ ] Implement SharedMemoryReader
- [ ] Add field offset mappings
- [ ] Test against simulator
- [ ] Test against live LMU
- [ ] Verify 100Hz performance

### Week 4: Historical Data
- [ ] Build DuckDBExplorer tool
- [ ] Analyze LMU database schema
- [ ] Implement DuckDBTelemetryReader
- [ ] Load historical sessions
- [ ] Parse lap summaries

### Week 5: Polish & Testing
- [ ] Comprehensive unit tests (>80% coverage)
- [ ] Integration tests
- [ ] Performance testing
- [ ] Documentation
- [ ] Code review & refactoring

---

## 📚 Additional Resources

### LMU Telemetry Paths
```
Default Location:
%USERPROFILE%\Documents\Le Mans Ultimate\Telemetry\

Typical Structure:
Telemetry/
  ├── sessions/
  │   ├── 20260209_143025.duckdb
  │   └── 20260209_151234.duckdb
  └── exports/
```

### Useful Commands

```bash
# Build all projects
dotnet build

# Run specific tool
dotnet run --project src/LMURaceEngineer.Tools/MemoryDumper

# Watch tests (auto-run on file change)
dotnet watch test

# Clean build artifacts
dotnet clean

# Restore NuGet packages
dotnet restore
```

### Debugging Tips

**VS Code launch.json for Memory Dumper:**
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Debug Memory Dumper",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/src/Tools/MemoryDumper/bin/Debug/net8.0/MemoryDumper.dll",
      "cwd": "${workspaceFolder}",
      "console": "integratedTerminal"
    }
  ]
}
```

---

## 🚦 Development Roadmap

### ✅ Phase 1 (COMPLETE)
**Data Acquisition Foundation**
- Real SharedMemoryReader with 72-byte memory parsing
- Async streaming via IAsyncEnumerable
- 6 unit tests passing

### ✅ Phase 2 (COMPLETE)
**Analytics Backend**
- DuckDB integration with transaction support
- Batch ingestion and schema management
- RecommendationService orchestration layer
- 5 unit tests passing (3 integration tests skipped - needs native binaries)

### 🔄 Phase 3 (IN PROGRESS - Current Focus)
**Real-Time API & Extended Strategy**

**Completed:**
- GET /api/recommend REST endpoint ✅
- RecommendationResponse DTO ✅
- Enhanced StrategyEngine with fuel thresholds ✅
- 5 API tests passing ✅

**Next (This Session):**
- [ ] WebSocket `/ws/state` endpoint for real-time state push
- [ ] Extend strategy rules: wheel lock detection, pit window optimization, brake coaching
- [ ] Confidence scoring system (replace 0.85 placeholder)
- [ ] Integration tests with WebApplicationFactory

### 📅 Phase 4 (PLANNED - Q2 2026)
**Advanced Intelligence & LLM Integration**
- Rules engine for common queries (90% coverage, instant, free)
- Local LLM server support (Ollama/LM Studio)
- Optional cloud LLM integration
- Natural language race engineering
- See [LLM Strategy](#appendix-aiagent-strategy-phase-4-planning) section for detailed architecture

### 🛠️ Phase 5 (FUTURE - Q3 2026)
**Development Tools & Utilities** (Aspirational - Not Yet Implemented)
- MemoryDumper: Reverse engineering tool for memory structure discovery
- TelemetrySimulator: Mock data generator for testing
- DuckDBExplorer: Database schema introspection utility

---

## 📝 Notes & Gotchas

### Common Issues

**1. Shared Memory Not Found**
- LMU must be in an active session (not menus)
- Try different memory names (rFactor2 variants)
- Check Windows permissions

**2. DuckDB Files Not Found**
- LMU must have saved telemetry first
- Check Documents folder permissions
- Verify LMU telemetry is enabled in settings

**3. Performance Issues**
- Validate only when needed (toggle in config)
- Use async/await properly
- Profile with dotnet-trace if < 100Hz

**4. Memory Marshaling Errors**
- Ensure struct layout matches actual memory
- Use StructLayout(LayoutKind.Explicit) with FieldOffset
- Test with known good data first

### Best Practices

- ✅ Always validate telemetry before use
- ✅ Log errors but don't crash on bad data
- ✅ Use thread-safe patterns for shared state
- ✅ Test with simulator before live LMU
- ✅ Document field offsets as you discover them

---

## 📞 Support & Contributing

### Getting Help
1. Check this documentation first
2. Review code comments and XML docs
3. Run relevant tests to isolate issues
4. Use Copilot to explain confusing code

### Documentation Updates
As you reverse-engineer LMU's memory structure, document findings in:
- `docs/memory-structure.md` - Field offsets
- `docs/duckdb-schema.md` - Database tables
- `docs/development-log.md` - Discoveries & gotchas

---

**END OF PHASE 1 DOCUMENTATION**

*Last Updated: February 9, 2026*  
*Version: 1.0*  
*Status: Ready for Development*


---

---

## 🤖 APPENDIX: AI/LLM Strategy (Phase 4 PLANNED - Q2 2026)

> **Status:** Detailed architectural planning for future implementation. Not yet built into the codebase.
> **Timeline:** Phase 4 development to begin after Phase 3 completion (WebSocket, extended rules).
> **Purpose:** Document strategy and design for seamless integration when Phase 4 work begins.

### Overview

The Race Engineer uses a **tiered intelligence system** to balance performance, cost, and capability. Most queries are handled by fast, free rules-based logic, with optional AI for complex reasoning.

---

### Tiered Intelligence Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    USER VOICE QUERY                         │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│         TIER 1: Rules Engine (Local, Instant, Free)         │
│  • Pattern matching on common queries                       │
│  • Deterministic responses using ML predictions             │
│  • Covers 80-90% of racing questions                        │
│  • Response time: <1ms                                       │
└────────────────────┬────────────────────────────────────────┘
                     │
              Can't answer? (10-20% of queries)
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│     TIER 2: Optional LLM (User Configurable)                │
│                                                              │
│  Option A: Cloud LLM (OpenAI/Anthropic)                     │
│  • Cost: $0.01-0.02 per query                               │
│  • Response: 1-2 seconds                                     │
│  • Pros: No hardware needed, always available               │
│  • Cons: Ongoing costs, internet required                   │
│                                                              │
│  Option B: Local LLM Server (Separate PC) ⭐ RECOMMENDED    │
│  • Cost: Free (after hardware)                              │
│  • Response: 0.5-2 seconds                                   │
│  • Pros: Zero cost, no performance impact, privacy          │
│  • Cons: Requires secondary PC/server                       │
│                                                              │
│  Option C: Same-Machine LLM (Ollama)                        │
│  • Cost: Free                                                │
│  • Response: 0.5-2 seconds                                   │
│  • Pros: Simple setup                                        │
│  • Cons: ⚠️ Performance impact on LMU                       │
│  • Restriction: Only when stopped in pit box                │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

---

### Tier 1: Rules Engine (Always Active)

**Handles common queries instantly with zero cost:**

```csharp
// Example pattern matching
"fuel?" / "how much fuel?" 
  → "7.2 laps of fuel remaining"

"should I pit?" / "box now?"
  → Analyzes: fuel laps, tire wear, optimal window, traffic
  → "Box this lap for fuel and tires" OR "Stay out 3 more laps"

"tire status?" / "tire temps?"
  → "FL: 92°C ✓, FR: 95°C ✓, RL: 88°C ⚠️ cold, RR: 91°C ✓"

"gap?" / "position?"
  → "P3. Gap ahead: +2.3s, Gap behind: -4.1s"

"weather?" / "rain?"
  → "Rain expected in 12 minutes, light intensity. Consider inters"

"pace?" / "lap time?"
  → "Last lap: 3:42.1, Best: 3:40.8, Delta: +1.3s"
```

**Query Coverage:**
- ✅ Fuel calculations
- ✅ Tire status and predictions
- ✅ Pit strategy recommendations
- ✅ Gap and position info
- ✅ Weather forecasts
- ✅ Basic performance metrics

**Implementation:** Fast C# pattern matching + ML predictions

---

### Tier 2: LLM Integration (Optional)

**For complex queries rules can't handle:**

```
Examples of LLM-appropriate queries:

"Why am I losing time in sector 2 compared to the leader?"
  → LLM analyzes: speed traces, braking points, line choice
  → Provides detailed explanation and coaching

"My front-left tire is overheating but rears are cold, what's wrong?"
  → LLM considers: setup, driving style, track characteristics
  → Suggests setup changes or driving adjustments

"Should I switch to a two-stop strategy given the weather forecast?"
  → LLM evaluates: weather timing, tire allocation, competitor strategies
  → Provides pros/cons analysis

"Explain why the AI recommended pitting on lap 18 instead of 20"
  → LLM reasons through: fuel margins, traffic, tire cliff, weather
  → Educational explanation of strategy logic
```

---

### Local LLM Server Setup (Recommended Approach)

#### Hardware Requirements

**Minimal Setup (Works!):**
```
Secondary PC/Server:
• CPU: Any modern CPU (even i3/Ryzen 3)
• GPU: GTX 1660 or better (for faster inference)
• RAM: 8GB minimum, 16GB recommended
• Storage: 20GB for models
• Network: Gigabit Ethernet (Wi-Fi works but slower)

Even an old gaming PC or laptop can work!
```

**Recommended Setup:**
```
Secondary PC:
• CPU: i5/Ryzen 5
• GPU: RTX 3060/4060 or AMD equivalent
• RAM: 16GB
• Network: Wired Gigabit connection

Can run larger models (7B-13B parameters) with fast inference
```

#### Software Options

**Option 1: Ollama (Easiest)**
```bash
# Install on secondary PC (Windows/Linux/Mac)
# Download from: https://ollama.ai

# Pull recommended model for racing
ollama pull llama3.2

# Server runs automatically on http://localhost:11434
# Access from racing PC: http://192.168.1.100:11434
```

**Models:** llama3.2 (3B), mistral (7B), mixtral (47B)

**Option 2: LM Studio (GUI, User-Friendly)**
```
1. Download LM Studio: https://lmstudio.ai
2. Download model through UI (Llama, Mistral, Phi)
3. Start local server (OpenAI-compatible API)
4. Default endpoint: http://localhost:1234
```

**Option 3: vLLM (High Performance, Advanced)**
```bash
# For users with powerful GPUs
pip install vllm

python -m vllm.entrypoints.openai.api_server \
    --model meta-llama/Llama-3.2-3B \
    --host 0.0.0.0 --port 8000
```

#### Network Setup

**Simple Home Network:**
```
Router (192.168.1.1)
  │
  ├── Racing PC (192.168.1.50)
  │   • Runs: LMU + Race Engineer Client
  │   • Sends queries to server
  │
  └── Server PC (192.168.1.100)
      • Runs: Ollama with llama3.2
      • Responds to queries
      • Zero impact on racing PC!

Expected latency: 20-50ms on local network
```

**Configuration in Race Engineer:**
```
AI Provider: Local LLM Server
Server Address: 192.168.1.100
Port: 11434
Model: llama3.2
Timeout: 5000ms

[Test Connection] → 🟢 Connected (avg 35ms)
```

---

### Implementation Details

#### LLM Service Interface

```csharp
public interface ILLMService
{
    bool IsEnabled { get; }
    bool IsAvailable { get; }
    LLMProviderType ProviderType { get; }
    
    Task<bool> TestConnectionAsync();
    Task<LLMResponse> QueryAsync(string prompt, RaceContext context);
    
    event EventHandler<bool> OnAvailabilityChanged;
}

public enum LLMProviderType
{
    None,              // Rules engine only
    CloudOpenAI,       // OpenAI API
    CloudAnthropic,    // Claude API
    LocalOllama,       // Ollama on same machine
    LocalServer,       // Ollama/LM Studio on different machine ⭐
    CustomEndpoint     // User-specified API
}
```

#### Hybrid Query Processing

```csharp
public class HybridRaceEngineer
{
    private RulesEngine _rules;
    private ILLMService _llm;
    
    public async Task<string> ProcessVoiceQuery(string query)
    {
        // ALWAYS try rules first (fast & free)
        var rulesResponse = _rules.TryAnswer(query, _raceContext);
        
        if (rulesResponse != null)
        {
            // 90% of queries answered here
            PlayAudio(rulesResponse);
            return rulesResponse;
        }
        
        // Rules couldn't handle it - check if LLM available
        if (_llm?.IsEnabled == true && _llm.IsAvailable)
        {
            // Play acknowledgment
            PlayAudio("Let me check that for you...");
            
            // Query LLM (1-2 second response)
            var llmResponse = await _llm.QueryAsync(query, _raceContext);
            
            if (llmResponse.Success)
            {
                PlayAudio(llmResponse.Text);
                return llmResponse.Text;
            }
        }
        
        // Fallback if no LLM or LLM failed
        return "I don't have enough information for that question.";
    }
}
```

#### Context Building for LLM

```csharp
private string BuildRaceContext(RaceContext ctx)
{
    return $@"You are a professional Le Mans endurance race engineer.

CURRENT SITUATION:
- Track: {ctx.TrackName}
- Car: {ctx.CarName}
- Lap: {ctx.CurrentLap}/{ctx.TotalLaps}
- Position: P{ctx.Position}
- Fuel: {ctx.FuelLevel:F1}L ({ctx.FuelLapsRemaining:F1} laps remaining)
- Tires: {ctx.TireCompound}, {ctx.TireLaps} laps old, {ctx.TireWear:F0}% remaining
- Weather: {ctx.Weather}, Track {ctx.TrackTemp}°C

PREDICTIONS (from ML models):
- Optimal pit lap: {ctx.OptimalPitLap}
- Tire cliff expected: Lap {ctx.TireCliffLap}
- Weather forecast: {ctx.WeatherForecast}

DRIVER PERFORMANCE (last 5 laps):
- Average lap time: {ctx.AvgLapTime:F3}
- Consistency: {ctx.Consistency}
- Fuel consumption: {ctx.AvgFuelPerLap:F2}L/lap

STRATEGY:
- Pit stops remaining: {ctx.PitStopsRemaining}
- Current strategy: {ctx.Strategy}

Respond as a concise, professional race engineer. Be direct and actionable.
Focus on what the driver needs to know right now.";
}
```

---

### User Configuration UI

```
┌─ AI & Intelligence Settings ──────────────────────────────┐
│                                                            │
│ Intelligence Level:                                        │
│                                                            │
│ ● Basic (Rules Engine Only - FREE)                        │
│   ✓ Instant responses                                     │
│   ✓ Covers fuel, tires, pit strategy, weather            │
│   ✓ Zero cost, zero performance impact                    │
│   ✓ Works offline                                         │
│                                                            │
│ ○ Advanced (Basic + Cloud AI)                             │
│   All Basic features plus:                                │
│   + Complex reasoning and explanations                    │
│   + Performance analysis and coaching                     │
│   + Setup recommendations                                 │
│   ⚠️ Requires internet, ~$1-3/month                       │
│                                                            │
│   Provider: [OpenAI GPT-4 ▼] [Anthropic Claude ▼]        │
│   API Key:  [________________________]  [Test]            │
│                                                            │
│ ○ Advanced (Basic + Local AI Server) ⭐ RECOMMENDED       │
│   All Basic features plus:                                │
│   + Complex reasoning and explanations                    │
│   + No ongoing costs after setup                          │
│   + Complete privacy (data stays local)                   │
│   + No performance impact on racing PC                    │
│   ℹ️ Requires secondary PC/server on network              │
│                                                            │
│   Server Address: [192.168.1.100___]                      │
│   Port:          [11434]                                  │
│   Model:         [llama3.2 ▼]                             │
│                                                            │
│   [Scan Network for Servers]                              │
│   Status: 🟢 Connected (avg 42ms response)               │
│   [Test Connection]                                       │
│                                                            │
│ ○ Advanced (Basic + Local AI - Same PC)                   │
│   ⚠️ May impact game performance                          │
│   ⚠️ Only available when stopped in pit                   │
│   Model: [llama3.2 ▼]                                     │
│                                                            │
│ ─────────────────────────────────────────────────────     │
│                                                            │
│ Safety Settings:                                          │
│ [✓] Only query AI when stopped in pit box                │
│ [ ] Allow AI queries while driving (may cause lag)       │
│ Max response time: [3000] ms (queries timeout if slower)  │
│                                                            │
│ ─────────────────────────────────────────────────────     │
│                                                            │
│ Session Statistics:                                       │
│ • Rules engine responses: 47                              │
│ • AI queries: 3                                           │
│ • Average AI response time: 1,234ms                       │
│ • Cost this session: $0.06                                │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

---

### Performance Comparison

| Method | Response Time | Cost/Query | Racing PC Impact | Setup Difficulty |
|--------|---------------|------------|------------------|------------------|
| **Rules Engine** | <1ms | $0 | None | None |
| **Cloud LLM** | 1-2s | $0.01-0.02 | None | Easy (API key) |
| **Local Server LLM** ⭐ | 0.5-2s | $0 | None | Medium (2nd PC) |
| **Same-Machine LLM** | 0.5-2s | $0 | ⚠️ -10-30 FPS | Easy (Software) |

---

### Recommended Models by Use Case

**Fast Racing (Minimal Latency):**
- `llama3.2` (3B params) - 500-800ms inference
- `phi-3-mini` (3.8B params) - 600-900ms inference

**Balanced (Good Quality):**
- `mistral` (7B params) - 1000-1500ms inference
- `llama3.1` (8B params) - 1200-1600ms inference

**Best Quality (Slower):**
- `mixtral` (47B params) - 2000-4000ms inference
- For post-race analysis, not real-time use

---

### Network Auto-Discovery

The Race Engineer can automatically find LLM servers on your network:

```
[Scan Network for LLM Servers]

Scanning 192.168.1.0/24...

Found servers:
● 192.168.1.100:11434 (Ollama)
  Models: llama3.2, mistral
  Latency: 35ms
  [Use This Server]

● 192.168.1.101:1234 (LM Studio)
  Models: phi-3-mini
  Latency: 42ms
  [Use This Server]

[Refresh] [Add Manual Server]
```

---

### Development Roadmap

**Phase 4a: Rules Engine (Essential)**
- Pattern matching for common queries
- Integration with ML predictions
- Voice synthesis for responses
- **Deliverable:** 90% query coverage, zero cost

**Phase 4b: Cloud LLM Integration (Optional)**
- OpenAI/Anthropic API integration
- Cost tracking and limits
- Fallback to rules on failure
- **Deliverable:** Complex query handling, $1-3/month cost

**Phase 4c: Local Server LLM (Recommended)**
- HTTP client for Ollama/LM Studio
- Network discovery
- Connection testing and monitoring
- **Deliverable:** Free AI, zero racing PC impact

**Phase 4d: Same-Machine LLM (Advanced)**
- Direct Ollama integration
- Safety restrictions (pit-only)
- Performance monitoring
- **Deliverable:** No 2nd PC needed, performance trade-off

---

### Example User Journey

**User Setup:**
1. Install Race Engineer on racing PC
2. Install Ollama on old gaming PC
3. Pull `llama3.2` model
4. In Race Engineer, scan network → finds server
5. Test connection → ✅ 38ms latency
6. Enable Local Server LLM

**During Race:**
```
Lap 5:  "Fuel?" 
        → Rules: "7.2 laps remaining" (instant)

Lap 12: "Should I pit?"
        → Rules: "Stay out 3 more laps, pit on lap 15" (instant)

Lap 15: "Why is my lap time dropping?"
        → LLM: "Your front-left tire is 3% below optimal temp. 
                Increase brake bias forward by 1 click to heat it up. 
                Also seeing slight degradation - normal for lap 15 
                on mediums." (1.2s response)

Pit:    "Analyze my stint pace compared to optimal"
        → LLM: [Detailed analysis with suggestions] (1.8s response)
```

**Cost:** $0.00
**Performance Impact:** 0 FPS lost
**Total Queries:** 45 (42 rules, 3 LLM)

---

### Key Takeaways

1. **Rules Engine handles 90%** of queries instantly and free
2. **Local Server LLM** is the sweet spot for advanced features
3. **No performance impact** on racing PC with server approach
4. **Users choose their level** - from free to AI-powered
5. **No mandatory costs** - full functionality without cloud AI

---

**END OF AI/LLM APPENDIX**

*This appendix covers Phase 4 planning. Implementation details will be expanded when Phase 4 development begins.*

