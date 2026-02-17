using System;
using PitWall.UI.Services;
using Xunit;

namespace PitWall.UI.Tests
{
    /// <summary>
    /// Tests for ApiProbe constructor validation.
    /// </summary>
    public class ApiProbeTests
    {
        [Fact]
        public void Constructor_ValidPath_DoesNotThrow()
        {
            var probe = new ApiProbe("/api/health");

            Assert.NotNull(probe);
        }

        [Fact]
        public void Constructor_DefaultPath_DoesNotThrow()
        {
            var probe = new ApiProbe();

            Assert.NotNull(probe);
        }

        [Fact]
        public void Constructor_EmptyPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ApiProbe(""));
        }

        [Fact]
        public void Constructor_WhitespacePath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ApiProbe("   "));
        }

        [Fact]
        public void Constructor_NullPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ApiProbe(null!));
        }

        [Fact]
        public void Constructor_CustomTimeout_DoesNotThrow()
        {
            var probe = new ApiProbe("/api/test", TimeSpan.FromSeconds(2));

            Assert.NotNull(probe);
        }
    }
}
