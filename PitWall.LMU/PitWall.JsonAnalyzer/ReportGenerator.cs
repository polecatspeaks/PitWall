using System.Text;
using System.Text.Json;

namespace PitWall.JsonAnalyzer;

/// <summary>
/// Generates a comprehensive Markdown report documenting the discovered JSON schema,
/// field categories, change patterns, damage/flag data, and statistics.
/// </summary>
public sealed class ReportGenerator
{
    private readonly SchemaAnalyzer _schema;
    private readonly ChangeDetector _changes;
    private readonly string _inputFile;
    private readonly long _fileSize;
    private readonly long _sampleCount;
    private readonly JsonElement? _sessionMetadata;

    public ReportGenerator(
        SchemaAnalyzer schema,
        ChangeDetector changes,
        string inputFile,
        long fileSize,
        long sampleCount,
        JsonElement? sessionMetadata)
    {
        _schema = schema;
        _changes = changes;
        _inputFile = inputFile;
        _fileSize = fileSize;
        _sampleCount = sampleCount;
        _sessionMetadata = sessionMetadata;
    }

    public async Task WriteReportAsync(string outputPath)
    {
        var sb = new StringBuilder(64 * 1024);

        WriteHeader(sb);
        WriteSessionInfo(sb);
        WriteStatistics(sb);
        WriteSchemaTree(sb);
        WriteCategoryBreakdown(sb);
        WriteDamageFields(sb);
        WriteFlagFields(sb);
        WriteMultiCarAnalysis(sb);
        WriteStaticVsDynamic(sb);
        WriteAnomalies(sb);
        WriteUncategorizedFields(sb);
        WriteFieldDetailsTable(sb);
        WriteAppendix(sb);

        await File.WriteAllTextAsync(outputPath, sb.ToString());
        Console.WriteLine($"Report written to: {outputPath}");
    }

