using System.Collections.Generic;
using PitWall.UI.Models;
using PitWall.UI.Services;
using Xunit;
using static PitWall.UI.Services.TrackMetadataStore;

namespace PitWall.UI.Tests
{
    /// <summary>
    /// Tests for TrackMetadataStore internal pure helper methods.
    /// These are accessible via InternalsVisibleTo.
    /// </summary>
    public class TrackMetadataStoreInternalTests
    {
        #region NormalizeKey Tests

        [Fact]
        public void NormalizeKey_RemovesAccents()
        {
            var result = TrackMetadataStore.NormalizeKey("Nürburgring");

            Assert.Equal("Nurburgring", result);
        }

        [Fact]
        public void NormalizeKey_PreservesNonAccented()
        {
            var result = TrackMetadataStore.NormalizeKey("Silverstone");

            Assert.Equal("Silverstone", result);
        }

        [Fact]
        public void NormalizeKey_TrimsWhitespace()
        {
            var result = TrackMetadataStore.NormalizeKey("  Spa  ");

            Assert.Equal("Spa", result);
        }

        [Fact]
        public void NormalizeKey_HandlesComplexAccents()
        {
            var result = TrackMetadataStore.NormalizeKey("São Paulo");

            Assert.Equal("Sao Paulo", result);
        }

        #endregion

        #region ToSlug Tests

        [Fact]
        public void ToSlug_BasicName_ReturnsLowerDashed()
        {
            var result = TrackMetadataStore.ToSlug("Spa Francorchamps");

            Assert.Equal("spa-francorchamps", result);
        }

        [Fact]
        public void ToSlug_AccentedName_NormalizesAndDashes()
        {
            var result = TrackMetadataStore.ToSlug("Nürburgring GP");

            Assert.Equal("nurburgring-gp", result);
        }

        [Fact]
        public void ToSlug_SpecialChars_ReplacesWithDashes()
        {
            var result = TrackMetadataStore.ToSlug("Track (Layout A)");

            Assert.Equal("track-layout-a", result);
        }

        [Fact]
        public void ToSlug_MultipleSeparators_CollapsesToSingle()
        {
            var result = TrackMetadataStore.ToSlug("Track -- Name");

            Assert.Equal("track-name", result);
        }

        [Fact]
        public void ToSlug_TrailingSpecialChars_TrimsEnds()
        {
            var result = TrackMetadataStore.ToSlug("--Track--");

            Assert.Equal("track", result);
        }

        #endregion

        #region MapDirection Tests

        [Fact]
        public void MapDirection_Zero_ReturnsLeft()
        {
            Assert.Equal("Left", TrackMetadataStore.MapDirection(0));
        }

        [Fact]
        public void MapDirection_One_ReturnsRight()
        {
            Assert.Equal("Right", TrackMetadataStore.MapDirection(1));
        }

