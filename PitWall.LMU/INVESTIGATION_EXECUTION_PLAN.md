# Investigation Execution Plan & Test Commands

**Status**: Ready for Manual Execution  
**Created**: 2026-02-10  
**Objective**: Execute RCA and isolate root cause of brake data issues

---

## PHASE 1: DATABASE & API VERIFICATION

### Test 1.1: Verify Brake Data in Database

```powershell
# PowerShell: Query brake position values for session 276
$db = "C:\Users\ohzee\.claude-worktrees\PitWall\vibrant-meninsky\PitWall.LMU\lmu_telemetry.db"

# Count total brake samples
duckdb "$db" 'SELECT COUNT(*) as total_samples FROM "Brake Pos" WHERE session_id = 276;'

# Get brake statistics
duckdb "$db" @"
SELECT 
    MIN(value) as min_brake,
    MAX(value) as max_brake,
    ROUND(AVG(value), 2) as avg_brake,
    ROUND(STDDEV(value), 2) as stddev_brake
FROM "Brake Pos"
WHERE session_id = 276;
"@

# Sample specific brake values (should be 0-100 in raw DB)
duckdb "$db" 'SELECT value FROM "Brake Pos" WHERE session_id = 276 LIMIT 20;' | head -20
```

**Expected Output**:
- Total samples: > 10,000
- Min brake: 0-20 (idle/coasting)
- Max brake: 70-100 (hard braking)
- Avg brake: 20-40 (typical lap mix)

---

### Test 1.2: Start API and Test WebSocket Stream

```powershell
# Terminal 1: Start API Server
Set-Location "C:\Users\ohzee\.claude-worktrees\PitWall\vibrant-meninsky\PitWall.LMU"
$env:LMU_TELEMETRY_DB = "C:\Users\ohzee\.claude-worktrees\PitWall\vibrant-meninsky\PitWall.LMU\lmu_telemetry.db"
$env:ASPNETCORE_URLS = "http://localhost:5236"
dotnet run --project PitWall.Api\PitWall.Api.csproj --no-build

# You should see:
# INFO: "Now listening on: http://localhost:5236"
# Keep this terminal open
```

```powershell
# Terminal 2: Test REST Endpoint for Samples
Start-Sleep -Seconds 3
$apiBase = "http://localhost:5236"

# Get sample with specific row range
$samples = Invoke-RestMethod "$apiBase/api/sessions/276/samples?startRow=5000&endRow=5010"
Write-Host "Sample count: $($samples.samples.Count)"
Write-Host "Brake values:"
$samples.samples | ForEach-Object {
    "  Speed: $($_.speedKph | [math]::Round($_, 1)) kph | Brake: $($_.brake | [math]::Round($_, 3)) | Throttle: $($_.throttle | [math]::Round($_, 3))"
}
```

**Expected Output**:
- Sample count: 10
- Brake values: 0.000 to 0.730 (0-1 scale)
- Speed, throttle correlate with brake (speed drops → brake up)

---

### Test 1.3: Send WebSocket Test Message (Power Shell)

```powershell
# This will subscribe to the WebSocket stream and capture raw messages
# Install WebSocket if needed: Install-Module -Name PowerShellWebSocket

$sessionId = 276
$startRow = 5000
$endRow = 5020
$intervalMs = 50
$uri = "ws://localhost:5236/ws/state?sessionId=$sessionId&startRow=$startRow&endRow=$endRow&intervalMs=$intervalMs"

# Connect and receive 3 messages
$ws = New-WebSocketConnection -Uri $uri
for ($i = 0; $i -lt 3; $i++) {
    $msg = Receive-WebSocketMessage -WebSocket $ws
    "Message $($i+1):"
    $msg | ConvertFrom-Json | Select-Object speedKph, brake, throttle, steering | Format-List
}
$ws.Close()
```

**Expected Output**:
- 3 JSON messages received
- Each with `"brake": 0.XX` (0-1 scale)
- brake values around 0.70-0.73 for high braking scenario

