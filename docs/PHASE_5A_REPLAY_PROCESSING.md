# Phase 5.A: Replay Library Processing & Database Seeding

## Context
PitWall is a race engineer plugin for SimHub that provides strategic recommendations. Phase 5.A adds the ability to process historical iRacing replay files to seed the profile database with the user's actual driving patterns, enabling personalized predictions from day 1.

This feature is OPTIONAL - users can choose to seed from replays or build profiles organically through live racing.

## Requirements

### Core Functionality
1. **Replay Scanner**: Scan user's iRacing replay folder and extract metadata
2. **Chronological Processor**: Process replays in date order (oldest to newest)
3. **Profile Generator**: Calculate fuel usage, tyre degradation, and driving patterns per track/car
4. **Recency Weighting**: Weight recent sessions higher than old sessions (90-day half-life)
5. **Confidence Scoring**: Calculate confidence based on recency, sample size, and consistency
6. **Time-Series Storage**: Store session data with timestamps, not flat averages

### User Experience
- Command-line tool: `PitWall.Tools.exe process-replays --input "path/to/replays"`
- Progress indicator: Shows "Processing [N/Total]: filename (date)"
- Summary report: Shows profiles created, confidence scores, and stale data warnings
- Totally optional: Works without replay processing, builds profiles from live sessions

### Technical Constraints
- Must handle two replay filename patterns:
  - Date-stamped: `YYYY_MM_DD_HH_MM_SS.rpy` (parse from filename)
  - Subsession: `subses{ID}.rpy` (parse from YAML header)
- Must process chronologically to maintain temporal patterns
- Must apply exponential decay weighting (90-day half-life)
- Must detect and flag stale data (>180 days old)
- Performance target: <1 minute per replay on average

## Implementation Tasks

### 1. Create Project Structure
```
PitWall.Tools/
├── PitWall.Tools.csproj
├── Program.cs (CLI entry point)
├── Commands/
│   └── ProcessReplaysCommand.cs
├── Replay/
│   ├── ReplayScanner.cs
│   ├── ReplayMetadataParser.cs
│   ├── ReplayTelemetryExtractor.cs
│   └── ReplayFileInfo.cs
├── Profile/
│   ├── ProfileAnalyzer.cs
│   ├── RecencyWeightCalculator.cs
│   ├── ConfidenceCalculator.cs
│   └── ProfileStatistics.cs
└── Database/
    └── ProfileDatabaseWriter.cs
```

### 2. Replay Metadata Parser
```csharp
public class ReplayMetadataParser
{
    // Parse session date from filename
    public DateTime ExtractSessionDate(string filepath)
    {
        var filename = Path.GetFileNameWithoutExtension(filepath);
        
        // Pattern 1: 2025_11_08_09_58_17.rpy
        if (Regex.IsMatch(filename, @"^\d{4}_\d{2}_\d{2}_\d{2}_\d{2}_\d{2}$"))
        {
            var parts = filename.Split('_');
            return new DateTime(
                int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]),
                int.Parse(parts[3]), int.Parse(parts[4]), int.Parse(parts[5])
            );
        }
        
        // Pattern 2: subses80974445.rpy - parse from YAML header
        if (filename.StartsWith("subses"))
        {
            return ParseSessionTimeFromYaml(filepath);
        }
        
        throw new Exception($"Unknown replay filename format: {filename}");
    }
    
    // Parse track, car, session type from iRacing YAML header
    public ReplayMetadata ParseMetadata(string filepath)
    {
        // Read YAML header from .rpy file
        // Extract: TrackName, CarName, SessionType, SessionLength
        // Return metadata object
    }
}
```

