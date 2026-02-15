using System.Text.Json.Serialization;
using rF2SharedMemoryNet.LMUData.Models;
using rF2SharedMemoryNet.RF2Data.Structs;

namespace LMUMemoryReader;

[JsonSourceGenerationOptions(IncludeFields = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(SessionMetadata))]
[JsonSerializable(typeof(TelemetryLogEntry))]
[JsonSerializable(typeof(Telemetry))]
[JsonSerializable(typeof(Scoring))]
[JsonSerializable(typeof(ScoringInfo))]
[JsonSerializable(typeof(VehicleTelemetry))]
[JsonSerializable(typeof(VehicleScoring))]
[JsonSerializable(typeof(Electronics))]
[JsonSerializable(typeof(Vec3))]
[JsonSerializable(typeof(Wheel))]
public partial class TelemetryJsonContext : JsonSerializerContext
{
}