---

## PHASE 2: UI PARSER VERIFICATION

### Test 2.1: Add Detailed Logging to Parser

**File**: `PitWall.UI/Services/TelemetryMessageParser.cs`

```csharp
// REPLACE the existing Parse() method with this version:

public static TelemetrySampleDto Parse(string json)
{
    // ========== NEW: Detailed Logging ==========
    Console.WriteLine($"[Parser:RAW_JSON] {json}");  // Full JSON
    
    var dto = JsonSerializer.Deserialize<TelemetrySampleDto>(json, Options);
    
    if (dto == null)
    {
        Console.WriteLine($"[Parser:ERROR] Deserialization returned NULL!\n[Parser:ERROR] Failed to parse: {json.Substring(0, Math.Min(100, json.Length))}");
        return new TelemetrySampleDto();
    }
    
    // ========== NEW: Range Validation ==========
    Console.WriteLine($"[Parser:VALUES] Speed={dto.SpeedKph:F1} Throttle={dto.ThrottlePosition:F3} Brake={dto.BrakePosition:F3} Steering={dto.SteeringAngle:F3}");
    
    if (dto.BrakePosition < 0 || dto.BrakePosition > 1.0)
    {
        Console.WriteLine($"[Parser:WARNING] Brake out of range: {dto.BrakePosition} (expected 0-1)");
    }
    
    if (dto.ThrottlePosition < 0 || dto.ThrottlePosition > 1.0)
    {
        Console.WriteLine($"[Parser:WARNING] Throttle out of range: {dto.ThrottlePosition} (expected 0-1)");
    }
    
    dto.TyreTempsC ??= Array.Empty<double>();
    return dto;
}
```

### Test 2.2: Rebuild and Run UI with Logging

```powershell
# Terminal 3: Build UI
Set-Location "C:\Users\ohzee\.claude-worktrees\PitWall\vibrant-meninsky\PitWall.LMU"
dotnet build PitWall.UI\PitWall.UI.csproj

# Terminal 4: Run UI
$env:PITWALL_API_BASE = "http://localhost:5236"
$env:PITWALL_AGENT_BASE = "http://localhost:5139"  # Won't be used
Set-Location "C:\Users\ohzee\.claude-worktrees\PitWall\vibrant-meninsky\PitWall.LMU"
dotnet run --project PitWall.UI\PitWall.UI.csproj --no-build 2>&1 | Tee-Object -FilePath "UI_DEBUG.log"
```

### Test 2.3: Trigger Replay and Capture Logs

**In UI (once running)**:
1. Select Session 276
2. Set "Start Row: 5000"
3. Set "End Row: 5020"
4. Click "Start Replay"
5. Watch console for `[Parser:*]` messages

**Expected Console Output**:
```
[Parser:RAW_JSON] {"timestamp":"2026-02-10T12:30:15.123Z","speedKph":241.5,"throttle":0.8,"brake":0.72,"steering":-0.05,...}
[Parser:VALUES] Speed=241.5 Throttle=0.800 Brake=0.720 Steering=-0.050
[Parser:RAW_JSON] {"timestamp":"2026-02-10T12:30:15.133Z","speedKph":245.2,"throttle":0.85,"brake":0.73,"steering":-0.15,...}
[Parser:VALUES] Speed=245.2 Throttle=0.850 Brake=0.730 Steering=-0.150
...
```

**IF YOU SEE**:
- `[Parser:ERROR] Deserialization returned NULL!` → Deserialization failure (likely JSON format issue)
- `[Parser:WARNING] Brake out of range: 72.3` → API sending 0-100 scale instead of 0-1
- `[Parser:VALUES] Brake=0.000` for all messages → API is sending 0, not dashboard issue
- `[Parser:VALUES]` missing from console → Parser code path not being called

---

## PHASE 3: DASHBOARD VERIFICATION

### Test 3.1: Add Diagnostics to Dashboard

