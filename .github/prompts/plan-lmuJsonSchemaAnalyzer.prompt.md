# Plan: LMU JSON Telemetry Schema Analyzer

A new `PitWall.JsonAnalyzer` .NET 9 console app that streams a 2.5GB JSON telemetry file, discovers its complete schema, analyzes field dynamics (static vs. changing), identifies damage/flag data, and outputs a comprehensive Markdown reference report for STARwall design.

## Key Constraints Discovered

- The JSON contains deeply nested per-sample objects with `TimestampUtc`, `Telemetry`, `Scoring`, `Electronics` sections
- ~649 `VehicleName` occurrences in 2MB = heavy per-vehicle nesting across ~20+ AI cars
- Existing codebase uses `System.Text.Json` exclusively, .NET 9
- ~95 known DuckDB channel names provide hints, but the raw JSON may expose far more fields (damage, flags, etc.)
- Root structure: `{"session": {...}, "samples": [{...}, ...]}`

## Steps

### 1. Create Project

Add `PitWall.LMU/PitWall.JsonAnalyzer/PitWall.JsonAnalyzer.csproj` as a .NET 9 `Exe` project. Single dependency: `System.Text.Json` (built-in). Add to `PitWall.LMU.sln` via `dotnet sln add`. No references to other PitWall projects needed — this is a standalone analysis tool.

### 2. Streaming JSON Reader (`JsonStreamReader.cs`)

Use `FileStream` with `JsonSerializer.DeserializeAsyncEnumerable<JsonElement>()` if root is an array, or `JsonDocument.Parse()` on chunked reads if root is an object. Auto-detect format by reading first byte (`[` = array, `{` = object). For the array case, each element arrives as a `JsonElement` without loading the entire file. Track bytes read via `FileStream.Position` for progress reporting.

Given the discovered root structure `{"session": {...}, "samples": [...]}`, the reader should:
- Parse the root object to extract `session` metadata first
- Then stream the `samples` array element-by-element using `Utf8JsonReader` to avoid loading the full 2.5GB array

### 3. Schema Walker (`SchemaAnalyzer.cs`)

Recursively walk each `JsonElement`:
- Build a tree of `SchemaNode { Path, JsonValueKind, SampleValues (first 5), MinValue, MaxValue, OccurrenceCount, DistinctValueCount (capped at 100), ChildNodes }`.
- Track array element schemas separately (e.g., `Scoring.Vehicles[*].VehicleName`).
- Merge schemas across samples (union of all fields seen, noting which samples introduced new fields).
- Cap sample value storage to avoid memory bloat.

### 4. Change Detector (`ChangeDetector.cs`)

For each leaf field path:
- Store hash of first-seen value and count how many times it changes across samples.
- Classify: `Static` (never changes), `PerLap` (changes infrequently), `PerSample` (changes every sample), `Sparse` (changes rarely/irregularly).
- Track approximate update frequency in Hz based on timestamp deltas.
- Flag fields whose values change dramatically (potential damage events — e.g., a suspension field going from nominal to degraded).

### 5. Category Tagger (`CategoryTagger.cs`)

Pattern-match field paths to identify data categories:
- **Damage**: paths containing `damage`, `dent`, `detach`, `wear`, `impact`, `collision`, `broken`, `flat`
- **Flags**: paths containing `flag`, `yellow`, `blue`, `caution`, `safety`, `sector.*flag`, `penalty`
- **Telemetry**: `speed`, `throttle`, `brake`, `steer`, `fuel`, `tyre`, `temp`, `gear`, `rpm`
- **Session**: `track`, `session`, `weather`, `car`, `class`, `type`
- **Multi-car**: paths under vehicle arrays (detected by array-of-objects with `VehicleName` or similar identifiers)
- **Uncategorized**: everything else — explicitly listed in the report for manual review

### 6. Sample Extractor (`SampleExtractor.cs`)

Extract 5-10 complete sample `JsonElement` objects:
- Sample 0: First sample (session start)
- Sample 1: ~25% through file
- Sample 2: ~50% through file (mid-race)
- Sample 3: ~75% through file
- Sample 4: Last sample (session end)
- Samples 5-9: Detect "interesting" samples where many fields changed simultaneously (potential damage/flag events) — compare consecutive samples, score by number of field changes, pick top 5.
- Write each to `output/sample_NNN.json` with `JsonSerializerOptions { WriteIndented = true }`.

### 7. Progress Reporter (`ProgressReporter.cs`)

Console progress bar:
- Show `[####....] 45.2% | 1.13 GB / 2.50 GB | ~3:42 remaining | 12,450 samples`
- Update every 100 samples or 1 second (whichever comes first) to avoid console spam.
- Use `Stopwatch` + bytes-per-second for ETA calculation.

### 8. Report Generator (`ReportGenerator.cs`)

Output comprehensive Markdown to `output/telemetry_schema_report.md`:
- **Header**: File info (name, size, sample count, time range, cars tracked)
- **Schema Tree**: Full field hierarchy with types, rendered as indented tree with `├──` connectors
- **Field Details Table**: Path | Type | Range (min-max) | Update Frequency | Category | Sample Value
- **Damage Fields**: Dedicated section listing all damage-related paths with sample values and change patterns
- **Flag Fields**: Dedicated section for flag/penalty data
- **Multi-Car Analysis**: How many vehicles, what per-vehicle data exists, is AI telemetry as detailed as player?
- **Static vs Dynamic**: Table of static fields (good for session metadata) vs. dynamic fields (need time-series storage)
- **Anomaly Detection**: Fields that changed dramatically (damage events)
- **Uncategorized Fields**: Everything that didn't match known patterns — flagged for manual review
- **Statistics**: Total samples, unique field count, nesting depth, estimated data rate
- **Appendix**: Links to extracted sample JSON files

### 9. Main Entry Point (`Program.cs`)

Wire everything together:
- Parse CLI args: `--input <path>` (required), `--output <dir>` (default: `./output`), `--max-samples <n>` (default: all, for quick test runs), `--skip-samples` (skip JSON extraction).
- Validate input file exists.
- Run streaming read → schema analysis → change detection → categorization → sample extraction → report generation.
- Print summary stats to console at completion.

## Verification

- `dotnet build PitWall.LMU/PitWall.JsonAnalyzer/PitWall.JsonAnalyzer.csproj` — compiles cleanly
- `dotnet run --project PitWall.LMU/PitWall.JsonAnalyzer -- --input "C:\Users\ohzee\git\lmu_telemetry_20260215_195813\lmu_telemetry_20260215_185721.json" --max-samples 100` — quick schema-only run on first 100 samples to validate before full run
- Full run without `--max-samples` produces the complete report
- Output directory contains `telemetry_schema_report.md` + `sample_*.json` files
- Memory stays under ~500MB during the full 2.5GB parse (streaming, not buffered)

## Decisions

- **System.Text.Json streaming over Newtonsoft**: Consistent with codebase, better performance for large files, built-in to .NET 9
- **Standalone project, no Core dependency**: This is a one-shot analysis tool; it shouldn't import PitWall models since we're discovering schema from scratch
- **Schema-first approach over typed deserialization**: We deliberately avoid defining DTOs — the whole point is to discover what's there
- **JsonElement walking over Utf8JsonReader**: `DeserializeAsyncEnumerable<JsonElement>` gives us complete per-sample elements to walk recursively while still streaming, avoiding the complexity of raw `Utf8JsonReader` state management
- **Category patterns are intentionally broad**: Better to over-match and let the user filter the report than miss damage/flag fields with too-narrow patterns
