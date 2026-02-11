using System.Threading.Tasks;
using PitWall.Api.Services;
using Xunit;

namespace PitWall.Tests
{
    public class NullSessionSummaryServiceTests
    {
        private readonly NullSessionSummaryService _service;

        public NullSessionSummaryServiceTests()
        {
            _service = new NullSessionSummaryService();
        }

        [Fact]
        public async Task GetSessionSummariesAsync_ReturnsEmptyList()
        {
            var result = await _service.GetSessionSummariesAsync();
            
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetSessionSummaryAsync_ReturnsNull()
        {
            var result = await _service.GetSessionSummaryAsync(1);
            
            Assert.Null(result);
        }

        [Fact]
        public async Task GetSessionSummaryAsync_WithDifferentSessionIds_ReturnsNull()
        {
            var result1 = await _service.GetSessionSummaryAsync(1);
            var result2 = await _service.GetSessionSummaryAsync(999);
            
            Assert.Null(result1);
            Assert.Null(result2);
        }
    }
}
