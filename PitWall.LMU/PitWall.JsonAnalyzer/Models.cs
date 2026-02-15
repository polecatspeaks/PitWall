using System.Text.Json;

namespace PitWall.JsonAnalyzer;

/// <summary>
/// Represents a node in the discovered JSON schema tree.
/// Tracks type information, sample values, numeric ranges, and occurrence counts.
/// </summary>
public class SchemaNode
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// All JSON value kinds observed for this node across all samples.
    /// Most nodes will have exactly one kind, but some may vary (e.g., null vs string).
    /// </summary>
    public HashSet<JsonValueKind> ObservedKinds { get; } = [];

    /// <summary>First N sample values seen (as raw JSON strings).</summary>
    public List<string> SampleValues { get; } = [];
    public const int MaxSampleValues = 5;

    /// <summary>For numeric fields: tracked min/max.</summary>
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }

    /// <summary>Number of samples where this field was present.</summary>
    public long OccurrenceCount { get; set; }

    /// <summary>Distinct values seen (capped to prevent memory bloat).</summary>
    public HashSet<string> DistinctValues { get; } = [];
    public const int MaxDistinctValues = 100;
    public bool DistinctValuesCapped { get; set; }

    /// <summary>Child nodes for object types. Key = property name.</summary>
    public Dictionary<string, SchemaNode> Children { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// For arrays: the merged schema of array elements.
    /// Null if this node is not an array or the array was always empty.
    /// </summary>
    public SchemaNode? ArrayElementSchema { get; set; }

    /// <summary>Min/max array lengths observed.</summary>
    public int? MinArrayLength { get; set; }
    public int? MaxArrayLength { get; set; }

    /// <summary>Maximum nesting depth from this node down.</summary>
    public int MaxDepth => Children.Count == 0 && ArrayElementSchema == null
        ? 0
        : 1 + Math.Max(
            Children.Values.Any() ? Children.Values.Max(c => c.MaxDepth) : 0,
            ArrayElementSchema?.MaxDepth ?? 0);

    /// <summary>Sample index where this node was first observed.</summary>
    public long FirstSeenAtSample { get; set; }

    public void AddSampleValue(string value)
    {
        if (SampleValues.Count < MaxSampleValues)
            SampleValues.Add(value);

        if (!DistinctValuesCapped)
        {
            DistinctValues.Add(value);
            if (DistinctValues.Count > MaxDistinctValues)
                DistinctValuesCapped = true;
        }
    }

    public void TrackNumericValue(double value)
    {
        MinValue = MinValue.HasValue ? Math.Min(MinValue.Value, value) : value;
        MaxValue = MaxValue.HasValue ? Math.Max(MaxValue.Value, value) : value;
    }

    public void TrackArrayLength(int length)
    {
        MinArrayLength = MinArrayLength.HasValue ? Math.Min(MinArrayLength.Value, length) : length;
        MaxArrayLength = MaxArrayLength.HasValue ? Math.Max(MaxArrayLength.Value, length) : length;
    }

    /// <summary>Primary JSON type as a human-readable string.</summary>
    public string TypeDescription
    {
        get
        {
            if (ObservedKinds.Count == 0) return "unknown";
            if (ObservedKinds.Count == 1) return KindToString(ObservedKinds.First());

            // Filter out Null to get the "real" type, then note nullable
            var nonNull = ObservedKinds.Where(k => k != JsonValueKind.Null).ToList();
            if (nonNull.Count == 1 && ObservedKinds.Contains(JsonValueKind.Null))
                return $"{KindToString(nonNull[0])}?";

            return string.Join("|", ObservedKinds.Select(KindToString));
        }
    }

    private static string KindToString(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Null => "null",
        _ => "undefined"
    };
}

/// <summary>
/// Tracks how a leaf field's value changes over time across samples.
/// </summary>
public class FieldChangeInfo
{
    public string Path { get; set; } = string.Empty;

    /// <summary>Hash of the first value seen.</summary>
    public int FirstValueHash { get; set; }
    public string? FirstValue { get; set; }
    public string? LastValue { get; set; }

    /// <summary>Number of times the value changed between consecutive samples.</summary>
    public long ChangeCount { get; set; }

    /// <summary>Total samples where this field was observed.</summary>
    public long TotalObservations { get; set; }

    /// <summary>Sample indices where changes were detected (first N).</summary>
    public List<long> ChangeIndices { get; } = [];
    public const int MaxChangeIndices = 20;

    /// <summary>Largest single-step change for numeric fields.</summary>
    public double? MaxDelta { get; set; }

    /// <summary>Previous value hash for change detection.</summary>
    public int PreviousValueHash { get; set; }

    /// <summary>Previous numeric value for delta tracking.</summary>
    public double? PreviousNumericValue { get; set; }

    public UpdateFrequency Frequency
    {
        get
        {
            if (TotalObservations == 0) return UpdateFrequency.Unknown;
            if (ChangeCount == 0) return UpdateFrequency.Static;

            double changeRate = (double)ChangeCount / TotalObservations;
            return changeRate switch
            {
                > 0.8 => UpdateFrequency.PerSample,
                > 0.1 => UpdateFrequency.Frequent,
                > 0.01 => UpdateFrequency.PerLap,
                _ => UpdateFrequency.Sparse
            };
        }
    }
}

public enum UpdateFrequency
{
    Unknown,
    Static,
    Sparse,
    PerLap,
    Frequent,
    PerSample
}

public enum FieldCategory
{
    Uncategorized,
    Damage,
    Flags,
    Telemetry,
    Timing,
    Session,
    Position,
    Weather,
    MultiCar,
    Controls,
    Engine,
    Tyres,
    Aero,
    Electronics
}

/// <summary>
/// Aggregated change info for a wildcard path (e.g., "Vehicles[*].Position.X").
/// Groups all per-index entries into a single summary.
/// </summary>
public class AggregatedFieldInfo
{
    /// <summary>Path with array indices replaced by [*].</summary>
    public string WildcardPath { get; set; } = string.Empty;

    /// <summary>How many concrete array indices were observed.</summary>
    public int IndexCount { get; set; }

    /// <summary>How many indices had at least one change.</summary>
    public int ActiveCount { get; set; }

    /// <summary>Total changes across all indices.</summary>
    public long TotalChanges { get; set; }

    /// <summary>Average changes per index.</summary>
    public double AvgChanges { get; set; }

    /// <summary>Maximum changes seen at any single index.</summary>
    public long MaxChanges { get; set; }

    /// <summary>Largest single-step delta across all indices.</summary>
    public double MaxDelta { get; set; }

    /// <summary>Update frequency of the most active index.</summary>
    public UpdateFrequency RepresentativeFrequency { get; set; }

    /// <summary>First N sample indices where any change occurred.</summary>
    public List<long> FirstChangeIndices { get; set; } = [];

    /// <summary>For static fields: the representative first value.</summary>
    public string? FirstValue { get; set; }

    /// <summary>Whether this path involves array indexing (multi-car etc.).</summary>
    public bool IsArrayPath => WildcardPath.Contains("[*]");
}
