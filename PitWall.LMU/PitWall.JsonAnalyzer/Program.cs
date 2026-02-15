using PitWall.JsonAnalyzer;

// ============================================================================
// PitWall.JsonAnalyzer — LMU Telemetry JSON Schema Discovery Tool
//
// Streams a large JSON telemetry file, discovers the complete schema,
// analyzes field dynamics, identifies damage/flag data, and outputs
// a comprehensive Markdown report for STARwall architecture design.
//
// Usage:
//   dotnet run --project PitWall.JsonAnalyzer -- --input <path> [options]
//
// Options:
//   --input <path>       Path to the JSON telemetry file (required)
//   --output <dir>       Output directory (default: ./output)
//   --max-samples <n>    Max samples to process (default: all)
//   --stride <n>         Analyze every Nth sample (default: 1 = all)
//   --skip-samples       Skip extracting sample JSON files
// ============================================================================

var inputFile = GetArg("--input");
var outputDir = GetArg("--output") ?? Path.Combine(Directory.GetCurrentDirectory(), "output");
var maxSamplesStr = GetArg("--max-samples");
int maxSamples = maxSamplesStr != null ? int.Parse(maxSamplesStr) : int.MaxValue;
var strideStr = GetArg("--stride");
int stride = strideStr != null ? Math.Max(1, int.Parse(strideStr)) : 1;
bool skipSamples = args.Contains("--skip-samples", StringComparer.OrdinalIgnoreCase);

if (string.IsNullOrEmpty(inputFile))
{
    Console.Error.WriteLine("Error: --input <path> is required.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Usage: dotnet run --project PitWall.JsonAnalyzer -- --input <path> [--output <dir>] [--max-samples <n>] [--stride <n>] [--skip-samples]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Example:");
    Console.Error.WriteLine(@"  dotnet run --project PitWall.JsonAnalyzer -- --input ""C:\Users\ohzee\git\lmu_telemetry.json"" --max-samples 100");
    return 1;
}

if (!File.Exists(inputFile))
{
    Console.Error.WriteLine($"Error: File not found: {inputFile}");
    return 1;
}

Directory.CreateDirectory(outputDir);

var fileInfo = new FileInfo(inputFile);
Console.WriteLine($"PitWall.JsonAnalyzer — LMU Telemetry Schema Discovery");
Console.WriteLine($"======================================================");
Console.WriteLine($"Input:       {fileInfo.Name}");
Console.WriteLine($"Size:        {fileInfo.Length / (1024.0 * 1024 * 1024):F2} GB ({fileInfo.Length:N0} bytes)");
Console.WriteLine($"Output:      {outputDir}");
Console.WriteLine($"Max samples: {(maxSamples == int.MaxValue ? "all" : maxSamples.ToString("N0"))}");
Console.WriteLine($"Stride:      {(stride > 1 ? $"every {stride:N0}th sample" : "1 (all)")}");
Console.WriteLine($"Extract:     {(!skipSamples ? "yes" : "no")}");
Console.WriteLine();

// Initialize components
var schema = new SchemaAnalyzer();
var changes = new ChangeDetector();
var extractor = new SampleExtractor();
var progress = new ProgressReporter(fileInfo.Length);
System.Text.Json.JsonElement? capturedSessionMetadata = null;

// Phase 1: Stream and analyze
Console.WriteLine("Phase 1: Streaming JSON and analyzing schema...");
Console.WriteLine();
progress.Start();

long sampleCount = 0;

using (var reader = new JsonStreamReader(inputFile))
{
    // Analyze session metadata if present
    await foreach (var (element, bytePos) in reader.ReadSamplesAsync(1))
    {
        // Check if session metadata was extracted during header parsing
        if (reader.SessionMetadata.HasValue)
        {
            schema.AnalyzeSessionMetadata(reader.SessionMetadata.Value);
            capturedSessionMetadata = reader.SessionMetadata.Value;
            Console.WriteLine("Session metadata extracted.");
        }
        break; // Just needed to trigger header parse
    }

    // Reset and stream all samples
    using var reader2 = new JsonStreamReader(inputFile);

    // For sample extraction, set estimated total (use file size heuristic)
    // We'll refine after counting
    long estimatedSamples = fileInfo.Length / 18_000; // rough estimate: ~18KB per sample for multi-car data
    extractor.SetTotalSamples(estimatedSamples);

    // With stride, we read more raw samples but only analyze every Nth one.
    // maxRead = how many raw samples to read from the stream.
    long maxRead = stride > 1
        ? Math.Min((long)maxSamples * stride, (long)int.MaxValue)
        : maxSamples;

    long rawIndex = 0; // position in the stream

    await foreach (var (element, bytePos) in reader2.ReadSamplesAsync((int)Math.Min(maxRead, int.MaxValue)))
    {
        if (rawIndex % stride == 0)
        {
            schema.AnalyzeSample(element, sampleCount);
            changes.RecordSample(element, sampleCount);

            if (!skipSamples)
                extractor.RecordSample(element, sampleCount);

            sampleCount++;
        }

        rawIndex++;
        progress.Update(bytePos, sampleCount);
    }

    // Capture session metadata from second reader if not already set
    if (!capturedSessionMetadata.HasValue && reader2.SessionMetadata.HasValue)
        capturedSessionMetadata = reader2.SessionMetadata.Value;
}

progress.Finish();
Console.WriteLine();

// Phase 2: Extract samples
if (!skipSamples)
{
    Console.WriteLine("Phase 2: Writing extracted sample files...");
    extractor.TotalSamples = sampleCount; // refine with actual count
    await extractor.WriteSamplesAsync(outputDir);
    Console.WriteLine();
}

// Phase 3: Generate report
Console.WriteLine("Phase 3: Generating schema report...");

var report = new ReportGenerator(
    schema, changes, inputFile, fileInfo.Length, sampleCount, capturedSessionMetadata);

string reportPath = Path.Combine(outputDir, "telemetry_schema_report.md");
await report.WriteReportAsync(reportPath);

// Summary
Console.WriteLine();
Console.WriteLine("=== Summary ===");
Console.WriteLine($"Samples processed: {sampleCount:N0}");
Console.WriteLine($"Unique fields:     {schema.CountLeafFields():N0}");
Console.WriteLine($"Max depth:         {schema.Root.MaxDepth}");
Console.WriteLine($"Tracked paths:     {changes.FieldChanges.Count:N0}");
Console.WriteLine($"Static paths:      {changes.GetStaticFields().Count:N0} ({changes.GetAggregatedStaticFields().Count:N0} unique)");
Console.WriteLine($"Dynamic paths:     {changes.GetDynamicFields(int.MaxValue).Count:N0} ({changes.GetAggregatedDynamicFields(int.MaxValue).Count:N0} unique)");
Console.WriteLine($"Anomalies:         {changes.GetAggregatedAnomalies().Count:N0}");
Console.WriteLine($"Report:            {reportPath}");
Console.WriteLine();
Console.WriteLine("Done! Open the report to explore the LMU telemetry schema.");

return 0;

// --- Helpers ---

string? GetArg(string name)
{
    int idx = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
    if (idx < 0 || idx + 1 >= args.Length) return null;
    return args[idx + 1];
}
