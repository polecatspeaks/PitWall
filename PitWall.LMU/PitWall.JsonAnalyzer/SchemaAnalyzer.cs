using System.Text.Json;

namespace PitWall.JsonAnalyzer;

/// <summary>
/// Recursively walks JsonElement trees to build a merged schema.
/// Handles objects, arrays, and all primitive types.
/// </summary>
public sealed class SchemaAnalyzer
{
    private readonly SchemaNode _root = new() { Name = "(root)", Path = "" };
    private long _currentSampleIndex;

    public SchemaNode Root => _root;

    /// <summary>
    /// Merge a sample element into the accumulated schema tree.
    /// </summary>
    public void AnalyzeSample(JsonElement element, long sampleIndex)
    {
        _currentSampleIndex = sampleIndex;
        WalkElement(element, _root, "");
    }

    /// <summary>
    /// Analyze the session metadata object separately.
    /// Stored under a "session" child of root.
    /// </summary>
    public void AnalyzeSessionMetadata(JsonElement sessionElement)
    {
        var sessionNode = GetOrCreateChild(_root, "session", "session");
        WalkElement(sessionElement, sessionNode, "session");
    }

    private void WalkElement(JsonElement element, SchemaNode node, string path)
    {
        node.ObservedKinds.Add(element.ValueKind);
        node.OccurrenceCount++;

        if (node.OccurrenceCount == 1)
            node.FirstSeenAtSample = _currentSampleIndex;

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    string childPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";
                    var childNode = GetOrCreateChild(node, prop.Name, childPath);
                    WalkElement(prop.Value, childNode, childPath);
                }
                break;

            case JsonValueKind.Array:
                int length = element.GetArrayLength();
                node.TrackArrayLength(length);

                if (length > 0)
                {
                    string elementPath = $"{path}[*]";
                    node.ArrayElementSchema ??= new SchemaNode { Name = "[*]", Path = elementPath };

                    // Analyze each element to capture full schema variety
                    // For large arrays (e.g., 20+ vehicles), analyze all to catch differences
                    int maxToAnalyze = Math.Min(length, 50);
                    int idx = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        if (idx >= maxToAnalyze) break;
                        WalkElement(item, node.ArrayElementSchema, elementPath);
                        idx++;
                    }
                }

                // Also store a sample of the raw array
                if (length <= 10)
                    node.AddSampleValue(element.GetRawText());
                else
                    node.AddSampleValue($"[array of {length} elements]");
                break;

            case JsonValueKind.Number:
                if (element.TryGetDouble(out double dval))
                {
                    node.TrackNumericValue(dval);
                    node.AddSampleValue(element.GetRawText());
                }
                break;

            case JsonValueKind.String:
                string sval = element.GetString() ?? "";
                // Truncate very long strings for sample storage
                node.AddSampleValue(sval.Length > 100 ? sval[..100] + "..." : sval);
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                node.AddSampleValue(element.GetBoolean().ToString().ToLowerInvariant());
                break;

            case JsonValueKind.Null:
                node.AddSampleValue("null");
                break;
        }
    }

    private static SchemaNode GetOrCreateChild(SchemaNode parent, string name, string fullPath)
    {
        if (!parent.Children.TryGetValue(name, out var child))
        {
            child = new SchemaNode { Name = name, Path = fullPath };
            parent.Children[name] = child;
        }
        return child;
    }

    /// <summary>
    /// Counts all unique leaf field paths in the schema tree.
    /// </summary>
    public int CountLeafFields() => CountLeaves(_root);

    private static int CountLeaves(SchemaNode node)
    {
        if (node.Children.Count == 0 && node.ArrayElementSchema == null)
            return 1;

        int count = 0;
        foreach (var child in node.Children.Values)
            count += CountLeaves(child);

        if (node.ArrayElementSchema != null)
            count += CountLeaves(node.ArrayElementSchema);

        return count;
    }

    /// <summary>
    /// Collects all leaf paths in the schema tree.
    /// </summary>
    public List<string> GetAllLeafPaths()
    {
        var paths = new List<string>();
        CollectLeafPaths(_root, paths);
        return paths;
    }

    private static void CollectLeafPaths(SchemaNode node, List<string> paths)
    {
        bool isLeaf = node.Children.Count == 0 && node.ArrayElementSchema == null;

        if (isLeaf && !string.IsNullOrEmpty(node.Path))
        {
            paths.Add(node.Path);
            return;
        }

        foreach (var child in node.Children.Values)
            CollectLeafPaths(child, paths);

        if (node.ArrayElementSchema != null)
            CollectLeafPaths(node.ArrayElementSchema, paths);
    }
}
