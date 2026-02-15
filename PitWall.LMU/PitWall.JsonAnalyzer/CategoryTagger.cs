using System.Text.RegularExpressions;

namespace PitWall.JsonAnalyzer;

/// <summary>
/// Categorizes field paths into racing-relevant data categories using pattern matching.
/// Patterns are intentionally broad — better to over-match and let the user filter.
/// </summary>
public static class CategoryTagger
{
    private static readonly (FieldCategory Category, Regex Pattern)[] Rules =
    [
        // Damage — most important for STARwall design
        (FieldCategory.Damage, new Regex(
            @"damage|dent|detach|broken|flat|impact|collision|lastimpact|overheating|wheelsdetached",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Flags — yellow, blue, sector flags, safety car, penalties
        (FieldCategory.Flags, new Regex(
            @"flag|yellow|blue|caution|safety|penalty|sector\d*flag|fullcourse|local.*yellow|vsc",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Timing / Lap data
        (FieldCategory.Timing, new Regex(
            @"laptime|lap.*time|sector\d|best.*time|last.*time|current.*time|behind|ahead|gap|interval|position\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Tyres
        (FieldCategory.Tyres, new Regex(
            @"tyre|tire|compound|carcass|rubber|rim.*temp|tread|wear|wheel.*speed|surface.*type",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Engine / Drivetrain
        (FieldCategory.Engine, new Regex(
            @"engine|rpm|oil.*temp|water.*temp|turbo|boost|clutch|exhaust|fuel.*mix|regen|soc|virtual.*energy",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Aero / Suspension
        (FieldCategory.Aero, new Regex(
            @"aero|wing|flap|ride.*height|suspension|susp|spring|damper|deflection|downforce|drag",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Electronics
        (FieldCategory.Electronics, new Regex(
            @"tc\b|traction|abs\b|anti.*stall|launch.*control|speed.*limiter|ers|drs|ffb|brake.*bias|brake.*migration",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Controls / Inputs
        (FieldCategory.Controls, new Regex(
            @"throttle|brake.*pos|steering|gear\b|pedal|torque|force.*feedback",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Position / GPS
        (FieldCategory.Position, new Regex(
            @"gps|latitude|longitude|position|dist|path.*lateral|track.*edge|coord|location|heading",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Telemetry (general)
        (FieldCategory.Telemetry, new Regex(
            @"speed|fuel|temp|g.*force|accel|velocity|pressure",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Weather / Environment
        (FieldCategory.Weather, new Regex(
            @"weather|ambient|track.*temp|cloud|wetness|rain|wind|humidity",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),

        // Session metadata
        (FieldCategory.Session, new Regex(
            @"session|track.*name|car.*name|driver|class|layout|recording|event|num.*vehicle",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    ];

    /// <summary>
    /// Classify a field path into a category.
    /// Returns the first matching category, or Uncategorized if no pattern matches.
    /// </summary>
    public static FieldCategory Classify(string path)
    {
        foreach (var (category, pattern) in Rules)
        {
            if (pattern.IsMatch(path))
                return category;
        }

        return FieldCategory.Uncategorized;
    }

    /// <summary>
    /// Classify all leaf paths in a schema, returning path → category mapping.
    /// </summary>
    public static Dictionary<string, FieldCategory> ClassifyAll(IEnumerable<string> paths)
    {
        var result = new Dictionary<string, FieldCategory>(StringComparer.Ordinal);
        foreach (var path in paths)
            result[path] = Classify(path);
        return result;
    }

    /// <summary>
    /// Detect if a path is under a vehicle/car array (multi-car data).
    /// </summary>
    public static bool IsMultiCarPath(string path)
    {
        return Regex.IsMatch(path,
            @"vehicle|veh\[|car\[|driver\[|scoring.*\[\*\]|participant",
            RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Human-readable category name for reports.
    /// </summary>
    public static string CategoryDisplayName(FieldCategory category) => category switch
    {
        FieldCategory.Damage => "Damage / Impact",
        FieldCategory.Flags => "Flags / Penalties",
        FieldCategory.Telemetry => "General Telemetry",
        FieldCategory.Timing => "Timing / Lap Data",
        FieldCategory.Session => "Session / Metadata",
        FieldCategory.Position => "Position / GPS",
        FieldCategory.Weather => "Weather / Environment",
        FieldCategory.MultiCar => "Multi-Car Data",
        FieldCategory.Controls => "Driver Controls",
        FieldCategory.Engine => "Engine / Drivetrain",
        FieldCategory.Tyres => "Tyres / Wheels",
        FieldCategory.Aero => "Aero / Suspension",
        FieldCategory.Electronics => "Electronics / Assists",
        FieldCategory.Uncategorized => "Uncategorized",
        _ => category.ToString()
    };
}