### 3. Chronological Processing Pipeline
```csharp
public class ReplayProcessor
{
    public async Task ProcessReplayLibrary(string replayFolder, string outputDbPath)
    {
        Console.WriteLine("Scanning replay library...");
        
        // 1. Find all .rpy files
        var files = Directory.GetFiles(replayFolder, "*.rpy", SearchOption.TopDirectoryOnly);
        Console.WriteLine($"Found {files.Length} replay files");
        
        // 2. Extract dates and sort chronologically
        var replays = files
            .Select(f => new ReplayFileInfo
            {
                FilePath = f,
                SessionDate = _metadataParser.ExtractSessionDate(f),
                FileSize = new FileInfo(f).Length
            })
            .OrderBy(r => r.SessionDate) // CRITICAL: Process oldest first
            .ToList();
        
        if (replays.Count == 0)
        {
            Console.WriteLine("No replays found.");
            return;
        }
        
        Console.WriteLine($"Date range: {replays.First().SessionDate:yyyy-MM-dd} to {replays.Last().SessionDate:yyyy-MM-dd}");
        Console.WriteLine($"Processing {replays.Count} replays chronologically...\n");
        
        // 3. Process each replay in order
        var processed = 0;
        foreach (var replay in replays)
        {
            processed++;
            Console.Write($"[{processed}/{replays.Count}] {replay.SessionDate:yyyy-MM-dd HH:mm}: ");
            
            try
            {
                var metadata = _metadataParser.ParseMetadata(replay.FilePath);
                var telemetry = await _telemetryExtractor.ExtractTelemetry(replay.FilePath);
                var profile = _profileAnalyzer.AnalyzeSession(telemetry, metadata);
                
                await _databaseWriter.StoreSessionData(
                    track: metadata.TrackName,
                    car: metadata.CarName,
                    sessionDate: replay.SessionDate,
                    profile: profile
                );
                
                Console.WriteLine($"✓ {metadata.TrackName} - {metadata.CarName} ({profile.LapCount} laps)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
            }
        }
        
        // 4. Generate weighted profiles
        Console.WriteLine("\nGenerating weighted profiles...");
        await _profileGenerator.GenerateWeightedProfiles(outputDbPath);
        
        // 5. Print summary
        PrintSummary(outputDbPath);
    }
}
```

### 4. Recency Weighting Algorithm
```csharp
public class RecencyWeightCalculator
{
    private const double HALF_LIFE_DAYS = 90.0;
    
    public double CalculateWeight(DateTime sessionDate, DateTime referenceDate)
    {
        var ageInDays = (referenceDate - sessionDate).TotalDays;
        
        // Exponential decay: weight halves every 90 days
        var weight = Math.Exp(-Math.Log(2) * ageInDays / HALF_LIFE_DAYS);
        
        return weight;
        
        // Examples:
        // 0 days old:   weight = 1.00 (100%)
        // 30 days old:  weight = 0.81 (81%)
        // 90 days old:  weight = 0.50 (50%)
        // 180 days old: weight = 0.25 (25%)
        // 365 days old: weight = 0.06 (6%)
    }
    
    public double CalculateWeightedAverage(List<SessionData> sessions, DateTime now)
    {
        double weightedSum = 0;
        double weightSum = 0;
        
        foreach (var session in sessions)
        {
            var weight = CalculateWeight(session.Date, now);
            weightedSum += session.FuelPerLap * weight;
            weightSum += weight;
        }
        
        return weightSum > 0 ? weightedSum / weightSum : 0;
    }
}
```

### 5. Confidence Score Calculator
```csharp
public class ConfidenceCalculator
{
    public double Calculate(List<SessionData> sessions, DateTime now)
    {
        if (sessions.Count == 0) return 0.0;
        
        // Factor 1: Recency (40% weight)
        var daysSinceLastSession = (now - sessions.Max(s => s.Date)).TotalDays;
        var recencyScore = Math.Exp(-daysSinceLastSession / 60.0);
        
        // Factor 2: Sample size (30% weight)
        var totalLaps = sessions.Sum(s => s.LapCount);
        var sampleScore = Math.Min(1.0, totalLaps / 100.0);
        
        // Factor 3: Consistency (20% weight)
        var values = sessions.Select(s => s.FuelPerLap).ToList();
        var mean = values.Average();
        var stdDev = Math.Sqrt(values.Average(v => Math.Pow(v - mean, 2)));
        var coefficientOfVariation = mean > 0 ? stdDev / mean : 1.0;
        var consistencyScore = Math.Exp(-coefficientOfVariation * 5);
        
        // Factor 4: Session count (10% weight)
        var sessionScore = Math.Min(1.0, sessions.Count / 10.0);
        
        // Weighted combination
        var confidence = 
            (recencyScore * 0.4) +
            (sampleScore * 0.3) +
            (consistencyScore * 0.2) +
            (sessionScore * 0.1);
        
        return Math.Round(confidence, 2);
    }
}
```

