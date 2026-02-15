using System.Text.Json;

namespace PitWall.JsonAnalyzer;

/// <summary>
/// Extracts representative sample objects from the telemetry stream.
/// Picks samples at fixed intervals (start, 25%, 50%, 75%, end) plus
/// "interesting" samples where many fields changed simultaneously (damage/flag events).
/// </summary>
public sealed class SampleExtractor
{
    private readonly int _maxInterestingSamples;

    /// <summary>Fixed-position samples: index 0=first, then 25%/50%/75%/last.</summary>
    private readonly Dictionary<string, (long targetIndex, string? json)> _fixedSamples = new();

    /// <summary>Interesting samples ranked by change score.</summary>
    private readonly SortedList<int, (long sampleIndex, string json)> _interestingSamples = new(
        Comparer<int>.Create((a, b) => b.CompareTo(a))); // descending by score

    /// <summary>Previous sample's leaf value hashes for change detection.</summary>
    private Dictionary<string, int> _previousHashes = new();

    public long TotalSamples { get; set; }

    public SampleExtractor(int maxInterestingSamples = 5)
    {
        _maxInterestingSamples = maxInterestingSamples;
    }

    /// <summary>
    /// Set the total expected sample count so we can compute 25/50/75% targets.
    /// Call this before RecordSample if known, or after first pass to pick fixed samples.
    /// </summary>
    public void SetTotalSamples(long total)
    {
        TotalSamples = total;
        _fixedSamples["first"] = (0, null);
        _fixedSamples["25pct"] = (total / 4, null);
        _fixedSamples["50pct"] = (total / 2, null);
        _fixedSamples["75pct"] = (total * 3 / 4, null);
        _fixedSamples["last"] = (total - 1, null);
    }

    /// <summary>
    /// Record a sample during streaming. Captures fixed-position samples and scores
    /// "interesting" samples by counting field changes from previous sample.
    /// </summary>
    public void RecordSample(JsonElement element, long sampleIndex)
    {
        string? rawJson = null;

        // Check if this is a fixed-position sample
        foreach (var kvp in _fixedSamples)
        {
            if (kvp.Value.targetIndex == sampleIndex && kvp.Value.json == null)
            {
                rawJson ??= element.GetRawText();
                _fixedSamples[kvp.Key] = (kvp.Value.targetIndex, rawJson);
            }
        }

        // Always capture the first sample
        if (sampleIndex == 0)
        {
            rawJson ??= element.GetRawText();
            if (!_fixedSamples.ContainsKey("first") || _fixedSamples["first"].json == null)
                _fixedSamples["first"] = (0, rawJson);
        }

        // Score this sample vs previous by counting changed fields
        var currentHashes = new Dictionary<string, int>();
        CollectHashes(element, "", currentHashes);

        if (_previousHashes.Count > 0)
        {
            int changeScore = 0;
            foreach (var (path, hash) in currentHashes)
            {
                if (_previousHashes.TryGetValue(path, out int prevHash) && prevHash != hash)
                    changeScore++;
            }

            // Also count fields that appeared or disappeared
            changeScore += currentHashes.Keys.Except(_previousHashes.Keys).Count();
            changeScore += _previousHashes.Keys.Except(currentHashes.Keys).Count();

            if (changeScore > 0)
            {
                rawJson ??= element.GetRawText();

                // Use a unique key to avoid collisions in the sorted list
                int uniqueKey = changeScore * 1_000_000 + (int)(sampleIndex % 1_000_000);

                if (_interestingSamples.Count < _maxInterestingSamples)
                {
                    _interestingSamples[uniqueKey] = (sampleIndex, rawJson);
                }
                else if (_interestingSamples.Count > 0)
                {
                    // Replace the least-interesting sample if this one scores higher
                    var lastKey = _interestingSamples.Keys[^1];
                    if (uniqueKey > lastKey)
                    {
                        _interestingSamples.Remove(lastKey);
                        _interestingSamples[uniqueKey] = (sampleIndex, rawJson);
                    }
                }
            }
        }

        _previousHashes = currentHashes;

        // Always keep the latest sample for "last"
        if (TotalSamples == 0)
        {
            // If total unknown, always keep updating "last"
            rawJson ??= element.GetRawText();
            _fixedSamples["last"] = (sampleIndex, rawJson);
        }
    }

    /// <summary>
    /// Write all extracted samples to the output directory as formatted JSON files.
    /// </summary>
    public async Task WriteSamplesAsync(string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        var options = new JsonSerializerOptions { WriteIndented = true };
        int fileIndex = 0;

        // Write fixed-position samples
        foreach (var (label, (idx, json)) in _fixedSamples.OrderBy(kv => kv.Value.targetIndex))
        {
            if (json == null) continue;

            string fileName = $"sample_{fileIndex:D3}_{label}_idx{idx}.json";
            string filePath = Path.Combine(outputDir, fileName);

            // Parse and re-serialize to get pretty-printed output
            using var doc = JsonDocument.Parse(json);
            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, doc.RootElement, options);

            Console.WriteLine($"  Wrote {fileName} (sample #{idx:N0})");
            fileIndex++;
        }

        // Write interesting samples
        foreach (var (score, (idx, json)) in _interestingSamples)
        {
            int changeCount = score / 1_000_000;
            string fileName = $"sample_{fileIndex:D3}_interesting_idx{idx}_changes{changeCount}.json";
            string filePath = Path.Combine(outputDir, fileName);

            using var doc = JsonDocument.Parse(json);
            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, doc.RootElement, options);

            Console.WriteLine($"  Wrote {fileName} ({changeCount} field changes, sample #{idx:N0})");
            fileIndex++;
        }

        Console.WriteLine($"  Total: {fileIndex} sample files written to {outputDir}");
    }

    private static void CollectHashes(JsonElement element, string path, Dictionary<string, int> hashes)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    string childPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";
                    CollectHashes(prop.Value, childPath, hashes);
                }
                break;

            case JsonValueKind.Array:
                int len = element.GetArrayLength();
                // Hash array contents for small/medium arrays, just length for very large ones.
                // LMU telemetry uses 128-element Vehicles arrays, so threshold must be >= 128.
                if (len <= 256)
                {
                    int idx = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        CollectHashes(item, $"{path}[{idx}]", hashes);
                        idx++;
                    }
                }
                else
                {
                    hashes[path + ".length"] = len.GetHashCode();
                }
                break;

            default:
                hashes[path] = element.GetRawText().GetHashCode(StringComparison.Ordinal);
                break;
        }
    }
}
