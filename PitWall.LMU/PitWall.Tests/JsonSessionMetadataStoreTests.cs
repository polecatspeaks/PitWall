using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Api.Models;
using PitWall.Api.Services;
using Xunit;

namespace PitWall.Tests
{
    public class JsonSessionMetadataStoreTests : IDisposable
    {
        private readonly string _tempFilePath;

        public JsonSessionMetadataStoreTests()
        {
            _tempFilePath = Path.Combine(Path.GetTempPath(), $"test_metadata_{Guid.NewGuid()}.json");
        }

        public void Dispose()
        {
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenFilePathIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new JsonSessionMetadataStore(null!, NullLogger<JsonSessionMetadataStore>.Instance));
        }

        [Fact]
        public async Task GetAllAsync_ReturnsEmptyDictionary_WhenFileDoesNotExist()
        {
            var store = new JsonSessionMetadataStore(_tempFilePath);

            var result = await store.GetAllAsync();

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAsync_ReturnsNull_WhenSessionDoesNotExist()
        {
            var store = new JsonSessionMetadataStore(_tempFilePath);

            var result = await store.GetAsync(1);

            Assert.Null(result);
        }

        [Fact]
        public async Task SetAsync_ThrowsArgumentNullException_WhenMetadataIsNull()
        {
            var store = new JsonSessionMetadataStore(_tempFilePath);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                store.SetAsync(1, null!));
        }

        [Fact]
        public async Task SetAsync_CreatesFileAndStoresMetadata()
        {
            var store = new JsonSessionMetadataStore(_tempFilePath);
            var metadata = new SessionMetadata
            {
                Track = "Monza",
                TrackId = "monza_gp",
                Car = "Ferrari 488 GT3"
            };

            await store.SetAsync(1, metadata);

            Assert.True(File.Exists(_tempFilePath));
            var result = await store.GetAsync(1);
            Assert.NotNull(result);
            Assert.Equal("Monza", result.Track);
            Assert.Equal("monza_gp", result.TrackId);
            Assert.Equal("Ferrari 488 GT3", result.Car);
        }

        [Fact]
        public async Task SetAsync_UpdatesExistingMetadata()
        {
            var store = new JsonSessionMetadataStore(_tempFilePath);
            var metadata1 = new SessionMetadata { Track = "Monza", Car = "Ferrari" };
            var metadata2 = new SessionMetadata { Track = "Spa", Car = "McLaren" };

            await store.SetAsync(1, metadata1);
            await store.SetAsync(1, metadata2);

            var result = await store.GetAsync(1);
            Assert.NotNull(result);
            Assert.Equal("Spa", result.Track);
            Assert.Equal("McLaren", result.Car);
        }

        [Fact]
        public async Task SetAsync_HandlesMultipleSessions()
        {
            var store = new JsonSessionMetadataStore(_tempFilePath);
            var metadata1 = new SessionMetadata { Track = "Monza", Car = "Ferrari" };
            var metadata2 = new SessionMetadata { Track = "Spa", Car = "McLaren" };
            var metadata3 = new SessionMetadata { Track = "Silverstone", Car = "Mercedes" };

            await store.SetAsync(1, metadata1);
            await store.SetAsync(2, metadata2);
            await store.SetAsync(3, metadata3);

            var all = await store.GetAllAsync();
            Assert.Equal(3, all.Count);
            Assert.Equal("Monza", all[1].Track);
            Assert.Equal("Spa", all[2].Track);
            Assert.Equal("Silverstone", all[3].Track);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllStoredMetadata()
        {
            var store = new JsonSessionMetadataStore(_tempFilePath);
            await store.SetAsync(1, new SessionMetadata { Track = "Track1" });
            await store.SetAsync(2, new SessionMetadata { Track = "Track2" });

            var result = await store.GetAllAsync();

            Assert.Equal(2, result.Count);
            Assert.True(result.ContainsKey(1));
            Assert.True(result.ContainsKey(2));
        }

        [Fact]
        public async Task SetAsync_CreatesDirectoryIfNotExists()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"testdir_{Guid.NewGuid()}");
            var filePath = Path.Combine(tempDir, "metadata.json");

            try
            {
                var store = new JsonSessionMetadataStore(filePath);
                var metadata = new SessionMetadata { Track = "Monza" };

                await store.SetAsync(1, metadata);

                Assert.True(Directory.Exists(tempDir));
                Assert.True(File.Exists(filePath));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task Store_LoadsExistingDataOnFirstAccess()
        {
            var store1 = new JsonSessionMetadataStore(_tempFilePath);
            await store1.SetAsync(1, new SessionMetadata { Track = "Monza" });

            var store2 = new JsonSessionMetadataStore(_tempFilePath);
            var result = await store2.GetAsync(1);

            Assert.NotNull(result);
            Assert.Equal("Monza", result.Track);
        }

        [Fact]
        public async Task Store_HandlesCorruptedJsonFile()
        {
            File.WriteAllText(_tempFilePath, "{ invalid json content }");

            var store = new JsonSessionMetadataStore(_tempFilePath);
            var result = await store.GetAllAsync();

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task SetAsync_PersistsDataImmediately()
        {
            var store = new JsonSessionMetadataStore(_tempFilePath);
            await store.SetAsync(1, new SessionMetadata { Track = "Monza" });

            var fileContent = File.ReadAllText(_tempFilePath);
            Assert.Contains("Monza", fileContent);
        }

        [Fact]
        public async Task GetAsync_ReturnsCopyOfData()
        {
            var store = new JsonSessionMetadataStore(_tempFilePath);
            var original = new SessionMetadata { Track = "Monza", Car = "Ferrari" };
            await store.SetAsync(1, original);

            var retrieved1 = await store.GetAsync(1);
            var retrieved2 = await store.GetAsync(1);

            Assert.NotNull(retrieved1);
            Assert.NotNull(retrieved2);
            Assert.Equal(retrieved1.Track, retrieved2.Track);
        }

        [Fact]
        public async Task Store_ThreadSafe_ConcurrentWrites()
        {
            var store = new JsonSessionMetadataStore(_tempFilePath);
            var tasks = new List<Task>();

            for (int i = 1; i <= 10; i++)
            {
                var sessionId = i;
                tasks.Add(Task.Run(async () =>
                {
                    await store.SetAsync(sessionId, new SessionMetadata
                    {
                        Track = $"Track{sessionId}",
                        Car = $"Car{sessionId}"
                    });
                }));
            }

            await Task.WhenAll(tasks);

            var all = await store.GetAllAsync();
            Assert.Equal(10, all.Count);
        }
    }
}