### 6. Database Schema
```sql
-- Time-series storage (raw session data)
CREATE TABLE IF NOT EXISTS ProfileTimeSeries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TrackName TEXT NOT NULL,
    CarName TEXT NOT NULL,
    SessionDate TEXT NOT NULL,  -- ISO 8601 format
    SessionId TEXT,
    SessionType TEXT,
    
    -- Metrics
    LapCount INTEGER,
    FuelPerLap REAL,
    AvgLapTime REAL,
    LapTimeStdDev REAL,
    
    -- Processing metadata
    ProcessedDate TEXT NOT NULL,
    ReplayFilePath TEXT
);

-- Computed profiles (cached weighted predictions)
CREATE TABLE IF NOT EXISTS CurrentProfiles (
    TrackName TEXT NOT NULL,
    CarName TEXT NOT NULL,
    
    -- Weighted predictions
    PredictedFuelPerLap REAL,
    PredictedConsistency REAL,
    
    -- Metadata
    Confidence REAL,
    LastSessionDate TEXT,
    TotalSessions INTEGER,
    TotalLaps INTEGER,
    
    -- Flags
    IsStale INTEGER DEFAULT 0,  -- 1 if last session >180 days old
    
    LastUpdated TEXT NOT NULL,
    
    PRIMARY KEY (TrackName, CarName)
);

-- Create indexes
CREATE INDEX IF NOT EXISTS idx_timeseries_track_car ON ProfileTimeSeries(TrackName, CarName);
CREATE INDEX IF NOT EXISTS idx_timeseries_date ON ProfileTimeSeries(SessionDate);
```

### 7. Summary Report Output
```
Processing complete!

=== Profile Summary ===

Watkins Glen + Porsche 911 GT3 R:
├─ Sessions: 12
├─ Total laps: 347
├─ Date range: 2025-11-08 to 2025-11-16 (8 days)
├─ Most recent: 2 days ago
├─ Confidence: 0.87 (Excellent)
├─ Trend: Stable
├─ Predicted fuel: 2.8L/lap
└─ Historical range: 2.7L to 3.0L/lap

Spa + Oreca 07 LMP2:
├─ Sessions: 5
├─ Total laps: 198
├─ Date range: 2025-11-06 to 2025-11-14 (8 days)
├─ Most recent: 4 days ago
├─ Confidence: 0.79 (Good)
├─ Trend: Improving
├─ Predicted fuel: 1.9L/lap
└─ Historical range: 1.8L to 2.1L/lap

⚠️ Sebring + Porsche 911 GT3 R:
├─ Sessions: 2
├─ Total laps: 45
├─ Date range: 2024-08-10 to 2024-09-15 (36 days)
├─ Most recent: 210 days ago (STALE)
├─ Confidence: 0.35 (Low - Run fresh session)
└─ Historical prediction: 3.1L/lap (may not reflect current form)

Database saved to: ~/.pitwall/profiles.db
Profiles ready for use in SimHub plugin!
```

## Testing Requirements

