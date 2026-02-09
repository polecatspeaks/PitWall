using System;

namespace PitWall.UI.Models
{
    public class AiMessage
    {
        public string Role { get; set; } = "Assistant";
        public string Text { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
