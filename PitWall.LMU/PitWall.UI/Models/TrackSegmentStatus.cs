namespace PitWall.UI.Models
{
    public sealed class TrackSegmentStatus
    {
        public string TrackName { get; init; } = "Default";
        public string SectorName { get; init; } = "--";
        public string CornerLabel { get; init; } = "--";
        public string SegmentType { get; init; } = "Straight";
        public string Direction { get; init; } = "";
        public string Severity { get; init; } = "";
        public double LapFraction { get; init; }
    }
}