**File**: `PitWall.UI/ViewModels/DashboardViewModel.cs`

**AFTER line 140** (in UpdateTelemetry method), add:

```csharp
        BrakePercent = brake * 100;
        SteeringPercent = (steering + 1) * 50;

        // ========== NEW: Detailed Dashboard Logging ==========
        Console.WriteLine($"[Dashboard:INPUT] Raw={telemetry.BrakePosition:F3} Throttle={telemetry.ThrottlePosition:F3} Steering={telemetry.SteeringAngle:F3}");
        Console.WriteLine($"[Dashboard:CLAMPED] Brake={brake:F3} ({brake*100:F1}%) Throttle={throttle:F3} Steering={steering:F3}");
        Console.WriteLine($"[Dashboard:DISPLAY] BrakePercent={BrakePercent:F1}% BrakeDisplay={BrakeDisplay}");
```

### Test 3.2: Run UI Again and Watch Logs

```
[Dashboard:INPUT] Raw=0.720 Throttle=0.800 Steering=-0.050
[Dashboard:CLAMPED] Brake=0.720 (72.0%) Throttle=0.800 Steering=-0.050
[Dashboard:DISPLAY] BrakePercent=72.0% BrakeDisplay=72%

[Dashboard:INPUT] Raw=0.730 Throttle=0.850 Steering=-0.150
[Dashboard:CLAMPED] Brake=0.730 (73.0%) Throttle=0.850 Steering=-0.150
[Dashboard:DISPLAY] BrakePercent=73.0% BrakeDisplay=73%
```

**IF YOU SEE**:
- `Raw=0.000` in Dashboard → Issue is in parser (test 2.1-2.3)
- `Raw=0.720` but `BrakeDisplay=0%` → Binding issue (unlikely, code looks correct)
- `Raw=72.0` → Parser is receiving 0-100 scale (API issue)

---

## PHASE 4: TELEMETRY BUFFER VERIFICATION

### Test 4.1: Add Buffer Diagnostics

**File**: `PitWall.UI/ViewModels/TelemetryAnalysisViewModel.cs`

**In LoadCurrentLapData() method**, replace entire method with:

```csharp
    public void LoadCurrentLapData(int lapNumber)
    {
        if (lapNumber <= 0) return;

        CurrentLap = lapNumber;
        var allData = _telemetryBuffer.GetAll();
        var lapData = _telemetryBuffer.GetLapData(lapNumber);

        // ========== NEW: Buffer Diagnostics ==========
        var brakeSamples = lapData.Count(s => s.BrakePosition > 0);
        var brakeStats = lapData.Where(s => s.BrakePosition > 0).Select(s => s.BrakePosition).ToList();
        
        if (brakeStats.Count > 0)
        {
            var minBrake = brakeStats.Min();
            var maxBrake = brakeStats.Max();
            var avgBrake = brakeStats.Average();
            Console.WriteLine($"[TelemetryAnalysis:BUFFER] Lap {lapNumber}: {lapData.Length} total samples, {brakeSamples} with brake > 0");
            Console.WriteLine($"[TelemetryAnalysis:BRAKE_RANGE] Min={minBrake:F3} Max={maxBrake:F3} Avg={avgBrake:F3}");
        }
        else
        {
            Console.WriteLine($"[TelemetryAnalysis:WARNING] Lap {lapNumber}: ALL {lapData.Length} samples have brake = 0!");
        }

        if (lapData.Length == 0)
        {
            StatusMessage = $"No data found for lap {lapNumber}";
            ClearCurrentLapData();
            return;
        }

        PopulateDataSeries(lapData, SpeedData, ThrottleData, BrakeData, SteeringData, TireTempData);
        StatusMessage = $"Loaded {lapData.Length} samples for lap {lapNumber}";
        RaiseDataSeriesUpdated();
    }
```

### Test 4.2: Load Lap Data and Check Output

