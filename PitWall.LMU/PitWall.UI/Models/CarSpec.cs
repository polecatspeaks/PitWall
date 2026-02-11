using System;
using System.Text.Json.Serialization;

namespace PitWall.UI.Models
{
    public sealed class CarSpec
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Engine { get; set; } = string.Empty;
        public string Transmission { get; set; } = string.Empty;
        public string Power { get; set; } = string.Empty;
        public int? PowerBhp { get; set; }
        public int? WeightKg { get; set; }
        public int? LengthMm { get; set; }
        public int? WidthMm { get; set; }
        public int? HeightMm { get; set; }
        public string SourceUrl { get; set; } = string.Empty;
    }
}
