using System;

namespace PitWall.UI.Models
{
    public class SessionSummaryDto
    {
        public int SessionId { get; set; }
        public DateTimeOffset? StartTimeUtc { get; set; }
        public DateTimeOffset? EndTimeUtc { get; set; }
        public string Track { get; set; } = "Unknown";
        public string Car { get; set; } = "Unknown";

        public string DisplayName
        {
            get
            {
                var dateLabel = StartTimeUtc.HasValue
                    ? StartTimeUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                    : "Unknown date";
                return $"{SessionId}: {dateLabel} | {Track} | {Car}";
            }
        }
    }
}
