using System;
using System.Linq;

namespace PitWall.Agent.Services.RulesEngine
{
    public class QueryPatterns
    {
        private static readonly string[] FuelKeywords =
            { "fuel", "gas", "laps remaining", "how much fuel" };

        private static readonly string[] PitKeywords =
            { "pit", "box", "stop", "come in", "should i pit" };

        private static readonly string[] TireKeywords =
            { "tire", "tyre", "rubber", "grip", "wear" };

        private static readonly string[] GapKeywords =
            { "gap", "position", "where am i", "ahead", "behind" };

        private static readonly string[] WeatherKeywords =
            { "weather", "rain", "wet", "dry", "track temp" };

        private static readonly string[] PaceKeywords =
            { "pace", "lap time", "delta", "fast", "slow" };

        public bool IsFuelQuery(string query) => ContainsAny(query, FuelKeywords);
        public bool IsPitQuery(string query) => ContainsAny(query, PitKeywords);
        public bool IsTireQuery(string query) => ContainsAny(query, TireKeywords);
        public bool IsGapQuery(string query) => ContainsAny(query, GapKeywords);
        public bool IsWeatherQuery(string query) => ContainsAny(query, WeatherKeywords);
        public bool IsPaceQuery(string query) => ContainsAny(query, PaceKeywords);

        private static bool ContainsAny(string query, string[] keywords)
        {
            return keywords.Any(k => query.Contains(k, StringComparison.OrdinalIgnoreCase));
        }
    }
}