### Unit Tests
```csharp
[Fact]
public void ExtractSessionDate_DateStampedFile_ParsesCorrectly()
{
    var date = parser.ExtractSessionDate("2025_11_08_09_58_17.rpy");
    Assert.Equal(new DateTime(2025, 11, 8, 9, 58, 17), date);
}

[Fact]
public void ProcessReplays_SortsChronologically()
{
    var replays = new[] { "2025_11_16_...", "2025_11_08_...", "2025_11_14_..." };
    var sorted = processor.SortReplays(replays);
    Assert.True(sorted[0].Date < sorted[1].Date);
    Assert.True(sorted[1].Date < sorted[2].Date);
}

[Fact]
public void RecencyWeight_OlderSessionsWeighLess()
{
    var now = DateTime.Now;
    var recent = now.AddDays(-7);
    var old = now.AddDays(-180);
    
    var recentWeight = calculator.CalculateWeight(recent, now);
    var oldWeight = calculator.CalculateWeight(old, now);
    
    Assert.True(recentWeight > 0.9);  // ~0.95
    Assert.True(oldWeight < 0.3);     // ~0.25
    Assert.True(recentWeight > oldWeight * 3);
}

[Fact]
public void WeightedAverage_FavorsRecentData()
{
    var sessions = new[]
    {
        new SessionData { Date = DateTime.Now.AddDays(-200), FuelPerLap = 3.5 },
        new SessionData { Date = DateTime.Now.AddDays(-7), FuelPerLap = 2.6 }
    };
    
    var weighted = calculator.CalculateWeightedAverage(sessions, DateTime.Now);
    
    // Should be much closer to 2.6 than 3.5
    Assert.InRange(weighted, 2.6, 2.8);
    
    // Not simple average (3.05)
    Assert.NotInRange(weighted, 3.0, 3.1);
}

[Fact]
public void ConfidenceScore_HighForRecentData()
{
    var sessions = new[]
    {
        new SessionData { Date = DateTime.Now.AddDays(-7), LapCount = 50, FuelPerLap = 2.8 },
        new SessionData { Date = DateTime.Now.AddDays(-5), LapCount = 45, FuelPerLap = 2.7 },
        new SessionData { Date = DateTime.Now.AddDays(-2), LapCount = 52, FuelPerLap = 2.8 }
    };
    
    var confidence = calculator.Calculate(sessions, DateTime.Now);
    
    Assert.True(confidence > 0.8);  // High confidence: recent + consistent
}

[Fact]
public void ConfidenceScore_LowForStaleData()
{
    var sessions = new[]
    {
        new SessionData { Date = DateTime.Now.AddDays(-300), LapCount = 30, FuelPerLap = 3.0 }
    };
    
    var confidence = calculator.Calculate(sessions, DateTime.Now);
    
    Assert.True(confidence < 0.4);  // Low confidence: old + small sample
}

[Fact]
public void DatabaseWriter_StoresChronologically()
{
    var sessions = new[]
    {
        new SessionData { Date = new DateTime(2025, 11, 8), Track = "Watkins Glen", Car = "GT3" },
        new SessionData { Date = new DateTime(2025, 11, 14), Track = "Watkins Glen", Car = "GT3" }
    };
    
    writer.StoreTimeSeries(sessions);
    var retrieved = reader.GetTimeSeries("Watkins Glen", "GT3");
    
    Assert.Equal(2, retrieved.Count);
    Assert.True(retrieved[0].Date < retrieved[1].Date);
}
```

### Integration Tests
```csharp
[Fact]
public async Task ProcessReplays_WithRealFiles_GeneratesProfiles()
{
    // Arrange: Use test replay files
    var testFolder = "./TestData/Replays";
    var outputDb = "./TestData/test_profiles.db";
    
    // Act
    var processor = new ReplayProcessor();
    await processor.ProcessReplayLibrary(testFolder, outputDb);
    
    // Assert
    var db = new ProfileDatabase(outputDb);
    var profiles = db.GetAllProfiles();
    
    Assert.True(profiles.Count > 0);
    Assert.All(profiles, p => Assert.True(p.Confidence >= 0 && p.Confidence <= 1));
    Assert.All(profiles, p => Assert.True(p.TotalLaps > 0));
}

[Fact]
public async Task ProcessReplays_MultipleSessions_CalculatesWeightedAverage()
{
    // Test that processing multiple sessions for same track/car
    // produces weighted average that favors recent sessions
    
    var testFolder = "./TestData/Replays/Watkins_Glen_GT3";
    var processor = new ReplayProcessor();
    await processor.ProcessReplayLibrary(testFolder, ":memory:");
    
    var db = new ProfileDatabase(":memory:");
    var profile = db.GetProfile("Watkins Glen", "GT3");
    
    // Verify recent sessions weighted higher
    var timeSeries = db.GetTimeSeries("Watkins Glen", "GT3");
    var mostRecentFuel = timeSeries.OrderByDescending(s => s.Date).First().FuelPerLap;
    
    // Weighted prediction should be close to most recent
    Assert.InRange(profile.PredictedFuelPerLap, mostRecentFuel - 0.3, mostRecentFuel + 0.3);
}
```

