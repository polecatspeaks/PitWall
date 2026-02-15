using System.Text.Json;
using System.Text.RegularExpressions;

namespace PitWall.JsonAnalyzer;

/// <summary>
/// Tracks value changes across consecutive samples for each leaf field.
/// Classifies fields as Static, PerLap, PerSample, Sparse, or Frequent.
/// Detects anomalies (large single-step changes) that may indicate damage events.
/// </summary>
public sealed class ChangeDetector
{
    private readonly Dictionary<string, FieldChangeInfo> _fieldChanges = new(StringComparer.Ordinal);

    /// <summary>Regex to replace array indices [0], [12], etc. with [*] for aggregation.</summary>
    private static readonly Regex ArrayIndexRegex = new(@"\[\d+\]", RegexOptions.Compiled);

    public IReadOnlyDictionary<string, FieldChangeInfo> FieldChanges => _fieldChanges;

    /// <summary>
    /// Record all leaf values from a sample, comparing against previous values.
    /// </summary>
    public void RecordSample(JsonElement element, long sampleIndex)
    {
        RecordElement(element, "", sampleIndex);
    }

    private void RecordElement(JsonElement element, string path, long sampleIndex)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    string childPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";
                    RecordElement(prop.Value, childPath, sampleIndex);
                }
                break;

            case JsonValueKind.Array:
                // For arrays, track changes at the element level for small/medium arrays
                // and at the array level for very large ones.
                // LMU telemetry uses 128-element Vehicles arrays, so threshold must be >= 128.
                int len = element.GetArrayLength();
                if (len <= 256)
                {
                    int idx = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        RecordElement(item, $"{path}[{idx}]", sampleIndex);
                        idx++;
                    }
                }
                else
                {
                    // Just track array length changes for very large arrays
                    RecordLeafValue(path + ".length", len.ToString(), len, sampleIndex);
                }
                break;

            case JsonValueKind.Number:
                string numStr = element.GetRawText();
                double? numVal = element.TryGetDouble(out double d) ? d : null;
                RecordLeafValue(path, numStr, numVal, sampleIndex);
                break;

            case JsonValueKind.String:
                RecordLeafValue(path, element.GetString() ?? "", null, sampleIndex);
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                RecordLeafValue(path, element.GetBoolean().ToString(), null, sampleIndex);
                break;

            case JsonValueKind.Null:
                RecordLeafValue(path, "null", null, sampleIndex);
                break;
        }
    }

    private void RecordLeafValue(string path, string value, double? numericValue, long sampleIndex)
    {
        int hash = value.GetHashCode(StringComparison.Ordinal);

        if (!_fieldChanges.TryGetValue(path, out var info))
        {
            info = new FieldChangeInfo
            {
                Path = path,
                FirstValueHash = hash,
                FirstValue = value.Length > 100 ? value[..100] : value,
                LastValue = value.Length > 100 ? value[..100] : value,
                PreviousValueHash = hash,
                PreviousNumericValue = numericValue
            };
            _fieldChanges[path] = info;
        }

        info.TotalObservations++;

        if (hash != info.PreviousValueHash)
        {
            info.ChangeCount++;

            if (info.ChangeIndices.Count < FieldChangeInfo.MaxChangeIndices)
                info.ChangeIndices.Add(sampleIndex);

            // Track numeric deltas for anomaly detection
            if (numericValue.HasValue && info.PreviousNumericValue.HasValue)
            {
                double delta = Math.Abs(numericValue.Value - info.PreviousNumericValue.Value);
                info.MaxDelta = info.MaxDelta.HasValue ? Math.Max(info.MaxDelta.Value, delta) : delta;
            }

            info.LastValue = value.Length > 100 ? value[..100] : value;
        }

        info.PreviousValueHash = hash;
        info.PreviousNumericValue = numericValue;
    }

    /// <summary>
    /// Get fields sorted by change count descending (most dynamic first).
    /// </summary>
    public List<FieldChangeInfo> GetDynamicFields(int maxCount = 100)
    {
        return _fieldChanges.Values
            .Where(f => f.ChangeCount > 0)
            .OrderByDescending(f => f.ChangeCount)
            .Take(maxCount)
            .ToList();
    }

    /// <summary>
    /// Get fields that never changed (static/constant).
    /// </summary>
    public List<FieldChangeInfo> GetStaticFields()
    {
        return _fieldChanges.Values
            .Where(f => f.ChangeCount == 0)
            .OrderBy(f => f.Path)
            .ToList();
    }

    /// <summary>
    /// Get fields with large single-step changes (anomalies, possible damage events).
    /// </summary>
    public List<FieldChangeInfo> GetAnomalies(double minDelta = 10.0)
    {
        return _fieldChanges.Values
            .Where(f => f.MaxDelta.HasValue && f.MaxDelta.Value >= minDelta)
            .OrderByDescending(f => f.MaxDelta)
            .ToList();
    }

    /// <summary>
    /// Convert a per-index path like "Vehicles[3].Position.X" to wildcard "Vehicles[*].Position.X".
    /// </summary>
    public static string WildcardPath(string path) => ArrayIndexRegex.Replace(path, "[*]");

    /// <summary>
    /// Get dynamic fields aggregated by wildcard path.
    /// Groups "Vehicles[0].Position.X" ... "Vehicles[127].Position.X" into a single entry 
    /// with aggregated statistics (sum of changes, max delta, count of active indices).
    /// </summary>
    public List<AggregatedFieldInfo> GetAggregatedDynamicFields(int maxCount = 200)
    {
        return _fieldChanges.Values
            .Where(f => f.ChangeCount > 0)
            .GroupBy(f => WildcardPath(f.Path))
            .Select(g => new AggregatedFieldInfo
            {
                WildcardPath = g.Key,
                IndexCount = g.Count(),
                ActiveCount = g.Count(f => f.ChangeCount > 0),
                TotalChanges = g.Sum(f => (long)f.ChangeCount),
                AvgChanges = g.Average(f => f.ChangeCount),
                MaxChanges = g.Max(f => f.ChangeCount),
                MaxDelta = g.Where(f => f.MaxDelta.HasValue).Select(f => f.MaxDelta!.Value).DefaultIfEmpty(0).Max(),
                RepresentativeFrequency = g.OrderByDescending(f => f.ChangeCount).First().Frequency,
                FirstChangeIndices = g.SelectMany(f => f.ChangeIndices).Distinct().OrderBy(x => x).Take(5).ToList()
            })
            .OrderByDescending(a => a.TotalChanges)
            .Take(maxCount)
            .ToList();
    }

    /// <summary>
    /// Get static fields aggregated by wildcard path.
    /// </summary>
    public List<AggregatedFieldInfo> GetAggregatedStaticFields()
    {
        return _fieldChanges.Values
            .Where(f => f.ChangeCount == 0)
            .GroupBy(f => WildcardPath(f.Path))
            .Select(g => new AggregatedFieldInfo
            {
                WildcardPath = g.Key,
                IndexCount = g.Count(),
                ActiveCount = 0,
                TotalChanges = 0,
                AvgChanges = 0,
                MaxChanges = 0,
                RepresentativeFrequency = UpdateFrequency.Static,
                FirstValue = g.First().FirstValue
            })
            .OrderBy(a => a.WildcardPath)
            .ToList();
    }

    /// <summary>
    /// Get anomalies aggregated by wildcard path.
    /// </summary>
    public List<AggregatedFieldInfo> GetAggregatedAnomalies(double minDelta = 10.0)
    {
        return _fieldChanges.Values
            .Where(f => f.MaxDelta.HasValue && f.MaxDelta.Value >= minDelta)
            .GroupBy(f => WildcardPath(f.Path))
            .Select(g => new AggregatedFieldInfo
            {
                WildcardPath = g.Key,
                IndexCount = g.Count(),
                ActiveCount = g.Count(f => f.ChangeCount > 0),
                TotalChanges = g.Sum(f => (long)f.ChangeCount),
                AvgChanges = g.Average(f => f.ChangeCount),
                MaxChanges = g.Max(f => f.ChangeCount),
                MaxDelta = g.Where(f => f.MaxDelta.HasValue).Select(f => f.MaxDelta!.Value).DefaultIfEmpty(0).Max(),
                RepresentativeFrequency = g.OrderByDescending(f => f.ChangeCount).First().Frequency,
                FirstChangeIndices = g.SelectMany(f => f.ChangeIndices).Distinct().OrderBy(x => x).Take(5).ToList()
            })
            .OrderByDescending(a => a.MaxDelta)
            .ToList();
    }
}
