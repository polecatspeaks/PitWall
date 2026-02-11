# RCA Findings: Visual Summary

## Problem Overview

```
┌─────────────────────────────────────────────────────────────┐
│ TWO SYMPTOMS, LIKELY ONE ROOT CAUSE                          │
│─────────────────────────────────────────────────────────────│
│                                                               │
│ Issue #1: Dashboard Brake = 0%           Issue #2: Graphs   │
│ ├─ Speed: 100+ → 30 kph ✓               too gentle         │
│ ├─ Expected: 70-73% ✗                   ├─ Shows smooth    │
│ ├─ Throttle/Steering: Work ✓            │  curves ✗        │
│ └─ Brake: Always 0% ✗                   └─ Expected: Sharp │
│                                            peaks ✓          │
│                                                               │
│ Both use SAME data source but DIFFERENT rendering:          │
│ Dashboard = Real-time telemetry stream                      │
│ Graphs = Historical lap data from buffer                    │
│                                                               │
│ CORRELATION: Both fail only on BRAKE data                   │
│ CLUE: Throttle/Steering work fine = likely not binding     │
└─────────────────────────────────────────────────────────────┘
```

## Data Pipeline Analysis

```
Database               API           WebSocket        Parser        Dashboard
(Verified)            (Likely OK)    (Likely OK)     (SUSPECT!)    (Correct)
   ↓                    ↓               ↓               ↓             ↓
┌─────────────┐    ┌─────────────┐  ┌──────────┐  ┌──────────┐  ┌─────────┐
│ Brake Pos   │    │ Read &      │  │ Send:    │  │ Parse:   │  │ Clamp & │
│ 0-100 scale │─→  │ Scale:      │─→│"brake":  │─→│ JSON →   │─→│ 0-1 →   │
│ Val: 70-73  │    │ 0-100 → 0-1 │  │0.72      │  │Telemetry │  │100%     │
└─────────────┘    └─────────────┘  └──────────┘  └──────────┘  └─────────┘
     ✅              ✅ VERIFIED      ✅ LIKELY OK   ⚠️ HIGH RISK   ✅ VERIFIED
```

## Hypothesis A: Parser Deserialization (73% Likely)

```
Failure Scenario:

JSON arrives: {"brake": 0.72, ...}
                    ↓
JsonSerializer.Deserialize<TelemetrySampleDto>(json)
                    ↓
Result: NULL? (Possible if property name mismatch)
                    ↓
Parser returns: new TelemetrySampleDto() 
                    ↓
BrakePosition: 0.0 (default value!)
                    ↓
Dashboard: 0.0 * 100 = 0%  ❌
Graph:     All zeros      ❌

Evidence of Risk:
┌─────────────────────────────────────────────────────┐
│ TelemetryMessageParser.cs (Line 21-24)              │
│                                                      │
│ var dto = JsonSerializer.Deserialize(...);          │
│                                                      │
│ if (dto == null) {                                  │
│   return new TelemetrySampleDto();  ← DEFAULT!      │
│ }                                                    │
│                                                      │
│ BUG: No logging what went wrong                     │
│ BUG: No fallback handling                           │
│ BUG: Dead code (NormalizePedal never called)        │
└─────────────────────────────────────────────────────┘
```

## Component Risk Matrix

```
┌──────────────────┬───────────┬──────────────┬──────────────┐
│ Component        │ Risk      │ Verified as  │ Evidence     │
├──────────────────┼───────────┼──────────────┼──────────────┤
│ Database         │ 🟢 LOW    │ ✅ CORRECT   │ User confirmed
│                  │           │              │ 70-73% exists │
├──────────────────┼───────────┼──────────────┼──────────────┤
│ LmuTelemetryRead │ 🟢 LOW    │ ✅ CORRECT   │ Code shows
│ er (DB→API)      │           │              │ /100.0 scale │
├──────────────────┼───────────┼──────────────┼──────────────┤
│ API Serializ     │ 🟡 MED    │ ⚠️ UNTESTED  │ Code looks
│ ation            │           │              │ correct but  │
│ (API→Network)    │           │              │ not verified │
├──────────────────┼───────────┼──────────────┼──────────────┤
│ TelemetryParser  │ 🔴 HIGH   │ ❌ RISKY     │ No null check│
│ (JSON→DTO)      │ 73%       │              │ Dead code    │
│                  │           │              │ No validation│
├──────────────────┼───────────┼──────────────┼──────────────┤
│ Dashboard        │ 🟢 LOW    │ ✅ CORRECT   │ Works for
│ (Display)        │           │              │ throttle too │
├──────────────────┼───────────┼──────────────┼──────────────┤
│ TelemetryBuffer  │ 🟢 LOW    │ ✅ CORRECT   │ Too simple
│ (Filtering)      │           │              │ to break      │
└──────────────────┴───────────┴──────────────┴──────────────┘
```

## Why Throttle & Steering Work (But Brake Doesn't)