## CLI Usage Examples

### Basic Usage
```bash
# Process all replays in default iRacing folder
PitWall.Tools.exe process-replays

# Specify custom replay folder
PitWall.Tools.exe process-replays --input "C:\Users\Username\Documents\iRacing\replays"

# Specify output database path
PitWall.Tools.exe process-replays --input "..." --output "C:\PitWall\my_profiles.db"

# Dry run (show what would be processed without actually processing)
PitWall.Tools.exe process-replays --dry-run

# Process only replays from specific date range
PitWall.Tools.exe process-replays --from "2025-01-01" --to "2025-12-31"

# Verbose output (show detailed processing info)
PitWall.Tools.exe process-replays --verbose
```

### Expected Output
```
PitWall Replay Processor v1.0
==============================

Scanning replay library...
Found 24 replay files in C:\Users\...\iRacing\replays

Extracting metadata...
✓ 24 replays parsed
✓ Date range: 2025-11-02 to 2025-11-16 (14 days)

Sorting chronologically...
✓ Processing order: oldest → newest

Processing replays:
[1/24] 2025-11-02 17:48: ✓ Watkins Glen - Porsche 911 GT3 R (28 laps)
[2/24] 2025-11-06 20:46: ✓ Spa - Oreca 07 LMP2 (35 laps)
[3/24] 2025-11-06 21:00: ✓ Spa - Oreca 07 LMP2 (32 laps)
...
[24/24] 2025-11-16 14:41: ✓ Watkins Glen - Porsche 911 GT3 R (30 laps)

Generating weighted profiles...
✓ 5 track/car combinations processed

=== Summary ===
Profiles created: 5
Total sessions processed: 24
Total laps analyzed: 1,247
Processing time: 11m 34s

Database saved to: C:\Users\...\AppData\Local\PitWall\profiles.db

Run SimHub to use these profiles for predictions!
```

## Integration with Main Plugin

### Plugin Checks for Seeded Database
```csharp
public class PitWallPlugin : IPlugin
{
    private ProfileDatabase _profileDb;
    
    public void Init(PluginManager pluginManager)
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PitWall",
            "profiles.db"
        );
        
        _profileDb = new ProfileDatabase(dbPath);
        
        // Check if database has profiles
        var profileCount = _profileDb.GetProfileCount();
        
        if (profileCount > 0)
        {
            SimHub.Logging.Current.Info($"PitWall: Loaded {profileCount} profiles from database");
        }
        else
        {
            SimHub.Logging.Current.Info("PitWall: No profiles found. Run PitWall.Tools to seed from replays, or profiles will build from live sessions.");
        }
    }
    
    public void DataUpdate(PluginManager pluginManager, ref GameData data)
    {
        var track = data.NewData.TrackName;
        var car = data.NewData.CarName;
        
        // Try to load profile
        var profile = _profileDb.GetProfile(track, car);
        
        if (profile != null && profile.Confidence > 0.5)
        {
            // Use profile for predictions
            var predictedFuel = profile.PredictedFuelPerLap;
            // ... use in strategy calculations
        }
        else
        {
            // Fall back to generic predictions or build profile from current session
            SimHub.Logging.Current.Info($"PitWall: No profile for {track} + {car}, building from live data");
        }
    }
}
```

## Documentation for Users

