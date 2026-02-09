# LMU Race Engineer - Phase 1: Shared Memory Reader
## Complete Development Documentation

**Version:** 1.0 | **Date:** February 9, 2026  
**Platform:** Windows | **Framework:** .NET 8.0 | **IDE:** VS Code + Copilot

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
.NET 8.0 + C#
├── System.IO.MemoryMappedFiles (shared memory)
├── DuckDB.NET.Data (historical data)
├── Serilog (logging)
├── System.Reactive (event streaming)
└── xUnit + Moq (testing)
```

### Success Criteria
- [ ] Connect to LMU shared memory
- [ ] Read all critical fields (fuel, tires, timing)
- [ ] Maintain 100Hz without dropped frames  
- [ ] Load historical sessions from DuckDB
- [ ] Zero performance impact on LMU

---

## 📁 Project Setup

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

## 🔍 Reverse Engineering Guide

### Step-by-Step Process

#### 1. Find Shared Memory Name

Run **MemoryDumper** tool while LMU is running:

**`src/LMURaceEngineer.Tools/MemoryDumper/Program.cs`**
```csharp
using System;
using System.IO;
using System.IO.MemoryMappedFiles;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== LMU Memory Dumper ===\n");
        
        var possibleNames = new[]
        {
            "Local\\LMU_Telemetry",
            "Local\\rFactor2SMMP_Telemetry",
            "$rFactor2SMMP_Telemetry$",
            "Local\\rF2"
        };
        
        foreach (var name in possibleNames)
        {
            Console.Write($"Trying: {name}... ");
            if (TryDump(name))
            {
                Console.WriteLine("FOUND!");
                ContinuousDump(name);
                return;
            }
            Console.WriteLine("not found");
        }
    }
    
    static bool TryDump(string name)
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(name);
            using var accessor = mmf.CreateViewAccessor();
            
            var buffer = new byte[accessor.Capacity];
            accessor.ReadArray(0, buffer, 0, (int)accessor.Capacity);
            
            File.WriteAllBytes($"dump_{DateTime.Now:HHmmss}.bin", buffer);
            return true;
        }
        catch { return false; }
    }
    
    static void ContinuousDump(string name)
    {
        Directory.CreateDirectory("dumps");
        using var mmf = MemoryMappedFile.OpenExisting(name);
        using var accessor = mmf.CreateViewAccessor();
        
        int count = 0;
        while (true)
        {
            var buffer = new byte[accessor.Capacity];
            accessor.ReadArray(0, buffer, 0, (int)accessor.Capacity);
            
            File.WriteAllBytes($"dumps/dump_{count:D5}.bin", buffer);
            Console.Write($"\rDumps: {++count}");
            
            Thread.Sleep(1000);
        }
    }
}
```

#### 2. Analyze Memory Dumps

1. **Start LMU** and enter a session
2. **Run MemoryDumper** - it will create continuous dumps
3. **Perform known actions** in LMU:
   - Accelerate/brake → Find speed field
   - Shift gears → Find RPM field
   - Pit stop → Find fuel field
   - Complete laps → Find tire wear

4. **Use hex editor** (HxD, 010 Editor):
   - Compare dumps before/after actions
   - Look for changing values
   - Note byte offsets

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

## 🚦 Next Steps

### After Phase 1 Completion

**Phase 2: UI & Visualization**
- WPF/Avalonia main application window
- Real-time telemetry display
- Overlay system for in-game notifications
- Professional pit wall UI design

**Phase 3: ML Predictions**
- Fuel consumption predictor
- Tire degradation model
- Optimal pit window calculator
- Weather impact analysis

**Phase 4: LLM Integration**
- OpenAI/Claude API integration
- Voice command system (Azure Speech)
- Natural language race engineer
- Strategy recommendations

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