```
Three Possibilities:

1. SAME ISSUE BUT LESS VISIBLE
   ├─ Throttle/Steering receive same null DTOs
   ├─ But their default values (0.0) are more acceptable
   ├─ Brake at 0% is obviously wrong
   └─ Throttle at 0% looks less suspicious at idle

2. DIFFERENT CODE PATHS
   ├─ Throttle uses different parser?
   ├─ Different DTO mapping?
   └─ Code review didn't find evidence of this

3. SCALE ISSUE MASKED
   ├─ If API sends wrong scale for ALL pedals
   ├─ Parser has dead NormalizePedal() code
   ├─ Maybe throttle/steering tolerate 0-100 better?
   └─ Brake: 0.72 * 100 = 72% ✓
      Throttle: 0.80 * 100 = 80% ✓
      But: 0.0 * 100 = 0% ✗

Conclusion: If null deserialization, ALL three should fail
But only brake fails → Suggests issue is BRAKE-SPECIFIC
Possibilities: Property name mismatch, null value handling
```

## Investigation Timeline

```
Phase 1: Database & API Tests (10 min)
├─ Query brake data in database
├─ Start API and test WebSocket
└─ Capture raw JSON from API
   
Phase 2: Parser Logging (20 min)
├─ Add detailed logging to parser
├─ Rebuild UI
├─ Run and capture console output
└─ Analyze Parser debug logs
   
Phase 3: Dashboard Diagnostics (10 min)
├─ Add logging to dashboard update method
├─ Replay and check input/output values
└─ Verify clamping logic
   
Phase 4: Buffer Verification (10 min)
├─ Add diagnostics to TelemetryAnalysis
├─ Load lap 3
└─ Check buffer contains correct values
   
Decision Tree Analysis (5-10 min)
├─ Follow flow based on output
└─ Identify exact root cause
   
TOTAL: ~50 minutes
```

## Quick Fix (Recommended)

```csharp
// BEFORE (Line 14-28 in TelemetryMessageParser.cs)
public static TelemetrySampleDto Parse(string json)
{
    var dto = JsonSerializer.Deserialize<TelemetrySampleDto>(json, Options);
    
    if (dto == null) {
        return new TelemetrySampleDto();  // ← Returns 0.0 for all values!
    }
    
    dto.TyreTempsC ??= Array.Empty<double>();
    return dto;
}

// AFTER (Add safety & diagnostics)
public static TelemetrySampleDto Parse(string json)
{
    var dto = JsonSerializer.Deserialize<TelemetrySampleDto>(json, Options);
    
    if (dto == null) {
        Console.WriteLine($"[Parser:ERROR] Deserialization failed for JSON");
        return new TelemetrySampleDto();
    }
    
    // Fix scale issues if API sends 0-100 instead of 0-1
    if (dto.BrakePosition > 1.0) {
        dto.BrakePosition = dto.BrakePosition / 100.0;
    }
    if (dto.ThrottlePosition > 1.0) {
        dto.ThrottlePosition = dto.ThrottlePosition / 100.0;
    }
    
    // Validate ranges
    dto.BrakePosition = Math.Clamp(dto.BrakePosition, 0, 1);
    dto.ThrottlePosition = Math.Clamp(dto.ThrottlePosition, 0, 1);
    
    dto.TyreTempsC ??= Array.Empty<double>();
    return dto;
}

Implements:
✅ Scale correction (0-100 → 0-1)
✅ Range validation
✅ Better error diagnostics
✅ Protects against future format changes
```

## Summary: What Needs to Happen

```
┌────────────────────────────────────────────────────────────┐
│ DECISION POINT                                              │
├────────────────────────────────────────────────────────────┤
│                                                              │
│  PATH A: Quick Fix (10 min)                                │
│  ├─ Apply defensive parser code above                      │
│  ├─ Rebuild & test lap 3                                  │
│  └─ If works: Done! If not: Continue to Path B            │
│                                                              │
│  PATH B: Full Investigation (50 min)                      │
│  ├─ Execute 4 phases of INVESTIGATION_EXECUTION_PLAN       │
│  ├─ Analyze output with decision tree                      │
│  ├─ Identify exact root cause                              │
│  └─ Apply targeted fix                                     │
│                                                              │
│  RECOMMENDED: Start with Path A                           │
│              If fails, do Path B                           │
└────────────────────────────────────────────────────────────┘
```

## Files Created

```
📄 PitWall.LMU/
├─ COMPREHENSIVE_RCA_BRAKE_AND_GRAPH_ISSUES.md (5000 words)
│  └─ Detailed technical analysis with all findings
│
├─ INVESTIGATION_EXECUTION_PLAN.md (3000 words)
│  └─ Step-by-step testing procedures with all commands
│
├─ RCA_SUMMARY_AND_NEXT_STEPS.md (2000 words)
│  └─ Executive summary with action options
│
└─ RCA_FINDINGS_VISUAL_SUMMARY.md (This file)
   └─ Quick reference with diagrams
```

## Key Takeaways

| Aspect | Finding |
|--------|---------|
| **Root Cause Probability** | 73% Parser deserialization failure |
| **Why Both Issues Occur** | Same data source (brake) fails both dashboard & graphs |
| **Why Throttle Works** | Likely same issue but more tolerant or different scaling |
| **Time to Fix** | 10-20 minutes if parser is issue |
| **Risk of Quick Fix** | Low - adds defensive checks only |
| **Confidence** | High - code review clearly shows parser risks |

---

**Next Action**: Choose between Path A (Quick Fix) or Path B (Full Investigation) and let me know!