### README Section
```markdown
## Optional: Seed Database from Replay Files

PitWall can learn from your historical iRacing replays to provide personalized 
predictions from day 1. This is completely optional - the plugin works fine without it.

### Why seed from replays?
- Instant personalized predictions (no need to run practice sessions)
- Uses YOUR actual driving data (fuel consumption, consistency, etc)
- The more replays, the better the predictions

### How to seed from replays:
1. Download `PitWall.Tools.exe` from releases
2. Open command prompt
3. Run: `PitWall.Tools.exe process-replays`
4. Wait 10-15 minutes while it processes your replays
5. Done! Profiles are ready to use in SimHub

### What if I don't want to?
No problem! PitWall will build profiles automatically as you race. It just takes 
a few sessions per track/car to build up enough data.

### How often should I re-run it?
- After major iRacing updates (tire model changes)
- Every few months to keep data fresh
- Or never - live sessions keep profiles updated automatically
```

## Error Handling

### Common Errors and Solutions
```csharp
public class ReplayProcessorErrorHandler
{
    public async Task<ProcessResult> SafeProcessReplay(string filePath)
    {
        try
        {
            return await ProcessReplay(filePath);
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"✗ File not found: {filePath}");
            return ProcessResult.Skipped;
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine($"✗ Permission denied: {filePath}");
            return ProcessResult.Skipped;
        }
        catch (InvalidDataException ex)
        {
            Console.WriteLine($"✗ Corrupted replay file: {ex.Message}");
            return ProcessResult.Skipped;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Unexpected error: {ex.Message}");
            return ProcessResult.Failed;
        }
    }
}
```

## Performance Considerations

### Processing Optimization
```csharp
public class ReplayProcessorOptimized
{
    // Only read YAML header for metadata (fast)
    // Full telemetry extraction only if needed
    
    public async Task<ReplayMetadata> QuickScan(string filePath)
    {
        // Read only first 100KB (YAML header)
        using var stream = File.OpenRead(filePath);
        var buffer = new byte[100 * 1024];
        await stream.ReadAsync(buffer, 0, buffer.Length);
        
        // Parse YAML, return metadata
        return ParseYamlHeader(buffer);
    }
    
    // Parallel processing for multiple replays (optional)
    public async Task ProcessParallel(List<ReplayFileInfo> replays, int maxParallel = 4)
    {
        var semaphore = new SemaphoreSlim(maxParallel);
        var tasks = replays.Select(async replay =>
        {
            await semaphore.WaitAsync();
            try
            {
                await ProcessReplay(replay);
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        await Task.WhenAll(tasks);
    }
}
```

## Success Criteria

Phase 5.A is complete when:
- ✅ Tool can scan iRacing replay folder
- ✅ Both filename patterns (date-stamped and subsession) are handled
- ✅ Replays are processed in chronological order
- ✅ Recency weighting is applied (90-day half-life)
- ✅ Confidence scores are calculated correctly
- ✅ Time-series data is stored in SQLite
- ✅ Weighted profiles are computed and cached
- ✅ Stale data (>180 days) is flagged
- ✅ Summary report shows profiles with confidence scores
- ✅ Main plugin loads and uses seeded profiles
- ✅ All unit tests pass
- ✅ Tool processes 20+ replays without errors
- ✅ Processing time is acceptable (<1 min per replay avg)
- ✅ Feature is clearly marked as OPTIONAL in docs

## Notes for Implementation

### Key Principles
1. **Chronological processing is non-negotiable** - must process oldest → newest
2. **Recency weighting is critical** - recent data must weigh more than old data
3. **Optional by design** - plugin works without replay seeding
4. **Transparent confidence** - show user what data quality is
5. **Fail gracefully** - skip corrupted replays, continue processing
6. **Performance matters** - users have 100+ replays, make it reasonably fast

### Implementation Tips
- Use iRacing SDK documentation for replay file format
- Test with real replay files early (don't wait until end)
- Show progress during processing (users get bored watching nothing)
- Handle Windows file paths correctly (they're painful)
- SQLite is perfect for this - don't overthink database choice
- Consider adding `--track "Watkins Glen"` filter for testing specific tracks

### Future Enhancements (Post-Phase 5.A)
- Export profiles to JSON for sharing with teammates
- Import profiles from other users
- Compare current form vs historical (am I getting faster?)
- Detect major changes (tire model update, setup change)
- Web UI for viewing profile statistics

---

**This is Phase 5.A: Replay processing is optional but powerful. Build it right.**
