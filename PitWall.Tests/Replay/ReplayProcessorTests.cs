using System;
using System.Collections.Generic;
using System.Linq;
using PitWall.Replay;
using Xunit;

namespace PitWall.Tests.Replay
{
    public class ReplayProcessorTests
    {
        [Fact]
        public void ScanReplayFolder_NonExistentFolder_ThrowsDirectoryNotFoundException()
        {
            var processor = new ReplayProcessor(null);
            var nonExistentPath = "C:\\NonExistent\\Folder\\Path";

            Assert.Throws<System.IO.DirectoryNotFoundException>(() =>
                processor.ScanReplayFolder(nonExistentPath));
        }

        [Fact]
        public void ChronologicalSorting_OrdersOldestToNewest()
        {
            // Simulate discovering replays in random order
            var unsortedReplays = new List<ReplayFileInfo>
            {
                new ReplayFileInfo { FilePath = "replay3.rpy", SessionDate = new DateTime(2025, 11, 16) },
                new ReplayFileInfo { FilePath = "replay1.rpy", SessionDate = new DateTime(2025, 11, 8) },
                new ReplayFileInfo { FilePath = "replay2.rpy", SessionDate = new DateTime(2025, 11, 14) }
            };

            // Sort chronologically (oldest first)
            var sorted = unsortedReplays.OrderBy(r => r.SessionDate).ToList();

            Assert.Equal(new DateTime(2025, 11, 8), sorted[0].SessionDate);
            Assert.Equal(new DateTime(2025, 11, 14), sorted[1].SessionDate);
            Assert.Equal(new DateTime(2025, 11, 16), sorted[2].SessionDate);
        }

        [Fact]
        public void ChronologicalSorting_MaintainsTemporalOrder()
        {
            var replays = new List<ReplayFileInfo>
            {
                new ReplayFileInfo { SessionDate = new DateTime(2025, 12, 1) },
                new ReplayFileInfo { SessionDate = new DateTime(2025, 11, 30) },
                new ReplayFileInfo { SessionDate = new DateTime(2025, 11, 29) },
                new ReplayFileInfo { SessionDate = new DateTime(2025, 12, 2) }
            };

            var sorted = replays.OrderBy(r => r.SessionDate).ToList();

            // Verify oldest → newest order
            for (int i = 1; i < sorted.Count; i++)
            {
                Assert.True(sorted[i].SessionDate >= sorted[i - 1].SessionDate);
            }
        }

        [Fact]
        public void ReplayFileInfo_StoresRequiredMetadata()
        {
            var replay = new ReplayFileInfo
            {
                FilePath = "C:\\replays\\2025_11_08_09_58_17.rpy",
                SessionDate = new DateTime(2025, 11, 8, 9, 58, 17),
                FileSize = 1024 * 1024 * 50 // 50 MB
            };

            Assert.Equal("C:\\replays\\2025_11_08_09_58_17.rpy", replay.FilePath);
            Assert.Equal(new DateTime(2025, 11, 8, 9, 58, 17), replay.SessionDate);
            Assert.Equal(1024 * 1024 * 50, replay.FileSize);
        }
    }
}