    private void WriteHeader(StringBuilder sb)
    {
        sb.AppendLine("# LMU Telemetry JSON Schema Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Input File**: `{Path.GetFileName(_inputFile)}`");
        sb.AppendLine($"**File Size**: {FormatBytes(_fileSize)}");
        sb.AppendLine($"**Total Samples**: {_sampleCount:N0}");
        sb.AppendLine($"**Unique Fields**: {_schema.CountLeafFields():N0}");
        sb.AppendLine($"**Max Nesting Depth**: {_schema.Root.MaxDepth}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    private void WriteSessionInfo(StringBuilder sb)
    {
        sb.AppendLine("## Session Information");
        sb.AppendLine();

        if (_sessionMetadata.HasValue)
        {
            sb.AppendLine("Extracted from the `session` property of the root JSON object:");
            sb.AppendLine();
            sb.AppendLine("| Field | Value |");
            sb.AppendLine("|-------|-------|");

            foreach (var prop in _sessionMetadata.Value.EnumerateObject())
            {
                string val = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? "null"
                    : prop.Value.GetRawText();
                sb.AppendLine($"| `{prop.Name}` | `{Truncate(val, 80)}` |");
            }
        }
        else
        {
            sb.AppendLine("*No session metadata found in root object.*");
        }

        sb.AppendLine();
    }

    private void WriteStatistics(StringBuilder sb)
    {
        var leafPaths = _schema.GetAllLeafPaths();
        var categories = CategoryTagger.ClassifyAll(leafPaths);

        int multiCarPaths = leafPaths.Count(p => CategoryTagger.IsMultiCarPath(p));
        var staticAgg = _changes.GetAggregatedStaticFields();
        var dynamicAgg = _changes.GetAggregatedDynamicFields(int.MaxValue);
        var staticRaw = _changes.GetStaticFields();
        var dynamicRaw = _changes.GetDynamicFields(int.MaxValue);

        sb.AppendLine("## Statistics Overview");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Total Samples | {_sampleCount:N0} |");
        sb.AppendLine($"| File Size | {FormatBytes(_fileSize)} |");
        sb.AppendLine($"| Avg Sample Size | {FormatBytes(_fileSize / Math.Max(_sampleCount, 1))} |");
        sb.AppendLine($"| Unique Leaf Fields | {leafPaths.Count:N0} |");
        sb.AppendLine($"| Tracked Paths (per-index) | {_changes.FieldChanges.Count:N0} |");
        sb.AppendLine($"| Static Paths | {staticRaw.Count:N0} ({staticAgg.Count:N0} unique) |");
        sb.AppendLine($"| Dynamic Paths | {dynamicRaw.Count:N0} ({dynamicAgg.Count:N0} unique) |");
        sb.AppendLine($"| Multi-Car Fields | {multiCarPaths:N0} |");
        sb.AppendLine($"| Max Nesting Depth | {_schema.Root.MaxDepth} |");

        sb.AppendLine();
        sb.AppendLine("### Fields by Category");
        sb.AppendLine();

        var grouped = categories.GroupBy(kv => kv.Value)
            .OrderByDescending(g => g.Count())
            .ToList();

        sb.AppendLine("| Category | Count | % |");
        sb.AppendLine("|----------|------:|--:|");
        foreach (var g in grouped)
        {
            double pct = 100.0 * g.Count() / leafPaths.Count;
            sb.AppendLine($"| {CategoryTagger.CategoryDisplayName(g.Key)} | {g.Count()} | {pct:F1}% |");
        }
        sb.AppendLine();
    }

    private void WriteSchemaTree(StringBuilder sb)
    {
        sb.AppendLine("## Complete Schema Tree");
        sb.AppendLine();
        sb.AppendLine("```");

        WriteSchemaNodeTree(sb, _schema.Root, "", true);

        sb.AppendLine("```");
        sb.AppendLine();
    }

    private void WriteSchemaNodeTree(StringBuilder sb, SchemaNode node, string indent, bool isLast)
    {
        string connector = indent.Length == 0 ? "" : (isLast ? "└── " : "├── ");
        string typeInfo = node.TypeDescription;

        string extra = "";
        if (node.MinValue.HasValue && node.MaxValue.HasValue)
            extra += $" [{node.MinValue:G5}..{node.MaxValue:G5}]";
        if (node.MinArrayLength.HasValue)
            extra += $" len={node.MinArrayLength}..{node.MaxArrayLength}";
        if (node.DistinctValues.Count > 0 && node.DistinctValues.Count <= 5 && !node.DistinctValuesCapped)
            extra += $" vals={{{string.Join(",", node.DistinctValues.Take(5))}}}";
        if (node.OccurrenceCount > 0 && node.OccurrenceCount < _sampleCount)
            extra += $" (seen in {node.OccurrenceCount}/{_sampleCount} samples)";

        if (!string.IsNullOrEmpty(node.Name))
            sb.AppendLine($"{indent}{connector}{node.Name}: {typeInfo}{extra}");

        string childIndent = indent + (indent.Length == 0 ? "" : (isLast ? "    " : "│   "));

        var children = node.Children.Values.ToList();
        if (node.ArrayElementSchema != null)
            children.Add(node.ArrayElementSchema);

        for (int i = 0; i < children.Count; i++)
            WriteSchemaNodeTree(sb, children[i], childIndent, i == children.Count - 1);
    }

    private void WriteCategoryBreakdown(StringBuilder sb)
    {
        var leafPaths = _schema.GetAllLeafPaths();
        var categories = CategoryTagger.ClassifyAll(leafPaths);

        var grouped = categories
            .GroupBy(kv => kv.Value)
            .OrderBy(g => g.Key)
            .ToList();

        sb.AppendLine("## Fields by Category");
        sb.AppendLine();

        foreach (var group in grouped)
        {
            sb.AppendLine($"### {CategoryTagger.CategoryDisplayName(group.Key)}");
            sb.AppendLine();

            var paths = group.Select(kv => kv.Key).OrderBy(p => p).ToList();
            int showCount = Math.Min(paths.Count, 50);
            if (paths.Count > 50)
            {
                sb.AppendLine($"*{paths.Count} fields — showing first {showCount}:*");
                sb.AppendLine();
            }
            foreach (var path in paths.Take(showCount))
            {
                var node = FindNode(path);
                string typeStr = node?.TypeDescription ?? "?";
                var (freq, _) = LookupChangeInfo(path);
                string freqStr = freq != UpdateFrequency.Unknown ? $" ({freq})" : "";
                sb.AppendLine($"- `{path}` — {typeStr}{freqStr}");
            }
            if (paths.Count > showCount)
                sb.AppendLine($"- ... and {paths.Count - showCount} more");

            sb.AppendLine();
        }
    }

    private void WriteDamageFields(StringBuilder sb)
    {
        sb.AppendLine("## Damage-Related Fields");
        sb.AppendLine();
        sb.AppendLine("Fields that may contain damage information (suspension, body, tires, impacts):");
        sb.AppendLine();

        var leafPaths = _schema.GetAllLeafPaths();
        var damagePaths = leafPaths.Where(p => CategoryTagger.Classify(p) == FieldCategory.Damage).OrderBy(p => p).ToList();

        if (damagePaths.Count == 0)
        {
            sb.AppendLine("*No fields matching damage patterns found. Check Uncategorized section for possible damage data under different names.*");
        }
        else
        {
            sb.AppendLine("| Path | Type | Range | Update Freq | Sample Values |");
            sb.AppendLine("|------|------|-------|-------------|---------------|");

            foreach (var path in damagePaths)
            {
                var node = FindNode(path);
                string typeStr = node?.TypeDescription ?? "?";
                string range = node?.MinValue.HasValue == true ? $"{node.MinValue:G4}..{node.MaxValue:G4}" : "-";
                var (freq, _) = LookupChangeInfo(path);
                string samples = node != null ? string.Join(", ", node.SampleValues.Take(3)) : "";
                sb.AppendLine($"| `{path}` | {typeStr} | {range} | {freq} | {Truncate(samples, 40)} |");
            }
        }

        sb.AppendLine();
    }

    private void WriteFlagFields(StringBuilder sb)
    {
        sb.AppendLine("## Flag-Related Fields");
        sb.AppendLine();
        sb.AppendLine("Fields that may contain flag/penalty information:");
        sb.AppendLine();

        var leafPaths = _schema.GetAllLeafPaths();
        var flagPaths = leafPaths.Where(p => CategoryTagger.Classify(p) == FieldCategory.Flags).OrderBy(p => p).ToList();

        if (flagPaths.Count == 0)
        {
            sb.AppendLine("*No fields matching flag patterns found. Check Uncategorized section.*");
        }
        else
        {
            sb.AppendLine("| Path | Type | Sample Values | Update Freq | Changes |");
            sb.AppendLine("|------|------|---------------|-------------|---------|");

            foreach (var path in flagPaths)
            {
                var node = FindNode(path);
                string typeStr = node?.TypeDescription ?? "?";
                string samples = node != null ? string.Join(", ", node.SampleValues.Take(3)) : "";
                var (freq, changes) = LookupChangeInfo(path);
                sb.AppendLine($"| `{path}` | {typeStr} | {Truncate(samples, 40)} | {freq} | {changes:N0} |");
            }
        }

        sb.AppendLine();
    }

    private void WriteMultiCarAnalysis(StringBuilder sb)
    {
        sb.AppendLine("## Multi-Car Analysis");
        sb.AppendLine();

        var leafPaths = _schema.GetAllLeafPaths();
        var multiCarPaths = leafPaths.Where(p => CategoryTagger.IsMultiCarPath(p)).OrderBy(p => p).ToList();

        // Try to find vehicle array info
        var vehicleArrayNode = FindNodeByPartialPath("Vehicles") ?? FindNodeByPartialPath("vehicles");

        if (vehicleArrayNode != null)
        {
            sb.AppendLine($"**Vehicle array found**: `{vehicleArrayNode.Path}`");
            sb.AppendLine($"**Array length**: {vehicleArrayNode.MinArrayLength}..{vehicleArrayNode.MaxArrayLength}");
            sb.AppendLine();

            if (vehicleArrayNode.ArrayElementSchema != null)
            {
                sb.AppendLine("**Per-vehicle fields:**");
                sb.AppendLine();
                var perVehicleFields = new List<string>();
                CollectChildPaths(vehicleArrayNode.ArrayElementSchema, perVehicleFields);
                foreach (var field in perVehicleFields.Take(50))
                {
                    var node = FindNode(field);
                    sb.AppendLine($"- `{field}` — {node?.TypeDescription ?? "?"}");
                }
                if (perVehicleFields.Count > 50)
                    sb.AppendLine($"- ... and {perVehicleFields.Count - 50} more fields per vehicle");
            }
        }
        else
        {
            sb.AppendLine($"**Multi-car field paths found**: {multiCarPaths.Count}");
            if (multiCarPaths.Count > 0)
            {
                sb.AppendLine();
                foreach (var p in multiCarPaths.Take(20))
                    sb.AppendLine($"- `{p}`");
                if (multiCarPaths.Count > 20)
                    sb.AppendLine($"- ... and {multiCarPaths.Count - 20} more");
            }
            else
            {
                sb.AppendLine("*No obvious multi-car data detected. Check if vehicle data is at root level.*");
            }
        }

        sb.AppendLine();
    }

    private void WriteStaticVsDynamic(StringBuilder sb)
    {
        sb.AppendLine("## Static vs Dynamic Fields");
        sb.AppendLine();

        var staticAgg = _changes.GetAggregatedStaticFields();
        var dynamicAgg = _changes.GetAggregatedDynamicFields(200);

        sb.AppendLine($"### Static Fields ({staticAgg.Count:N0} unique patterns — constant throughout session)");
        sb.AppendLine();
        sb.AppendLine("These are good candidates for **session metadata** (store once per session):");
        sb.AppendLine();

        sb.AppendLine("| Path | Indices | Value |");
        sb.AppendLine("|------|--------:|-------|");
        foreach (var f in staticAgg.Take(60))
        {
            string indices = f.IsArrayPath ? $"{f.IndexCount}" : "—";
            sb.AppendLine($"| `{f.WildcardPath}` | {indices} | `{Truncate(f.FirstValue ?? "", 50)}` |");
        }
        if (staticAgg.Count > 60)
        {
            sb.AppendLine();
            sb.AppendLine($"*... and {staticAgg.Count - 60} more static field patterns*");
        }

        sb.AppendLine();
        sb.AppendLine($"### Most Dynamic Fields (top {Math.Min(dynamicAgg.Count, 80)} unique patterns)");
        sb.AppendLine();
        sb.AppendLine("These need **time-series storage** (per-sample columns in DuckDB):");
        sb.AppendLine();
        sb.AppendLine("| Path | Indices | Avg Changes | Max Changes | Frequency | Max Delta |");
        sb.AppendLine("|------|--------:|------------:|------------:|-----------|----------:|");

        foreach (var f in dynamicAgg.Take(80))
        {
            string indices = f.IsArrayPath ? $"{f.IndexCount}" : "—";
            string delta = f.MaxDelta > 0 ? $"{f.MaxDelta:G6}" : "—";
            sb.AppendLine($"| `{f.WildcardPath}` | {indices} | {f.AvgChanges:F1} | {f.MaxChanges:N0} | {f.RepresentativeFrequency} | {delta} |");
        }

        sb.AppendLine();
    }

    private void WriteAnomalies(StringBuilder sb)
    {
        sb.AppendLine("## Anomaly Detection (Potential Damage Events)");
        sb.AppendLine();
        sb.AppendLine("Fields with large single-step value changes that may indicate collisions, damage, or flag state changes:");
        sb.AppendLine();

        var anomalies = _changes.GetAggregatedAnomalies(5.0);

        if (anomalies.Count == 0)
        {
            sb.AppendLine("*No large anomalies detected (threshold: delta >= 5.0).*");
        }
        else
        {
            sb.AppendLine("| Path | Max Delta | Tot. Changes | Indices | Category | First Changes At |");
            sb.AppendLine("|------|----------:|-------------:|--------:|----------|-----------------|");

            foreach (var a in anomalies.Take(60))
            {
                var cat = CategoryTagger.Classify(a.WildcardPath);
                string firstChanges = a.FirstChangeIndices.Count > 0
                    ? string.Join(", ", a.FirstChangeIndices.Select(i => $"#{i:N0}"))
                    : "?";
                string indices = a.IsArrayPath ? $"{a.IndexCount}" : "—";
                sb.AppendLine($"| `{a.WildcardPath}` | {a.MaxDelta:G6} | {a.TotalChanges:N0} | {indices} | {CategoryTagger.CategoryDisplayName(cat)} | {firstChanges} |");
            }
        }

        sb.AppendLine();
    }

    private void WriteUncategorizedFields(StringBuilder sb)
    {
        sb.AppendLine("## Uncategorized Fields");
        sb.AppendLine();
        sb.AppendLine("Fields that didn't match any known category pattern. Review these manually — they may contain important data under unexpected names:");
        sb.AppendLine();

        var leafPaths = _schema.GetAllLeafPaths();
        var uncategorized = leafPaths
            .Where(p => CategoryTagger.Classify(p) == FieldCategory.Uncategorized)
            .OrderBy(p => p)
            .ToList();

        if (uncategorized.Count == 0)
        {
            sb.AppendLine("*All fields categorized.*");
        }
        else
        {
            foreach (var path in uncategorized.Take(100))
            {
                var node = FindNode(path);
                string typeStr = node?.TypeDescription ?? "?";
                var (freq, _) = LookupChangeInfo(path);
                sb.AppendLine($"- `{path}` — {typeStr} ({freq})");
            }

            if (uncategorized.Count > 100)
                sb.AppendLine($"\n*... and {uncategorized.Count - 100} more uncategorized fields*");
        }

        sb.AppendLine();
    }

    private void WriteFieldDetailsTable(StringBuilder sb)
    {
        sb.AppendLine("## Complete Field Details");
        sb.AppendLine();
        sb.AppendLine("Full table of all discovered leaf fields:");
        sb.AppendLine();
        sb.AppendLine("| Path | Type | Range | Frequency | Category | Sample |");
        sb.AppendLine("|------|------|-------|-----------|----------|--------|");

        var leafPaths = _schema.GetAllLeafPaths();
        foreach (var path in leafPaths.OrderBy(p => p))
        {
            var node = FindNode(path);
            string typeStr = node?.TypeDescription ?? "?";
            string range = node?.MinValue.HasValue == true ? $"{node.MinValue:G4}..{node.MaxValue:G4}" : "-";
            var cat = CategoryTagger.Classify(path);
            var (freq, _) = LookupChangeInfo(path);
            string sample = node?.SampleValues.FirstOrDefault() ?? "";
            sb.AppendLine($"| `{path}` | {typeStr} | {range} | {freq} | {CategoryTagger.CategoryDisplayName(cat)} | `{Truncate(sample, 30)}` |");
        }

        sb.AppendLine();
    }

    private void WriteAppendix(StringBuilder sb)
    {
        sb.AppendLine("## Appendix");
        sb.AppendLine();
        sb.AppendLine("### Extracted Sample Files");
        sb.AppendLine();
        sb.AppendLine("Sample JSON objects are saved alongside this report for manual inspection:");
        sb.AppendLine();
        sb.AppendLine("- `sample_000_first_*.json` — First sample (session start)");
        sb.AppendLine("- `sample_001_25pct_*.json` — 25% through session");
        sb.AppendLine("- `sample_002_50pct_*.json` — 50% through session (mid-race)");
        sb.AppendLine("- `sample_003_75pct_*.json` — 75% through session");
        sb.AppendLine("- `sample_004_last_*.json` — Last sample (session end)");
        sb.AppendLine("- `sample_005+_interesting_*.json` — Samples with highest field-change counts (potential damage/flag events)");
        sb.AppendLine();
        sb.AppendLine("### Update Frequency Key");
        sb.AppendLine();
        sb.AppendLine("| Frequency | Description |");
        sb.AppendLine("|-----------|-------------|");
        sb.AppendLine("| Static | Never changes during session |");
        sb.AppendLine("| Sparse | Changes <1% of samples |");
        sb.AppendLine("| PerLap | Changes 1-10% of samples (roughly per-lap) |");
        sb.AppendLine("| Frequent | Changes 10-80% of samples |");
        sb.AppendLine("| PerSample | Changes >80% of samples (high-frequency telemetry) |");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Report generated by PitWall.JsonAnalyzer — LMU Telemetry Schema Discovery Tool*");
    }

    // --- Helpers ---

    /// <summary>
    /// Look up aggregated change info for a schema path (which uses [*] for arrays).
    /// Falls back to direct lookup for non-array paths.
    /// </summary>
    private (UpdateFrequency freq, long changes) LookupChangeInfo(string schemaPath)
    {
        // Direct lookup first (works for non-array paths)
        if (_changes.FieldChanges.TryGetValue(schemaPath, out var direct))
            return (direct.Frequency, direct.ChangeCount);

        // For paths with [*], find any matching per-index field
        string wildcardPath = ChangeDetector.WildcardPath(schemaPath);
        var matching = _changes.FieldChanges.Values
            .Where(f => ChangeDetector.WildcardPath(f.Path) == wildcardPath)
            .ToList();

        if (matching.Count > 0)
        {
            // Use the most-active index as representative
            var best = matching.OrderByDescending(f => f.ChangeCount).First();
            return (best.Frequency, best.ChangeCount);
        }

        return (UpdateFrequency.Unknown, 0);
    }

    private SchemaNode? FindNode(string path)
    {
        var parts = ParsePath(path);
        SchemaNode current = _schema.Root;

        foreach (var part in parts)
        {
            if (part == "[*]")
            {
                if (current.ArrayElementSchema == null) return null;
                current = current.ArrayElementSchema;
            }
            else
            {
                // Handle indexed array access like [0], [1]
                if (part.StartsWith('[') && part.EndsWith(']'))
                {
                    if (current.ArrayElementSchema == null) return null;
                    current = current.ArrayElementSchema;
                }
                else if (!current.Children.TryGetValue(part, out var child))
                {
                    return null;
                }
                else
                {
                    current = child;
                }
            }
        }

        return current;
    }

    private SchemaNode? FindNodeByPartialPath(string namePart)
    {
        return FindNodeByPartialPathRecursive(_schema.Root, namePart);
    }

    private static SchemaNode? FindNodeByPartialPathRecursive(SchemaNode node, string namePart)
    {
        if (node.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase))
            return node;

        foreach (var child in node.Children.Values)
        {
            var found = FindNodeByPartialPathRecursive(child, namePart);
            if (found != null) return found;
        }

        if (node.ArrayElementSchema != null)
        {
            var found = FindNodeByPartialPathRecursive(node.ArrayElementSchema, namePart);
            if (found != null) return found;
        }

        return null;
    }

    private static void CollectChildPaths(SchemaNode node, List<string> paths)
    {
        if (node.Children.Count == 0 && node.ArrayElementSchema == null && !string.IsNullOrEmpty(node.Path))
        {
            paths.Add(node.Path);
            return;
        }

        foreach (var child in node.Children.Values)
            CollectChildPaths(child, paths);

        if (node.ArrayElementSchema != null)
            CollectChildPaths(node.ArrayElementSchema, paths);
    }

    private static List<string> ParsePath(string path)
    {
        var parts = new List<string>();
        int start = 0;
        for (int i = 0; i < path.Length; i++)
        {
            if (path[i] == '.')
            {
                if (i > start)
                    parts.Add(path[start..i]);
                start = i + 1;
            }
            else if (path[i] == '[')
            {
                if (i > start)
                    parts.Add(path[start..i]);
                int end = path.IndexOf(']', i);
                if (end < 0) end = path.Length - 1;
                parts.Add(path[i..(end + 1)]);
                start = end + 1;
                if (start < path.Length && path[start] == '.')
                    start++;
            }
        }
        if (start < path.Length)
            parts.Add(path[start..]);
        return parts;
    }

    private static string Truncate(string s, int max)
    {
        if (s.Length <= max) return s;
        return s[..(max - 3)] + "...";
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):F2} GB",
            >= 1024L * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            >= 1024L => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes} B"
        };
    }
}
