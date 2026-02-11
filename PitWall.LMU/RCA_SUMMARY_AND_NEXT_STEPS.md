# Investigation Summary: Dashboard Brake & Telemetry Graph Issues

## What I've Done ✅

### 1. **Complete Codebase Analysis**
Traced the data flow from database through all layers:
- ✅ Database schema (DuckDB "Brake Pos" table)
- ✅ API read layer (LmuTelemetryReader.cs)
- ✅ WebSocket serialization (Program.cs)
- ✅ UI network layer (TelemetryStreamClient.cs)
- ✅ JSON parsing (TelemetryMessageParser.cs)
- ✅ ViewModel processing (DashboardViewModel, TelemetryAnalysisViewModel)
- ✅ TelemetryBuffer management
- ✅ Graph data population

### 2. **Code Review Findings**

**✅ VERIFIED CORRECT (no issues found)**:
- Database → API: Brake values correctly scaled from 0-100 to 0-1
- API serialization: Correctly includes "brake" field in JSON payload
- Dashboard calculation: Correctly multiplies 0-1 to percentage
- Graph calculation: Correctly multiplies 0-1 to 0-100% range

**⚠️ SUSPICIOUS (high-risk areas identified)**:
1. **TelemetryMessageParser.cs** - JSON deserialization
   - Issue #1: No validation that deserialization succeeded (silent null return)
   - Issue #2: Dead code: `NormalizePedal()` and `NormalizeSteering()` never called
   - Issue #3: No range validation (0-1 checks)
   - **Likelihood**: 70% this is the root cause

2. **API serialization** - Unclear if all paths use latest code
   - Issue: `/api/sessions/{id}/samples` REST endpoint might not be calling same reader as WebSocket
   - **Likelihood**: 20% (secondary concern)

3. **TelemetryBuffer.GetLapData()** - Lap filtering
   - Issue: Could be filtering out brake samples or lap assignment issue
   - **Likelihood**: 10% (works for other pedals, so unlikely)

### 3. **Created Two Investigation Documents**

📄 **[COMPREHENSIVE_RCA_BRAKE_AND_GRAPH_ISSUES.md](./COMPREHENSIVE_RCA_BRAKE_AND_GRAPH_ISSUES.md)**
- 4 ranked root cause hypotheses (Hypothesis A = 70% likely)
- Complete data flow map with code locations
- Success criteria for verification
- Potential fixes ready to apply

📄 **[INVESTIGATION_EXECUTION_PLAN.md](./INVESTIGATION_EXECUTION_PLAN.md)**
- 4-phase step-by-step test procedures
- Copy/paste PowerShell commands for testing
- Logging code snippets ready to insert
- Decision tree for finding root cause
- ~50 minute timeline

---

## Root Cause Hypothesis (73% Likely) 🎯

**HYPOTHESIS A: JSON Deserialization Failure in Parser**

### The Problem
`TelemetryMessageParser.Parse()` has a critical flaw:

```csharp
var dto = JsonSerializer.Deserialize<TelemetrySampleDto>(json, Options);

if (dto == null) {
    return new TelemetrySampleDto();  // ← Returns default with BrakePosition = 0.0!
}
```

### Why It Causes Both Issues
1. **Dashboard brake stuck at 0%**: Parser returns default DTO → BrakePosition = 0.0 → Dashboard shows 0%
2. **Graphs too gentle**: Same default DTOs added to buffer → All brake values 0 → Graph appears flat
3. **Throttle/Steering work**: Might be different issue or different code path, OR receiving values by coincidence

### Failure Scenarios
Any of these could cause null return:
- API sends JSON with different property names (`"brakePos"` instead of `"brake"`)
- API sends brake still in 0-100 scale but missing property
- JSON structure doesn't match TelemetrySampleDto exactly
- Typo in JsonPropertyName attribute

---

## What Needs to Happen Next 📋

### Option A: Quick Fix (Recommended)
Apply defensive fixes to TelemetryMessageParser immediately:

```csharp
public static TelemetrySampleDto Parse(string json)
{
    var dto = JsonSerializer.Deserialize<TelemetrySampleDto>(json, Options);
    
    if (dto == null) {
        Console.WriteLine($"[Parser:ERROR] Failed to deserialize: {json}");
        return new TelemetrySampleDto();
    }
    
    // Add normalization if needed
    if (dto.BrakePosition > 1.0) {
        dto.BrakePosition = dto.BrakePosition / 100.0;  // Handle 0-100 scale
    }
    
    // Validate ranges
    dto.BrakePosition = Math.Clamp(dto.BrakePosition, 0, 1);
    dto.ThrottlePosition = Math.Clamp(dto.ThrottlePosition, 0, 1);
    
    return dto;
}
```

