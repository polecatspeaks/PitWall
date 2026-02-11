# Root Cause Analysis: Dashboard Brake = 0% & Gentle Telemetry Graphs

**Status**: 🔍 INVESTIGATION PHASE  
**Date**: 2026-02-10  
**Scope**: Two related data display issues affecting lap 3 analysis

---

## Problem Statement

### Issue #1: Dashboard Brake Gauge Stuck at 0%
- **Observed**: Speed drops from 100+ kph to 30 kph, but brake gauge remains 0%
- **Expected**: Brake gauge should show 70-73% (confirmed in database for lap 3)
- **Duration**: Persistent during replay; throttle & steering work correctly
- **Tools Working**: Throttle and steering gauges display correctly
- **Impact**: Critical for real-time feedback during telemetry analysis

### Issue #2: Telemetry Graphs Too Gentle  
- **Observed**: Graphs show smooth curves (like casual town driving)
- **Expected**: Sharp peaks/valleys reflecting GT3 aggressive racing at Silverstone
- **Context**: Occurs when viewing lap 3 data via telemetry analysis tab
- **Database Status**: Confirmed 70-73% brake values exist in database
- **Impact**: Undermines race analysis - data looks insufficient for problem diagnosis

**Correlation**: Both problems affect the same data (brake) and related lap (lap 3). Likely **single root cause** with dual manifestation.

---

## Data Flow Architecture

### Complete Pipeline
```
Database (Brake Pos table: 0-100 scale)
    ↓
LmuTelemetryReader.ReadSamplesAsync() [scales 0-100 → 0-1]
    ↓
TelemetrySample.Brake (0-1 range)
    ↓
API /ws/state WebSocket [serializes as JSON "brake": 0.XX]
    ↓
TelemetryStreamClient.ConnectAsync() [receives bytes]
    ↓
TelemetryMessageParser.Parse(json) [deserializes to TelemetrySampleDto]
    ↓
TelemetrySampleDto.BrakePosition (0-1 value) [via JsonPropertyName]
    ↓
MainWindowViewModel.ApplyTelemetry() → TelemetryBuffer.Add()
    ↓
Dashboard.UpdateTelemetry() → BrakePercent = brake * 100
OR
TelemetryAnalysisViewModel.LoadCurrentLapData() → PopulateDataSeries()
```

---

## Data Flow Points: Code Review Findings

### ✓ Database → Core Layer (LmuTelemetryReader) ✓ VERIFIED CORRECT
**File**: `PitWall.Core/Services/LmuTelemetryReader.cs:172-174`
```csharp
var brake = GetDouble(reader, 4) / 100.0;  // Correct: 0-100 → 0-1
// ...
yield return new TelemetrySample(..., brake, ...)
```
**Status**: ✅ Correctly scales 0-100 to 0-1

### ✓ Core → API Layer (Program.cs WebSocket) ✓ VERIFIED CORRECT  
**File**: `PitWall.Api/Program.cs:219-224`
```csharp
var payload = JsonSerializer.Serialize(new {
    // ...
    brake = sample.Brake,  // ✅ 0-1 value
    // ...
});
```
**Status**: ✅ Correctly sends brake (0-1) in JSON

### ⚠️ API → Network (JSON Format) - REQUIRES VERIFICATION
**Assumption**: JSON format is `{ "brake": 0.723, ... }`  
**Risk**: If API serializer is changing property names or values somewhere

### ⚠️ Network → UI Parser (TelemetryMessageParser)  - **POTENTIAL ISSUE**
**File**: `PitWall.UI/Services/TelemetryMessageParser.cs:14-28`
```csharp
public static TelemetrySampleDto Parse(string json)
{
    Console.WriteLine($"[Parser] Raw JSON: {json.Substring(0, Math.Min(200, json.Length))}");
    
    var dto = JsonSerializer.Deserialize<TelemetrySampleDto>(json, Options);
    
    if (dto == null) {
        return new TelemetrySampleDto();  // ← Returns default (all zero values!)
    }
    
    Console.WriteLine($"[Parser] Deserialized - Throttle: {dto.ThrottlePosition:F3}, Brake: {dto.BrakePosition:F3}, ...");
    
    // NormalizePedal function exists but is NEVER CALLED!
    dto.TyreTempsC ??= Array.Empty<double>();
    return dto;
}

private static double NormalizePedal(double value)
{
    return value > 1.0 ? value / 100.0 : value;  // Converts 0-100 → 0-1 if needed
}
```
**Issues Identified**:
1. **Dead Code**: `NormalizePedal()` and `NormalizeSteering()` are defined but never used
2. **Silent Failure**: If `JsonSerializer.Deserialize()` returns null, returns default DTO with RakePosition = 0.0
3. **No Value Validation**: No checks if brake value is in expected 0-1 range

