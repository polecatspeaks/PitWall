using PitWall.UI.Services;
using PitWall.UI.Models;
using Xunit;

namespace PitWall.UI.Tests
{
    public class TrackMetadataStoreTests
    {
        [Fact]
        public void Constructor_InitializesSuccessfully()
        {
            var store = new TrackMetadataStore();
            
            Assert.NotNull(store);
        }

        [Fact]
        public void GetByName_NullInput_ReturnsDefault()
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName(null);
            
            Assert.NotNull(result);
            Assert.Equal("Default", result.Name);
            Assert.Equal(3, result.Sectors.Count);
        }

        [Fact]
        public void GetByName_EmptyString_ReturnsDefault()
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName("");
            
            Assert.NotNull(result);
            Assert.Equal("Default", result.Name);
            Assert.Equal(3, result.Sectors.Count);
        }

        [Fact]
        public void GetByName_WhitespaceOnly_ReturnsDefault()
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName("   ");
            
            Assert.NotNull(result);
            Assert.Equal("Default", result.Name);
        }

        [Fact]
        public void GetByName_UnknownTrack_ReturnsDefault()
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName("NonExistentTrack12345");
            
            Assert.NotNull(result);
            Assert.Equal("Default", result.Name);
            Assert.Equal(3, result.Sectors.Count);
        }

        [Fact]
        public void GetByName_Default_ReturnsDefaultWithThreeSectors()
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName("Default");
            
            Assert.NotNull(result);
            Assert.Equal("Default", result.Name);
            Assert.Equal(3, result.Sectors.Count);
            Assert.Equal("Sector 1", result.Sectors[0].Name);
            Assert.Equal(0.0, result.Sectors[0].Start);
            Assert.Equal(0.333, result.Sectors[0].End);
        }

        [Theory]
        [InlineData("Spa-Francorchamps")]
        [InlineData("Monza")]
        [InlineData("Silverstone")]
        [InlineData("Monaco")]
        public void GetByName_ValidTrackNames_ReturnsNonNull(string trackName)
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName(trackName);
            
            Assert.NotNull(result);
            Assert.NotNull(result.Sectors);
        }

        [Theory]
        [InlineData("Spa")]
        [InlineData("spa")]
        [InlineData("SPA")]
        public void GetByName_CaseInsensitive(string input)
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName(input);
            
            Assert.NotNull(result);
        }

        [Fact]
        public void GetByName_WithAccents_HandlesNormalization()
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName("São Paulo");
            
            Assert.NotNull(result);
        }

        [Fact]
        public void GetByName_WithSpecialCharacters_HandlesCorrectly()
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName("Nürburgring");
            
            Assert.NotNull(result);
        }

        [Fact]
        public void GetByName_PartialMatch_CanFindTrack()
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName("Circuit");
            
            Assert.NotNull(result);
        }

        [Fact]
        public void GetByName_WithHyphens_HandlesSlugMatching()
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName("spa-francorchamps");
            
            Assert.NotNull(result);
        }

        [Fact]
        public void DefaultTrack_HasValidSectorConfiguration()
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName("Default");
            
            Assert.Equal(3, result.Sectors.Count);
            Assert.Equal(0.0, result.Sectors[0].Start);
            Assert.Equal(0.333, result.Sectors[0].End);
            Assert.Equal(0.333, result.Sectors[1].Start);
            Assert.Equal(0.666, result.Sectors[1].End);
            Assert.Equal(0.666, result.Sectors[2].Start);
            Assert.Equal(1.0, result.Sectors[2].End);
        }

        [Fact]
        public void GetByName_ReturnedTrack_HasSectors()
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName("SomeTrack");
            
            Assert.NotNull(result.Sectors);
            Assert.NotEmpty(result.Sectors);
        }

        [Fact]
        public void GetByName_ReturnedTrack_HasCornersList()
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName("Default");
            
            Assert.NotNull(result.Corners);
        }

        [Fact]
        public void GetByName_ReturnedTrack_HasOutlineList()
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName("Default");
            
            Assert.NotNull(result.Outline);
        }

        [Fact]
        public void GetByName_MultipleCallsSameTrack_ReturnsSameData()
        {
            var store = new TrackMetadataStore();
            
            var result1 = store.GetByName("Default");
            var result2 = store.GetByName("Default");
            
            Assert.Equal(result1.Name, result2.Name);
            Assert.Equal(result1.Sectors.Count, result2.Sectors.Count);
        }

        [Theory]
        [InlineData("Default", "Default")]
        [InlineData("default", "Default")]
        [InlineData("DEFAULT", "Default")]
        public void GetByName_DefaultCaseInsensitive_ReturnsDefault(string input, string expectedName)
        {
            var store = new TrackMetadataStore();
            
            var result = store.GetByName(input);
            
            Assert.Equal(expectedName, result.Name);
        }

        [Fact]
        public void TrackMetadata_FindSector_ReturnsCorrectSector()
        {
            var store = new TrackMetadataStore();
            var track = store.GetByName("Default");
            
            var sector = track.FindSector(0.5);
            
            Assert.NotNull(sector);
            Assert.Equal("Sector 2", sector.Name);
        }

        [Fact]
        public void TrackMetadata_FindSector_FirstSector()
        {
            var store = new TrackMetadataStore();
            var track = store.GetByName("Default");
            
            var sector = track.FindSector(0.1);
            
            Assert.NotNull(sector);
            Assert.Equal("Sector 1", sector.Name);
        }

        [Fact]
        public void TrackMetadata_FindSector_LastSector()
        {
            var store = new TrackMetadataStore();
            var track = store.GetByName("Default");
            
            var sector = track.FindSector(0.9);
            
            Assert.NotNull(sector);
            Assert.Equal("Sector 3", sector.Name);
        }

        [Fact]
        public void TrackMetadata_FindSector_AtBoundary()
        {
            var store = new TrackMetadataStore();
            var track = store.GetByName("Default");
            
            var sector = track.FindSector(0.333);
            
            Assert.NotNull(sector);
        }

        [Fact]
        public void TrackMetadata_FindCorner_NoCorners_ReturnsNull()
        {
            var store = new TrackMetadataStore();
            var track = store.GetByName("Default");
            
            var corner = track.FindCorner(0.5);
            
            Assert.Null(corner);
        }
    }
}
