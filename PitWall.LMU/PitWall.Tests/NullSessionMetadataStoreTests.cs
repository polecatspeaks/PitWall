using System.Threading.Tasks;
using PitWall.Api.Services;
using Xunit;

namespace PitWall.Tests
{
    public class NullSessionMetadataStoreTests
    {
        private readonly NullSessionMetadataStore _store;

        public NullSessionMetadataStoreTests()
        {
            _store = new NullSessionMetadataStore();
        }

        [Fact]
        public async Task GetAllAsync_ReturnsEmptyDictionary()
        {
            var result = await _store.GetAllAsync();
            
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAsync_ReturnsNull()
        {
            var result = await _store.GetAsync(1);
            
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAsync_WithDifferentSessionIds_ReturnsNull()
        {
            var result1 = await _store.GetAsync(1);
            var result2 = await _store.GetAsync(999);
            
            Assert.Null(result1);
            Assert.Null(result2);
        }

        [Fact]
        public async Task SetAsync_CompletesSuccessfully()
        {
            var metadata = new Api.Models.SessionMetadata
            {
                Track = "Test Track",
                Car = "Test Car"
            };

            await _store.SetAsync(1, metadata);
            
            // Verify it still returns null after setting (null implementation)
            var result = await _store.GetAsync(1);
            Assert.Null(result);
        }
    }
}
