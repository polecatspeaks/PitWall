using System.Collections.Generic;

namespace PitWall.Core.Models
{
    public record ChannelInfo(string Name, IReadOnlyList<string> ColumnNames)
    {
        public int ColumnCount => ColumnNames.Count;
    }
}
