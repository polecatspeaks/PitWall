using System.Linq;
using System.Threading.Tasks;
using PitWall.Core.Services;
using Xunit;

namespace PitWall.Tests
{
    public class NullLmuTelemetryReaderTests
    {
        private readonly NullLmuTelemetryReader _reader;

        public NullLmuTelemetryReaderTests()
        {
            _reader = new NullLmuTelemetryReader();
        }

        [Fact]
        public async Task GetSessionCountAsync_ReturnsZero()
        {
            var result = await _reader.GetSessionCountAsync();
            
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task GetChannelsAsync_ReturnsEmptyList()
        {
            var result = await _reader.GetChannelsAsync();
            
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task ReadSamplesAsync_ReturnsEmpty()
        {
            var samples = await ConsumeAsync(_reader.ReadSamplesAsync(1, 0, 100));
            
            Assert.NotNull(samples);
            Assert.Empty(samples);
        }

        [Fact]
        public async Task ReadSamplesAsync_WithDifferentParameters_ReturnsEmpty()
        {
            var samples1 = await ConsumeAsync(_reader.ReadSamplesAsync(1, 0, 10));
            var samples2 = await ConsumeAsync(_reader.ReadSamplesAsync(999, 100, 200));
            
            Assert.Empty(samples1);
            Assert.Empty(samples2);
        }

        private static async Task<List<Core.Models.TelemetrySample>> ConsumeAsync(System.Collections.Generic.IAsyncEnumerable<Core.Models.TelemetrySample> asyncEnumerable)
        {
            var result = new List<Core.Models.TelemetrySample>();
            await foreach (var item in asyncEnumerable)
            {
                result.Add(item);
            }
            return result;
        }
    }
}