### ✓ UI Parser → Dashboard/TelemetryAnalysis ✓ VERIFIED CORRECT
**File**: `PitWall.UI/ViewModels/DashboardViewModel.cs:129-140`
```csharp
public void UpdateTelemetry(TelemetrySampleDto telemetry)
{
    var brake = Math.Clamp(telemetry.BrakePosition, 0, 1);
    BrakePercent = brake * 100;  // ✅ Correct: 0-1 → 0-100%
}
```
**Status**: ✅ Correctly calculates display percentage

### ✓ Telemetry Analysis Graph Loading ✓ VERIFIED CORRECT
**File**: `PitWall.UI/ViewModels/TelemetryAnalysisViewModel.cs:277`
```csharp
private void PopulateDataSeries(TelemetrySampleDto[] samples, ...)
{
    foreach (var sample in samples) {
        brakeSeries.Add(new TelemetryDataPoint { 
            Time = time, 
            Value = sample.BrakePosition * 100  // ✅ Correct: 0-1 → 0-100%
        });
        // ...
    }
}
```
**Status**: ✅ Graph data calculation is correct

---

## Root Cause Hypotheses (Ranked by Probability)

### 🔴 HYPOTHESIS A: JSON Deserialization Failure (HIGH PROBABILITY = 70%)
**Trigger**: API JSON structure doesn't match TelemetrySampleDto expectations  
**Mechanism**:
1. API sends JSON with different property names (e.g., `"brakePos"` instead of `"brake"`)
2. OR API sends values in unexpected scale (e.g., still 0-100)
3. JsonSerializer fails silently (doesn't throw, just null)
4. Parser returns default DTO: `BrakePosition = 0.0`
5. Dashboard receives 0, displays 0%
6. Graphs receive 0 values, appear flat

**Evidence**:
- ✅ Console logs in parser show raw JSON (line 19) and deserialized values (line 28)
- ✅ If deserialization fails, default DTO is returned (line 24)
- ✅ Dead code `NormalizePedal()` suggests this might have been a past issue

**Test Method**:
```
Start API, connect UI, check [Parser] debug logs in console
Look for: mismatched property names or unexpected value ranges
```

### 🟡 HYPOTHESIS B: API Serialization Changed the Value Scale (MEDIUM PROBABILITY = 20%)
**Trigger**: API sends brake value still in 0-100 range instead of 0-1  
**Mechanism**:
1. TelemetrySample.Brake is 0.723 (0-1 range)
2. API accidentally sends it as-is in JSON
3. Parser deserializes 0.723 as correct (no NormalizePedal called)
4. Dashboard multiplies by 100: 0.723 * 100 = 72.3% ✓
5. **BUT** if API sends as string or there's double-serialization...

**Evidence**:
- ❌ Code review shows no serialization that doubles the scale
- ✅ Dead `NormalizePedal()` suggests this was considered a risk

### 🟡 HYPOTHESIS C: Lap Data Not Fully Loaded Into Buffer (MEDIUM PROBABILITY = 20%)
**Trigger**: WebSocket stream incomplete or lap boundary issue  
**Mechanism**:
1. Replay streams from startRow to endRow
2. Some rows with high brake values are skipped/missed
3. Buffer contains sparse data
4. Graphs show averaged/smoothed appearance
5. Dashboard occasionally shows correct value, mostly 0

**Evidence**:
- ⚠️ TelemetryBuffer is circular with 10,000 capacity
- ⚠️ Lap data is filtered from buffer: `GetLapData(lapNumber)` filters by lap number
- ⚠️ No timestamp validation in parser

### 🟢 HYPOTHESIS D: Bug in TelemetryBuffer.GetLapData() (LOW PROBABILITY = 10%)
**Trigger**: Lap filtering logic incorrectly excludes brake samples  
**Mechanism**:
1. Samples added to buffer with correct lap numbers
2. GetLapData filter is broken or lap numbers are wrong
3. Graph receives incomplete/filtered dataset
4. Dashboard receives correct data (not filtered) → works fine

**Evidence**:
- ✅ Dashboard works, graphs don't → suggests data is correct but filtering is wrong
- ❌ Code review shows straightforward LINQ filter: `.Where(s => s.LapNumber == lapNumber)`

---

## Recommended Investigation Steps

### Phase 1: Verify Data Through Stack (2 hours)

**Step 1A**: Check Parser Logs
```bash
1. Start API server in background
2. Start UI
3. Begin lap 3 replay
4. Capture console output from both API and UI
5. Look for:
   - [Parser] Raw JSON lines (see actual JSON structure)
   - [Parser] Deserialized lines (see final values)
   - Any exceptions or null returns
6. Save to: DEBUG_PARSER_LOGS.txt
```

**Step 1B**: Database Verification
```sql
-- Run against lmu_telemetry.db
SELECT session_id, COUNT(*) as brake_samples, MIN(value), MAX(value), AVG(value)
FROM "Brake Pos"
WHERE session_id = 276
GROUP BY session_id;

-- Check if lap 3 has brake data
SELECT COUNT(*) FROM "Brake Pos" bp
INNER JOIN "Lap" lap ON bp.session_id = lap.session_id
WHERE bp.session_id = 276 AND lap.value = 3;
```

**Step 1C**: API Direct Test
```powershell
# While API is running, test the raw samples endpoint
$sample = Invoke-RestMethod "http://localhost:5236/api/sessions/276/samples?startRow=5000&endRow=5010"
$sample.samples | Select-Object speedKph, brake, throttle | Format-Table
# Verify brake values are in 0-1 range, not 0 or 0-100
```

### Phase 2: Isolate Component Failure (1 hour)

**Step 2A**: Unit Test Parser
```csharp
// Create test JSON matching API output
var json = "{\"brake\": 0.72, \"throttle\": 0.8, ...}";
var result = TelemetryMessageParser.Parse(json);
Assert.NotEqual(0, result.BrakePosition);  // Should NOT be 0
```

**Step 2B**: Trace Buffer Content
```csharp
// In MainWindowViewModel.ApplyTelemetry, add logging:
Console.WriteLine($"[Buffer] Adding sample. Lap: {telemetry.LapNumber}, Brake: {telemetry.BrakePosition}");
var allLaps = _telemetryBuffer.GetAvailableLaps();
Console.WriteLine($"[Buffer] Available laps after add: {string.Join(", ", allLaps)}");
```

**Step 2C**: Verify Lap Data Filtering
```csharp
// After loading lap data
var lapData = _telemetryBuffer.GetLapData(3);
Console.WriteLine($"[TelemetryAnalysis] Lap 3 loaded: {lapData.Length} samples");
var brakeSummary = lapData.Select(s => s.BrakePosition).Where(b => b > 0);
Console.WriteLine($"[TelemetryAnalysis] Samples with brake > 0: {brakeSummary.Count()}");
```

### Phase 3: Fix & Verify (2 hours)

Based on findings above, apply targeted fixes

---

## Potential Fixes (To Be Applied After Root Cause Confirmed)

### Fix A: Hardened Parser with Validation
```csharp
public static TelemetrySampleDto Parse(string json)
{
    Console.WriteLine($"[Parser] Raw JSON: {json.Substring(0, Math.Min(200, json.Length))}");
    
    var dto = JsonSerializer.Deserialize<TelemetrySampleDto>(json, Options);
    
    if (dto == null) {
        Console.WriteLine("[Parser] ERROR: Deserialization returned null!");
        return new TelemetrySampleDto();
    }
    
    // ✅ NEW: Apply normalization if value is in wrong scale
    if (dto.BrakePosition > 1.0) {
        Console.WriteLine($"[Parser] WARNING: Brake value {dto.BrakePosition} > 1.0, normalizing...");
        dto.BrakePosition = dto.BrakePosition / 100.0;  // Assume 0-100 scale
    }
    
    // ✅ NEW: Clamp values to expected range
    dto.BrakePosition = Math.Clamp(dto.BrakePosition, 0, 1);
    dto.ThrottlePosition = Math.Clamp(dto.ThrottlePosition, 0, 1);
    
    Console.WriteLine($"[Parser] Deserialized - Brake: {dto.BrakePosition:F3}, Throttle: {dto.ThrottlePosition:F3}");
    
    dto.TyreTempsC ??= Array.Empty<double>();
    return dto;
}
```

### Fix B: Buffer Diagnostics
```csharp
public void LoadCurrentLapData(int lapNumber)
{
    if (lapNumber <= 0) return;
    
    CurrentLap = lapNumber;
    var allData = _telemetryBuffer.GetAll();
    var lapData = _telemetryBuffer.GetLapData(lapNumber);
    
    // ✅ NEW: Diagnostic logging
    var brakeCounts = lapData.GroupBy(s => s.BrakePosition > 0).Select(g => $"{(g.Key ? "Non-zero" : "Zero")}: {g.Count()}");
    StatusMessage = $"Loaded {lapData.Length} samples for lap {lapNumber}. Brake: {string.Join(", ", brakeCounts)}";
    
    if (lapData.Length == 0) {
        ClearCurrentLapData();
        return;
    }
    
    PopulateDataSeries(lapData, SpeedData, ThrottleData, BrakeData, SteeringData, TireTempData);
    RaiseDataSeriesUpdated();
}
```

### Fix C: Aligned Scale Across Stack
```csharp
// If API is incorrectly sending 0-100 scale:
// Option 1: Fix API to send 0-1
// Option 2: Cache in parser:
private static class ScaleCache {
    public static (bool IsBad, double Scale) LastSeenBrakeScale = (false, 1.0);
}

// In parser:
if (dto.BrakePosition > 1.0) {
    ScaleCache.LastSeenBrakeScale = (true, 100.0);
    dto.BrakePosition /= 100.0;  // Normalize
}
```

---

## Success Criteria (Post-Fix Verification)

- [ ] Dashboard brake gauge shows 70-73% during lap 3 braking zones
- [ ] Console logs show brake values 0.70-0.73 (0-1 scale) in parser output  
- [ ] Telemetry graph shows sharp peaks (not gentle curves) for brake during braking
- [ ] Reference lap braking shows similar amplitude to current lap
- [ ] All three pedal gauges (throttle, brake, steering) display synchronized,  proportional values
- [ ] No "ERROR" or "WARNING" messages in [Parser] logs
- [ ] Unit tests pass for parser with both 0-1 and 0-100 scale inputs

---

## Files to Examine During Investigation
1. `PitWall.UI/Services/TelemetryMessageParser.cs` - JSON deserialization
2. `PitWall.UI/Services/TelemetryStreamClient.cs` - WebSocket receiving
3. `PitWall.Api/Program.cs` (Line 219) - API serialization
4. `PitWall.Core/Services/LmuTelemetryReader.cs` (Line 173) - Database reading
5. `PitWall.UI/ViewModels/MainWindowViewModel.cs` (Line 311) - Buffer population
6. `PitWall.UI/Services/TelemetryBuffer.cs` - Lap data filtering
7. `PitWall.UI/ViewModels/TelemetryAnalysisViewModel.cs` (Line 277) - Graph data loading

---

## Related Context
- **Previous Fix**: Lap selection persistence (RefreshAvailableLaps issue) ✅ RESOLVED
- **Session**: 276 (lap 3, Silverstone)
- **Data Anomaly**: Database has 70-73% brake values, but UI shows 0%
- **Tools Working**: Throttle & steering gauges (different code path)
- **Tools Broken**: Brake gauge + brake graph (same code path)