```
Navigate to Telemetry Analysis tab
Select Lap 3 from dropdown
Check console output:

[TelemetryAnalysis:BUFFER] Lap 3: 8000 total samples, 2400 with brake > 0
[TelemetryAnalysis:BRAKE_RANGE] Min=0.050 Max=0.735 Avg=0.280
```

**IF YOU SEE**:
- `0 with brake > 0` → Brake values not in buffer (upstream issue)
- Expected stats → Buffer is fine, issue is dashboard/UI binding

---

## SUMMARY: Root Cause Detection Decision Tree

### Run all 4 phases and analyze output:

```
Question 1: Do database brake values exist?
├─ YES → Continue to Q2
└─ NO → Database rebuild needed (out of scope)

Question 2: Does API return non-zero brake values?
├─ YES (0.00-1.00 scale) → Continue to Q3
├─ YES (but 0.00-100.0 scale) → ROOT CAUSE: API sending wrong scale (Fix A)
└─ NO (all 0.0) → ROOT CAUSE: API read failure (investigate LmuTelemetryReader)

Question 3: Does parser extract correct values?
├─ YES [Parser:VALUES] shows 0.70+ → Continue to Q4
├─ NO NULL deserialization error → ROOT CAUSE: JSON format mismatch (Fix C)
└─ NO out-of-range warning → ROOT CAUSE: Parser validation (Fix A)

Question 4: Does dashboard receive correct values?
├─ YES [Dashboard:INPUT] shows 0.70+ → ROOT CAUSE: UI binding/display (unlikely)
└─ NO [Dashboard:INPUT] shows 0.0 → ROOT CAUSE: Buffer or Main ViewModel issue (Fix D)

Question 5: Does buffer contain correct values?
├─ YES [TelemetryAnalysis:BRAKE_RANGE] shows 0.05-0.73 → ROOT CAUSE: Graph rendering (unlikely)
└─ NO all brake = 0 → ROOT CAUSE: Buffer filtering or TelemetryBuffer (Fix E)
```

---

## Additional Tools

### If WebSocket Testing Needed:
```powershell
# Install dependency if needed
# Install-PackageProvider -Name NuGet -Force
# Install-Module -Name WebSocketSharp -Force

# Or use wscat (Node.js based):
npm install -g wscat
wscat -c "ws://localhost:5236/ws/state?sessionId=276&startRow=5000&endRow=5020"
# Then inspect messages manually
```

### Database Deep Dive:
```sql
-- Check if Brake Pos table exists and has session 276
duckdb "C:\Users\ohzee\.claude-worktrees\PitWall\vibrant-meninsky\PitWall.LMU\lmu_telemetry.db" \
  'SELECT * FROM "Brake Pos" WHERE session_id = 276 LIMIT 1;'

-- Verify lap table structure
duckdb "lmu_telemetry.db" \
  'SELECT session_id, DISTINCT value as lap FROM "Lap" WHERE session_id = 276 ORDER BY value;'

-- Cross-join to find lap 3 brake samples
duckdb "lmu_telemetry.db" @"
SELECT COUNT(*) as lap3_brake_samples
FROM "Brake Pos" bp
WHERE bp.session_id = 276
  AND EXISTS (
    SELECT 1 FROM "Lap" lap 
    WHERE lap.session_id = 276 AND lap.value = 3
  )
"@
```

---

## Files to Back Up Before Testing
- `PitWall.UI/Services/TelemetryMessageParser.cs`
- `PitWall.UI/ViewModels/DashboardViewModel.cs`
- `PitWall.UI/ViewModels/TelemetryAnalysisViewModel.cs`

These can be reverted after testing if fixes aren't needed.

---

## Expected Timeline
- Phase 1: 10 minutes (database + API tests)
- Phase 2: 20 minutes (parser logging + UI run)
- Phase 3: 10 minutes (dashboard diagnostics)
- Phase 4: 10 minutes (buffer verification)
- **Total: ~50 minutes** to complete RCA

