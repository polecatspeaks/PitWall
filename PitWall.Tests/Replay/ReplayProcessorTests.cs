using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PitWall.Replay;
using PitWall.Storage;
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

        [Fact]
        public async Task ProcessReplayLibrary_Skips_Replays_Under_Ten_Minutes()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempRoot);

            string replayFolder = Path.Combine(tempRoot, "replays");
            Directory.CreateDirectory(replayFolder);

            // Short replay (5 minutes = 300 seconds) should be skipped
            string shortReplay = Path.Combine(replayFolder, "2025_01_01_00_00_00.rpy");
            File.WriteAllLines(shortReplay, new[]
            {
                "track_name: TestTrack",
                "car_name: TestCar",
                "session_type: Race",
                "session_length: 300",
                "session_start_time: 2025-01-01T00:00:00Z",
                "---",
                "binarydata"
            });

            // Long replay (20 minutes = 1200 seconds) should be processed
            string longReplay = Path.Combine(replayFolder, "2025_01_02_00_00_00.rpy");
            File.WriteAllLines(longReplay, new[]
            {
                "track_name: TestTrack",
                "car_name: TestCar",
                "session_type: Race",
                "session_length: 1200",
                "session_start_time: 2025-01-02T00:00:00Z",
                "---",
                "binarydata"
            });

            var database = new SQLiteProfileDatabase(tempRoot);
            var processor = new ReplayProcessor(database);

            ReplayProcessingCompleteEventArgs? completed = null;
            processor.ProcessingComplete += (_, e) => completed = e;

            try
            {
                await processor.ProcessReplayLibraryAsync(replayFolder, "TestDriver");

                var timeSeries = await database.GetTimeSeries("TestDriver", "TestTrack", "TestCar");

                Assert.NotNull(completed);
                Assert.Equal(1, completed!.ReplaysProcessed);
                Assert.Equal(1, completed.ReplaysSkipped);
                Assert.Single(timeSeries); // Only the long replay should be stored
            }
            finally
            {
                try
                {
                    Directory.Delete(tempRoot, true);
                }
                catch
                {
                    // Best-effort cleanup; ignore if locked
                }
            }
        }
    }
}
