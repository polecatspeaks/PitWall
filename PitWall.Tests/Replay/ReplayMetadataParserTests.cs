using System;
using PitWall.Replay;
using Xunit;

namespace PitWall.Tests.Replay
{
    public class ReplayMetadataParserTests
    {
        private readonly ReplayMetadataParser _parser = new ReplayMetadataParser();

        [Fact]
        public void ExtractSessionDate_DateStampedFile_ParsesCorrectly()
        {
            var filePath = "C:\\replays\\2025_11_08_09_58_17.rpy";

            var date = _parser.ExtractSessionDate(filePath);

            Assert.Equal(new DateTime(2025, 11, 8, 9, 58, 17), date);
        }

        [Fact]
        public void ExtractSessionDate_DifferentDateStampedFile_ParsesCorrectly()
        {
            var filePath = "2025_12_05_14_23_45.rpy";

            var date = _parser.ExtractSessionDate(filePath);

            Assert.Equal(new DateTime(2025, 12, 5, 14, 23, 45), date);
        }

        [Fact]
        public void ExtractSessionDate_WithPath_ParsesFilenameOnly()
        {
            var filePath = "C:\\Users\\test\\Documents\\iRacing\\replays\\2025_11_16_20_30_00.rpy";

            var date = _parser.ExtractSessionDate(filePath);

            Assert.Equal(new DateTime(2025, 11, 16, 20, 30, 0), date);
        }

        [Fact]
        public void ExtractSessionDate_InvalidFormat_ThrowsFormatException()
        {
            var filePath = "invalid_replay_name.rpy";

            Assert.Throws<FormatException>(() => _parser.ExtractSessionDate(filePath));
        }

        [Fact]
        public void ExtractSessionDate_SubsessionFormat_AcceptsPattern()
        {
            // Subsession format requires YAML parsing which we can't fully test without real files
            // But we can verify the pattern is recognized
            var filePath = "subses80974445.rpy";

            // This will try to parse YAML, which will fail gracefully and use file creation time
            // The important part is it doesn't throw FormatException on pattern recognition
            try
            {
                var date = _parser.ExtractSessionDate(filePath);
                // If we get here without exception, pattern was recognized
                Assert.True(true);
            }
            catch (System.IO.FileNotFoundException)
            {
                // Expected when file doesn't exist - pattern was recognized
                Assert.True(true);
            }
            catch (FormatException)
            {
                // Should NOT throw FormatException for valid pattern
                Assert.True(false, "Should not throw FormatException for subsession pattern");
            }
        }
    }
}
