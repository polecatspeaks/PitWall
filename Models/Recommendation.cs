namespace PitWall.Models
{
    /// <summary>
    /// Strategy recommendation from the race engineer
    /// </summary>
    public class Recommendation
    {
        public bool ShouldPit { get; set; }
        public string Message { get; set; } = string.Empty;
        public RecommendationType Type { get; set; }
        public Priority Priority { get; set; }
    }

    public enum RecommendationType
    {
        None,
        Fuel,
        Tyres,
        Damage,
        Traffic,
        Weather
    }

    public enum Priority
    {
        Info = 0,
        Warning = 1,
        Critical = 2
    }
}
