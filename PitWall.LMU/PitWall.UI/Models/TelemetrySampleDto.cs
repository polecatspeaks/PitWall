using System;
using System.Text.Json.Serialization;

namespace PitWall.UI.Models
{
    public class TelemetrySampleDto
    {
        public DateTime? Timestamp { get; set; }
        public double SpeedKph { get; set; }
        public double Throttle { get; set; }
        public double Brake { get; set; }
        public double Steering { get; set; }
        [JsonPropertyName("tyreTemps")]
        public double[] TyreTempsC { get; set; } = Array.Empty<double>();
        public double FuelLiters { get; set; }

        [JsonPropertyName("currentLap")]
        public int? CurrentLap { get; set; }

        [JsonPropertyName("totalLaps")]
        public int? TotalLaps { get; set; }

        [JsonPropertyName("lastLapTime")]
        public double? LastLapTime { get; set; }

        [JsonPropertyName("bestLapTime")]
        public double? BestLapTime { get; set; }
    }
}
