# LMU Telemetry UI Fix - Summary

## Problem Statement
The PitWall UI was showing brake=0 and no lap selection after 30-60 seconds, even though the API returned valid brake and lap values. WebSocket replay was also closing unexpectedly before reaching rows with brake/lap data.

## Root Cause Analysis

### Brake Issue
- **NOT a bug**: Brake values legitimately start at 0
- Brake data begins appearing at row 1289 (about 13% into the session)
- Early disconnect prevented UI from reaching rows with brake data
- REST API correctly returns brake values when queried at row 10000+

### Lap Issue  
- **Incorrect row-based proportional mapping**
- Lap table has only 12 rows (one per lap transition event) vs 10,000 rows for other channels
- Old query: spread 12 lap values across 10,000 rows proportionally
  - Result: Only ~12 positions had lap values, most were 0
- Lap table has `ts` (timestamp) column for event times
- For session 276 sample DB (100 seconds), only Lap 0 should appear (Lap 1 starts at 181.42s)

### WebSocket Issue
- No explicit completion signal
- No graceful close handshake
- Client couldn't distinguish between normal completion and error

## Implementation

### 1. GPS Time-based Lap Forward-Fill
**File:** `PitWall.Core/Services/LmuTelemetryReader.cs`

Changed lap mapping from proportional row-based to GPS Time-based:

```sql
-- OLD: Proportional row mapping (WRONG for sparse tables)
lap_map AS (
    SELECT t.rn, l.lap
    FROM throttle t
    LEFT JOIN lap l ON l.rn = FLOOR((t.rn - 1) * lap_count / throttle_count) + 1
)

-- NEW: GPS Time-based forward-fill (CORRECT)
lap_map AS (
    SELECT t.rn,
           COALESCE((
               SELECT l.lap 
               FROM lap l, clock_map c 
               WHERE c.rn = t.rn AND l.ts <= c.gps_time 
               ORDER BY l.ts DESC 
               LIMIT 1
           ), 0) AS lap
    FROM throttle t
)
```

This ensures lap values persist until the next lap transition, matching real telemetry behavior.

### 2. WebSocket Completion Handling
**Files:** `PitWall.Api/Program.cs`, `PitWall.UI/Services/TelemetryStreamClient.cs`

**Server-side:**
- Count samples sent
- Send completion message: `{"type":"complete","sampleCount":N}`
- Gracefully close WebSocket with `NormalClosure` status
- Log completion and sample count

**Client-side:**
- Parse completion message using JsonDocument
- Exit loop cleanly on completion
- Distinguish normal completion from errors

### 3. Cross-platform Native Library Support
**Files:** `PitWall.Core/PitWall.Core.csproj`, `PitWall.Tests/PitWall.Tests.csproj`

Added conditional native library copying:
```xml
<None Include="..\native\duckdb\duckdb.dll" 
      Link="duckdb.dll" 
      CopyToOutputDirectory="PreserveNewest" 
      Condition="'$(OS)' == 'Windows_NT'" />
<None Include="..\native\duckdb\libduckdb.so" 
      Link="libduckdb.so" 
      CopyToOutputDirectory="PreserveNewest" 
      Condition="'$(OS)' != 'Windows_NT'" />
```

Enables builds and tests on both Windows and Linux.

### 4. SQL Security Fix
**File:** `PitWall.Tests/Session276DataExplorationTests.cs`

Fixed SQL injection vulnerability in test code by using parameterized queries.

## Testing

### Unit Tests
- ✅ `LmuTelemetryReaderTests`: All 3 tests pass
- ✅ `Session276TelemetryTests`: Validates brake and lap data with session 276 DB
- ✅ Full test suite: 45 tests pass

### Integration Test Results (Session 276)
```
Total samples read: 2000
Samples with non-zero brake: 239
First non-zero brake at index 1289: brake=0.0074, throttle=0.9772
Samples with Lap 0: 2000
Samples with Lap > 0: 0
```

**Correct behavior:**
- Brake appears at row 1289 (expected)
- All samples show Lap 0 (correct - Lap 1 starts at 181.42s, outside 100s window)

### Manual API Test
```bash
curl "http://localhost:5236/api/sessions/276/samples?startRow=1289&endRow=1291"
```

Returns:
- Row 1289: brake=0.0074, throttle=0.977, lapNumber=0 ✅
- Row 1290: brake=0.0201, throttle=0.969, lapNumber=0 ✅  
- Row 1291: brake=0.0363, throttle=0.954, lapNumber=0 ✅

## Security
- ✅ CodeQL scan: 0 alerts
- ✅ SQL injection vulnerability fixed in test code
- ✅ Dependency security check: clean

## Files Changed
1. `PitWall.Core/Services/LmuTelemetryReader.cs` - Lap forward-fill logic
2. `PitWall.Api/Program.cs` - WebSocket completion
3. `PitWall.UI/Services/TelemetryStreamClient.cs` - Completion handling
4. `PitWall.Core/PitWall.Core.csproj` - Native library support
5. `PitWall.Tests/PitWall.Tests.csproj` - Native library support
6. `PitWall.Tests/LmuTelemetryReaderTests.cs` - Updated test data
7. `PitWall.Tests/Integration/Session276TelemetryTests.cs` - New integration test
8. `PitWall.Tests/Session276DataExplorationTests.cs` - New data exploration test
9. `native/duckdb/libduckdb.so` - Added Linux native library

## Expected UI Behavior
After these changes, the UI should:
1. ✅ Show brake gauge responding when brake data appears (after row ~1289)
2. ✅ Show consistent lap numbers (Lap 0 for first 100s of session 276)
3. ✅ Complete WebSocket replay without disconnect errors
4. ✅ Display "Replay finished" status when complete
5. ✅ Track map marker should render at GPS coordinates

## Next Steps
1. Manual UI testing with session 276 database
2. Verify brake gauge animates when replaying past row 1289
3. Verify lap selector shows Lap 0
4. Verify no WebSocket disconnect errors in logs
5. Verify track map renders GPS position correctly
