namespace PitWall.Models
{
    /// <summary>
    /// Traffic classification based on relative pace
    /// </summary>
    public enum TrafficClass
    {
        FasterClass,    // >2s faster per lap
        SameClass,      // Within 2s per lap
        SlowerClass     // >2s slower per lap
    }
}