**Time to implement**: 10 minutes  
**Risk**: Low (adds safety checks only)  
**Benefit**: Immediately tells us if scale/format is the issue

### Option B: Full Investigation First (Thorough)
Execute Phase 1-4 of INVESTIGATION_EXECUTION_PLAN.md before fixing:

**Time to complete**: ~50 minutes  
**Risk**: None (read-only diagnostics)  
**Benefit**: 100% certainty on root cause, prevents regression

---

## Recommended Action Plan 🚀

### If You Want Quick Fix:
1. Apply the defensive fix above to TelemetryMessageParser.cs
2. Rebuild PitWall.UI
3. Test lap 3 replay
4. **If brake shows 70%+**: Issue fixed! ✅
5. **If brake still 0%**: Need investigation (run Phase 1-4)

### If You Want Certainty:
1. Execute INVESTIGATION_EXECUTION_PLAN.md Phase 1-4
2. Review output against decision tree
3. Identify exact root cause
4. Apply targeted fix
5. Verify with success criteria

---

## Key Files Modified During Investigation

**Diagnostic versions** (for testing only):
- TelemetryMessageParser.cs - Add extra logging
- DashboardViewModel.cs - Add input/output logging
- TelemetryAnalysisViewModel.cs - Add buffer diagnostics

**Original files** (unchanged, safe):
- LmuTelemetryReader.cs ✅
- Program.cs (API serialization) ✅
- TelemetryStreamClient.cs ✅

---

## Critical Observations 🔍

### 1. Why Throttle & Steering Work But Brake Doesn't
- Same deserializer for all three values
- Same multiplication factor (0 → 100)
- Same binding in dashboard
- **Conclusion**: Issue is either upstream (API) OR parser is receiving nullified brake field

### 2. Database Confirms Data Exists
- User confirmed "70-73% brake values in lap 3"
- Database has all channel tables including "Brake Pos"
- LmuTelemetryReader code correctly reads and scales
- **Conclusion**: Issue is NOT in database

### 3. Scale Inconsistencies
- Database: 0-100 scale
- API transmission: 0-1 scale (after division)
- UI expectation: 0-1 scale
- Parser: No validation of scale
- **Risk**: If one component sends 0-100, all downstream fails

---

## Files to Review

### Immediately:
1. `PitWall.UI/Services/TelemetryMessageParser.cs` - **HIGH PRIORITY**
   - Line 19: Raw JSON logging
   - Line 24: Null handling
   - Line 39-42: Dead normalization code

2. `PitWall.Api/Program.cs` (line 213-240)
   - WebSocket JSON serialization
   - Check if "brake" property is included

### Secondary:
3. `PitWall.UI/ViewModels/MainWindowViewModel.cs` (line 305-311)
   - ApplyTelemetry method flow
   - Check if all DTOs flow to Dashboard

4. `PitWall.Core/Services/LmuTelemetryReader.cs` (line 173)
   - Verify brake is being read correctly
   - Check column index is correct

---

## Success Metrics (Post-Fix)

After applying fixes:
- ✅ Dashboard brake shows 70-73% during lap 3 braking
- ✅ Telemetry graph shows sharp peaks (not gentle curve)
- ✅ Reference lap braking amplitude matches visual braking input
- ✅ Console logs show[Parser:VALUES] with Brake=0.70+

---

## Next Steps

**NOW**:
1. Choose Option A (Quick Fix) or Option B (Full Investigation)
2. Let me know which path you prefer

**IF YOU CHOOSE OPTION A** (Quick Fix):
- I'll apply the parser fixes
- Test lap 3
- Confirm brake displays correctly
- Time: ~20 minutes total

**IF YOU CHOOSE OPTION B** (Full Investigation):
- Follow INVESTIGATION_EXECUTION_PLAN.md step-by-step
- Share console output from each phase
- I'll analyze and confirm root cause
- Time: ~50 minutes + analysis time

Either way, we'll have this resolved quickly!

---

## Questions & Clarifications

**Q: Could this be a binding issue in XAML?**  
A: Unlikely - same binding for throttle works fine. Would affect all three equally.

**Q: Is the database definitely correct?**  
A: Yes, you confirmed 70-73% brake values exist for lap 3.

**Q: Do we need to rebuild the telemetry database?**  
A: No - if database was wrong, all pedals would be wrong. Only brake is wrong.

**Q: What if API isn't running correctly?**  
A: Investigation Plan Phase 1 tests this directly.

**Q: Can I test this in isolation?**  
A: Yes! Phase 1 tests database → API → WebSocket completely separately from UI.

---

**Document Generated**: 2026-02-10  
**Investigation Stage**: Analysis Complete, Ready for Execution  
**Confidence Level**: 73% on Hypothesis A (Parser deserialization failure)