        [Fact]
        public void MapDirection_Null_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, TrackMetadataStore.MapDirection(null));
        }

        [Fact]
        public void MapDirection_Other_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, TrackMetadataStore.MapDirection(5));
        }

        #endregion

        #region MapSeverity Tests

        [Theory]
        [InlineData(1, "Slow")]
        [InlineData(2, "Slow")]
        [InlineData(3, "Medium")]
        [InlineData(4, "Medium")]
        [InlineData(5, "Fast")]
        [InlineData(6, "Fast")]
        public void MapSeverity_ValidScale_ReturnsCorrectSeverity(int scale, string expected)
        {
            Assert.Equal(expected, TrackMetadataStore.MapSeverity(scale));
        }

        [Fact]
        public void MapSeverity_Null_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, TrackMetadataStore.MapSeverity(null));
        }

        [Fact]
        public void MapSeverity_OutOfRange_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, TrackMetadataStore.MapSeverity(7));
            Assert.Equal(string.Empty, TrackMetadataStore.MapSeverity(0));
        }

        #endregion

        #region ClampFraction Tests

        [Fact]
        public void ClampFraction_Negative_ReturnsZero()
        {
            Assert.Equal(0.0, TrackMetadataStore.ClampFraction(-0.5));
        }

        [Fact]
        public void ClampFraction_GreaterThanOne_ReturnsOne()
        {
            Assert.Equal(1.0, TrackMetadataStore.ClampFraction(1.5));
        }

        [Fact]
        public void ClampFraction_InRange_ReturnsValue()
        {
            Assert.Equal(0.5, TrackMetadataStore.ClampFraction(0.5));
        }

        [Fact]
        public void ClampFraction_Zero_ReturnsZero()
        {
            Assert.Equal(0.0, TrackMetadataStore.ClampFraction(0.0));
        }

        [Fact]
        public void ClampFraction_One_ReturnsOne()
        {
            Assert.Equal(1.0, TrackMetadataStore.ClampFraction(1.0));
        }

        #endregion

        #region BuildSectors Tests

        [Fact]
        public void BuildSectors_NullSectors_ReturnsEmpty()
        {
            var data = new LovelyTrackData { Sector = null };

            var result = TrackMetadataStore.BuildSectors(data);

            Assert.Empty(result);
        }

        [Fact]
        public void BuildSectors_EmptySectors_ReturnsEmpty()
        {
            var data = new LovelyTrackData { Sector = new List<LovelySector>() };

            var result = TrackMetadataStore.BuildSectors(data);

            Assert.Empty(result);
        }

        [Fact]
        public void BuildSectors_TwoMarkers_CreatesThreeSectors()
        {
            var data = new LovelyTrackData
            {
                Sector = new List<LovelySector>
                {
                    new LovelySector { Name = "S1", Marker = 0.33 },
                    new LovelySector { Name = "S2", Marker = 0.66 }
                }
            };

            var result = TrackMetadataStore.BuildSectors(data);

            Assert.Equal(3, result.Count);
            Assert.Equal("S1", result[0].Name);
            Assert.Equal(0.0, result[0].Start);
            Assert.Equal(0.33, result[0].End);
            Assert.Equal("S2", result[1].Name);
            Assert.Equal(0.33, result[1].Start);
            Assert.Equal(0.66, result[1].End);
            Assert.Equal("Sector 3", result[2].Name);
            Assert.Equal(0.66, result[2].Start);
            Assert.Equal(1.0, result[2].End);
        }

        [Fact]
        public void BuildSectors_NoName_DefaultsToSectorN()
        {
            var data = new LovelyTrackData
            {
                Sector = new List<LovelySector>
                {
                    new LovelySector { Marker = 0.5 }
                }
            };

            var result = TrackMetadataStore.BuildSectors(data);

            Assert.Equal(2, result.Count);
            Assert.Equal("Sector 1", result[0].Name);
        }

        [Fact]
        public void BuildSectors_ZeroMarker_Skipped()
        {
            var data = new LovelyTrackData
            {
                Sector = new List<LovelySector>
                {
                    new LovelySector { Name = "Zero", Marker = 0.0 },
                    new LovelySector { Name = "Valid", Marker = 0.5 }
                }
            };

            var result = TrackMetadataStore.BuildSectors(data);

            Assert.Equal(2, result.Count);
            Assert.Equal("Valid", result[0].Name);
        }

        [Fact]
        public void BuildSectors_NullMarker_Filtered()
        {
            var data = new LovelyTrackData
            {
                Sector = new List<LovelySector>
                {
                    new LovelySector { Name = "NoMarker", Marker = null },
                    new LovelySector { Name = "Valid", Marker = 0.5 }
                }
            };

            var result = TrackMetadataStore.BuildSectors(data);

            Assert.Equal(2, result.Count);
            Assert.Equal("Valid", result[0].Name);
        }

        [Fact]
        public void BuildSectors_AllNullMarkers_ReturnsEmpty()
        {
            var data = new LovelyTrackData
            {
                Sector = new List<LovelySector>
                {
                    new LovelySector { Marker = null },
                    new LovelySector { Marker = null }
                }
            };

            var result = TrackMetadataStore.BuildSectors(data);

            Assert.Empty(result);
        }

        #endregion

        #region BuildCorners Tests

        [Fact]
        public void BuildCorners_NullTurns_ReturnsEmpty()
        {
            var data = new LovelyTrackData { Turn = null };

            var result = TrackMetadataStore.BuildCorners(data);

            Assert.Empty(result);
        }

        [Fact]
        public void BuildCorners_EmptyTurns_ReturnsEmpty()
        {
            var data = new LovelyTrackData { Turn = new List<LovelyTurn>() };

            var result = TrackMetadataStore.BuildCorners(data);

            Assert.Empty(result);
        }

        [Fact]
        public void BuildCorners_SingleTurn_BuildsCorrectly()
        {
            var data = new LovelyTrackData
            {
                Turn = new List<LovelyTurn>
                {
                    new LovelyTurn
                    {
                        Number = 1,
                        Name = "La Source",
                        Start = 0.05,
                        End = 0.10,
                        Direction = 1,
                        Scale = 2
                    }
                }
            };

            var result = TrackMetadataStore.BuildCorners(data);

            Assert.Single(result);
            Assert.Equal(1, result[0].Number);
            Assert.Equal("La Source", result[0].Name);
            Assert.Equal(0.05, result[0].Start);
            Assert.Equal(0.10, result[0].End);
            Assert.Equal("Right", result[0].Direction);
            Assert.Equal("Slow", result[0].Severity);
        }

        [Fact]
        public void BuildCorners_NoName_DefaultsToTurnN()
        {
            var data = new LovelyTrackData
            {
                Turn = new List<LovelyTurn>
                {
                    new LovelyTurn { Number = 3, Direction = 0, Scale = 5 }
                }
            };

            var result = TrackMetadataStore.BuildCorners(data);

            Assert.Single(result);
            Assert.Equal("Turn 3", result[0].Name);
            Assert.Equal("Left", result[0].Direction);
            Assert.Equal("Fast", result[0].Severity);
        }

        [Fact]
        public void BuildCorners_NullNumber_UsesSequential()
        {
            var data = new LovelyTrackData
            {
                Turn = new List<LovelyTurn>
                {
                    new LovelyTurn { Name = "Turn A" }
                }
            };

            var result = TrackMetadataStore.BuildCorners(data);

            Assert.Single(result);
            Assert.Equal(1, result[0].Number);
        }

        [Fact]
        public void BuildCorners_NullStartEnd_ClampsToZero()
        {
            var data = new LovelyTrackData
            {
                Turn = new List<LovelyTurn>
                {
                    new LovelyTurn { Number = 1, Start = null, End = null }
                }
            };

            var result = TrackMetadataStore.BuildCorners(data);

            Assert.Equal(0.0, result[0].Start);
            Assert.Equal(0.0, result[0].End);
        }

        #endregion

        #region MergeTracks Tests

        [Fact]
        public void MergeTracks_EmptyBoth_ReturnsDefault()
        {
            var result = TrackMetadataStore.MergeTracks(
                new List<TrackMetadata>(),
                new List<TrackMetadata>());

            Assert.Single(result);
            Assert.Equal("Default", result[0].Name);
        }

        [Fact]
        public void MergeTracks_OverlappingTracks_LovelyWins()
        {
            var baseTrack = new TrackMetadata
            {
                Name = "Spa",
                TrackId = "spa-francorchamps",
                Sectors = new List<TrackSector> { new TrackSector { Name = "Old" } }
            };
            var lovelyTrack = new TrackMetadata
            {
                Name = "Spa",
                TrackId = "spa-francorchamps",
                Sectors = new List<TrackSector> { new TrackSector { Name = "New" } }
            };

            var result = TrackMetadataStore.MergeTracks(
                new List<TrackMetadata> { baseTrack },
                new List<TrackMetadata> { lovelyTrack });

            // Default + replaced Spa
            Assert.Equal(2, result.Count);
            Assert.Equal("Default", result[0].Name);
            Assert.Equal("Spa", result[1].Name);
            Assert.Equal("New", result[1].Sectors[0].Name);
        }

        [Fact]
        public void MergeTracks_NoOverlap_AddsAll()
        {
            var baseTrack = new TrackMetadata { Name = "Monza" };
            var lovelyTrack = new TrackMetadata { Name = "Le Mans" };

            var result = TrackMetadataStore.MergeTracks(
                new List<TrackMetadata> { baseTrack },
                new List<TrackMetadata> { lovelyTrack });

            // Default + Monza + Le Mans
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void MergeTracks_DefaultInBase_PreservedAtIndex0()
        {
            var defaultTrack = new TrackMetadata { Name = "Default" };
            var baseTrack = new TrackMetadata { Name = "Monza" };

            var result = TrackMetadataStore.MergeTracks(
                new List<TrackMetadata> { defaultTrack, baseTrack },
                new List<TrackMetadata>());

            Assert.Equal("Default", result[0].Name);
            Assert.Equal("Monza", result[1].Name);
        }

        #endregion

        #region IsSameTrack Tests

        [Fact]
        public void IsSameTrack_SameTrackId_ReturnsTrue()
        {
            var a = new TrackMetadata { Name = "Track A", TrackId = "spa" };
            var b = new TrackMetadata { Name = "Track B", TrackId = "spa" };

            Assert.True(TrackMetadataStore.IsSameTrack(a, b));
        }

        [Fact]
        public void IsSameTrack_SameName_ReturnsTrue()
        {
            var a = new TrackMetadata { Name = "Spa" };
            var b = new TrackMetadata { Name = "Spa" };

            Assert.True(TrackMetadataStore.IsSameTrack(a, b));
        }

        [Fact]
        public void IsSameTrack_DifferentNames_ReturnsFalse()
        {
            var a = new TrackMetadata { Name = "Spa" };
            var b = new TrackMetadata { Name = "Monza" };

            Assert.False(TrackMetadataStore.IsSameTrack(a, b));
        }

        [Fact]
        public void IsSameTrack_SameSlugTrackId_ReturnsTrue()
        {
            var a = new TrackMetadata { Name = "A", TrackId = "Spa Francorchamps" };
            var b = new TrackMetadata { Name = "B", TrackId = "spa-francorchamps" };

            Assert.True(TrackMetadataStore.IsSameTrack(a, b));
        }

        [Fact]
        public void IsSameTrack_SameNameDifferentCase_ReturnsTrue()
        {
            var a = new TrackMetadata { Name = "SPA" };
            var b = new TrackMetadata { Name = "spa" };

            Assert.True(TrackMetadataStore.IsSameTrack(a, b));
        }

        #endregion
    }
}
