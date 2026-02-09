using System.Collections.Generic;

namespace PitWall.Agent.Models
{
    public enum QueryIntent
    {
        Unknown,
        Fuel,
        Pit,
        Tires,
        Gap,
        Weather,
        Pace,
        Strategy,
        Complex
    }

    public class ParsedQuery
    {
        public QueryIntent Intent { get; set; }
        public string OriginalQuery { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}
