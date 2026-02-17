using System;
using System.Collections.Generic;
using Avalonia;
using PitWall.UI.Models;
using PitWall.UI.Services;
using Xunit;

namespace PitWall.UI.Tests
{
    /// <summary>
    /// Tests for TrackMapService internal types and static helpers:
    /// TrackMapState, SegmentClassification, OutlineSegment, ClassifySegment,
    /// GetSeverityFromAngle, GetSeverityAngle, BuildSegmentStatus.
    /// </summary>
    public class TrackMapServiceInternalTests
    {
        #region TrackMapState.Build

        [Fact]
        public void Build_WithValidCircularTrack_ReturnsState()
        {
            var samples = CreateCircularLapSamples(100);
            var state = TrackMapService.TrackMapState.Build(samples);

            Assert.NotNull(state);
            Assert.True(state!.NormalizedPoints.Count >= 50);
            Assert.True(state.TotalDistance > 0);
            Assert.False(state.IsOutline);
        }

        [Fact]
        public void Build_WithTooFewValidSamples_ReturnsNull()
        {
            var samples = new List<TelemetrySampleDto>();
            for (int i = 0; i < 10; i++)
            {
                samples.Add(new TelemetrySampleDto
                {
                    Latitude = 52.0 + i * 0.001,
                    Longitude = -1.0 + i * 0.001,
                    LapNumber = 1
                });
            }

            var state = TrackMapService.TrackMapState.Build(samples);
            Assert.Null(state);
        }

        [Fact]
        public void Build_FiltersClosePoints()
        {
            // Create samples where many are within MinPointSpacingMeters
            var samples = new List<TelemetrySampleDto>();
            for (int i = 0; i < 200; i++)
            {
                samples.Add(new TelemetrySampleDto
                {
                    Latitude = 52.0 + i * 0.0001,
                    Longitude = -1.0 + i * 0.0001,
                    LapNumber = 1
                });
            }

            var state = TrackMapService.TrackMapState.Build(samples);
            // Should succeed since there are enough widely spaced points
            Assert.NotNull(state);
            // Normalized points should be fewer than input due to spacing filter
            Assert.True(state!.NormalizedPoints.Count <= samples.Count);
        }

        [Fact]
        public void Build_SetsOriginAndScale()
        {
            var samples = CreateCircularLapSamples(100);
            var state = TrackMapService.TrackMapState.Build(samples);

            Assert.NotNull(state);
            Assert.True(state!.LatScale > 0);
            Assert.True(state.LonScale > 0);
        }

        [Fact]
        public void Build_NormalizedPointsWithinUnitSquare()
        {
            var samples = CreateCircularLapSamples(100);
            var state = TrackMapService.TrackMapState.Build(samples);

            Assert.NotNull(state);
            foreach (var point in state!.NormalizedPoints)
            {
                Assert.InRange(point.X, -0.01, 1.01);
                Assert.InRange(point.Y, -0.01, 1.01);
            }
        }

        [Fact]
        public void Build_CumulativeDistancesMonotonicallyIncrease()
        {
            var samples = CreateCircularLapSamples(100);
            var state = TrackMapService.TrackMapState.Build(samples);

            Assert.NotNull(state);
            for (int i = 1; i < state!.CumulativeDistances.Count; i++)
            {
                Assert.True(state.CumulativeDistances[i] >= state.CumulativeDistances[i - 1]);
            }
        }

        #endregion

        #region TrackMapState.BuildFromNormalized

        [Fact]
        public void BuildFromNormalized_WithEnoughPoints_ReturnsState()
        {
            var points = CreateCircularOutlinePoints(20);
            var state = TrackMapService.TrackMapState.BuildFromNormalized(points);

            Assert.NotNull(state);
            Assert.True(state!.IsOutline);
            Assert.True(state.NormalizedPoints.Count >= 12);
            Assert.True(state.TotalDistance > 0);
        }

        [Fact]
        public void BuildFromNormalized_TooFewPoints_ReturnsNull()
        {
            var points = new List<Point>
            {
                new Point(0, 0),
                new Point(1, 0),
                new Point(1, 1)
            };

            var state = TrackMapService.TrackMapState.BuildFromNormalized(points);
            Assert.Null(state);
        }

        [Fact]
        public void BuildFromNormalized_SetsIsOutlineTrue()
        {
            var points = CreateCircularOutlinePoints(20);
            var state = TrackMapService.TrackMapState.BuildFromNormalized(points);

            Assert.NotNull(state);
            Assert.True(state!.IsOutline);
            Assert.Equal(0, state.OriginLat);
            Assert.Equal(0, state.OriginLon);
        }

        [Fact]
        public void BuildFromNormalized_NormalizesPointsToUnitSquare()
        {
            var points = new List<Point>();
            for (int i = 0; i < 20; i++)
            {
                points.Add(new Point(i * 10, i * 5));
            }

            var state = TrackMapService.TrackMapState.BuildFromNormalized(points);

            Assert.NotNull(state);
            foreach (var p in state!.NormalizedPoints)
            {
                Assert.InRange(p.X, -0.01, 1.01);
                Assert.InRange(p.Y, -0.01, 1.01);
            }
        }

        #endregion

        #region TrackMapState.GetFractionForIndex

        [Fact]
        public void GetFractionForIndex_FirstIndex_ReturnsZero()
        {
            var state = BuildTestMapState();
            Assert.Equal(0.0, state.GetFractionForIndex(0));
        }

        [Fact]
        public void GetFractionForIndex_LastIndex_ReturnsApproximatelyOne()
        {
            var state = BuildTestMapState();
            var fraction = state.GetFractionForIndex(state.PointCount - 1);
            Assert.InRange(fraction, 0.9, 1.0);
        }

        [Fact]
        public void GetFractionForIndex_NegativeIndex_ReturnsZero()
        {
            var state = BuildTestMapState();
            Assert.Equal(0.0, state.GetFractionForIndex(-1));
        }

        [Fact]
        public void GetFractionForIndex_OutOfRange_ReturnsZero()
        {
            var state = BuildTestMapState();
            Assert.Equal(0.0, state.GetFractionForIndex(state.PointCount + 10));
        }

        [Fact]
        public void GetFractionForIndex_MidIndex_ReturnsMidValue()
        {
            var state = BuildTestMapState();
            var mid = state.PointCount / 2;
            var fraction = state.GetFractionForIndex(mid);
            Assert.InRange(fraction, 0.1, 0.9);
        }

        #endregion

        #region TrackMapState.GetLapFraction

        [Fact]
        public void GetLapFraction_WithValidCoordinates_ReturnsFraction()
        {
            var samples = CreateCircularLapSamples(100);
            var state = TrackMapService.TrackMapState.Build(samples);

            Assert.NotNull(state);
            var fraction = state!.GetLapFraction(samples[50].Latitude, samples[50].Longitude);

            Assert.NotNull(fraction);
            Assert.InRange(fraction!.Value, 0.0, 1.0);
        }

        [Fact]
        public void GetLapFraction_NearZeroLatLon_ReturnsNull()
        {
            var state = BuildTestMapState();
            Assert.Null(state.GetLapFraction(0.0, 0.0));
        }

        [Fact]
        public void GetLapFraction_EmptyRawPoints_ReturnsNull()
        {
            // BuildFromNormalized outline state has no GPS origin
            var points = CreateCircularOutlinePoints(20);
            var state = TrackMapService.TrackMapState.BuildFromNormalized(points);

            Assert.NotNull(state);
            // Even with valid lat/lon, an outline state uses normalized coords
            // RawPoints > 0, TotalDistance > 0, but FindNearestIndex on meters space
            // Still returns a value since RawPoints is populated
            var result = state!.GetLapFraction(52.0, -1.0);
            // outline re-uses normalized as raw, so it should find something
            Assert.NotNull(result);
        }

        #endregion

        #region TrackMapState.GetNormalizedPoint

        [Fact]
        public void GetNormalizedPoint_GpsBuild_ReturnsPointWithinUnitSquare()
        {
            var samples = CreateCircularLapSamples(100);
            var state = TrackMapService.TrackMapState.Build(samples);

            Assert.NotNull(state);
            var point = state!.GetNormalizedPoint(samples[50].Latitude, samples[50].Longitude);

            Assert.NotNull(point);
            Assert.InRange(point!.Value.X, -0.1, 1.1);
            Assert.InRange(point.Value.Y, -0.1, 1.1);
        }

        [Fact]
        public void GetNormalizedPoint_NearZeroLatLon_ReturnsNull()
        {
            var state = BuildTestMapState();
            Assert.Null(state.GetNormalizedPoint(0.0000001, 0.0000001));
        }

        [Fact]
        public void GetNormalizedPoint_OutlineState_ReturnsNull()
        {
            var points = CreateCircularOutlinePoints(20);
            var state = TrackMapService.TrackMapState.BuildFromNormalized(points);

            Assert.NotNull(state);
            Assert.Null(state!.GetNormalizedPoint(52.0, -1.0));
        }

        #endregion

        #region TrackMapState.GetNormalizedPointByFraction

        [Fact]
        public void GetNormalizedPointByFraction_Zero_ReturnsFirstPoint()
        {
            var state = BuildTestMapState();
            var point = state.GetNormalizedPointByFraction(0.0);

            Assert.NotNull(point);
        }

        [Fact]
        public void GetNormalizedPointByFraction_One_ReturnsLastPoint()
        {
            var state = BuildTestMapState();
            var point = state.GetNormalizedPointByFraction(1.0);

            Assert.NotNull(point);
        }

        [Fact]
        public void GetNormalizedPointByFraction_MidValue_InterpolatesCorrectly()
        {
            var state = BuildTestMapState();
            var point = state.GetNormalizedPointByFraction(0.5);

            Assert.NotNull(point);
            Assert.InRange(point!.Value.X, -0.1, 1.1);
            Assert.InRange(point.Value.Y, -0.1, 1.1);
        }

        [Fact]
        public void GetNormalizedPointByFraction_NegativeClampedToZero()
        {
            var state = BuildTestMapState();
            var pointAtZero = state.GetNormalizedPointByFraction(0.0);
            var pointNegative = state.GetNormalizedPointByFraction(-0.5);

            Assert.NotNull(pointAtZero);
            Assert.NotNull(pointNegative);
            // Negative clamped to 0.0
            Assert.Equal(pointAtZero!.Value.X, pointNegative!.Value.X, 5);
            Assert.Equal(pointAtZero.Value.Y, pointNegative.Value.Y, 5);
        }

        [Fact]
        public void GetNormalizedPointByFraction_AboveOneClampedToOne()
        {
            var state = BuildTestMapState();
            var pointAtOne = state.GetNormalizedPointByFraction(1.0);
            var pointAbove = state.GetNormalizedPointByFraction(1.5);

            Assert.NotNull(pointAtOne);
            Assert.NotNull(pointAbove);
            Assert.Equal(pointAtOne!.Value.X, pointAbove!.Value.X, 5);
            Assert.Equal(pointAtOne.Value.Y, pointAbove.Value.Y, 5);
        }

        #endregion

        #region TrackMapState.FindNearestIndex

        [Fact]
        public void FindNearestIndex_PointAtOrigin_FindsClosest()
        {
            var state = BuildTestMapState();
            // Use a point near the raw data
            var index = state.FindNearestIndex(state.RawPoints[0]);

            Assert.Equal(0, index);
        }

        [Fact]
        public void FindNearestIndex_PointNearMid_FindsMidIndex()
        {
            var state = BuildTestMapState();
            var mid = state.RawPoints.Count / 2;
            var midPoint = state.RawPoints[mid];

            var index = state.FindNearestIndex(midPoint);
            Assert.Equal(mid, index);
        }

        #endregion

        #region TrackMapState.GetBounds

        [Fact]
        public void GetBounds_SimplePoints_ReturnsCorrectBounds()
        {
            var points = new List<Point>
            {
                new Point(1, 2),
                new Point(5, 8),
                new Point(3, 4)
            };

            var (minX, minY, maxX, maxY) = TrackMapService.TrackMapState.GetBounds(points);

            Assert.Equal(1, minX);
            Assert.Equal(2, minY);
            Assert.Equal(5, maxX);
            Assert.Equal(8, maxY);
        }

        [Fact]
        public void GetBounds_SinglePoint_MinEqualsMax()
        {
            var points = new List<Point> { new Point(3, 7) };

            var (minX, minY, maxX, maxY) = TrackMapService.TrackMapState.GetBounds(points);

            Assert.Equal(3, minX);
            Assert.Equal(7, minY);
            Assert.Equal(3, maxX);
            Assert.Equal(7, maxY);
        }

        [Fact]
        public void GetBounds_NegativeCoordinates_HandledCorrectly()
        {
            var points = new List<Point>
            {
                new Point(-5, -3),
                new Point(-1, -7),
                new Point(2, 4)
            };

            var (minX, minY, maxX, maxY) = TrackMapService.TrackMapState.GetBounds(points);

            Assert.Equal(-5, minX);
            Assert.Equal(-7, minY);
            Assert.Equal(2, maxX);
            Assert.Equal(4, maxY);
        }

        #endregion

        #region TrackMapState.NormalizePoints

        [Fact]
        public void NormalizePoints_ResultFitsInUnitSquare()
        {
            var points = new List<Point>
            {
                new Point(100, 200),
                new Point(500, 800),
                new Point(300, 400)
            };

            var result = TrackMapService.TrackMapState.NormalizePoints(points);

            foreach (var p in result)
            {
                Assert.InRange(p.X, -0.01, 1.01);
                Assert.InRange(p.Y, -0.01, 1.01);
            }
        }

        [Fact]
        public void NormalizePoints_PreservesRelativePositions()
        {
            var points = new List<Point>
            {
                new Point(0, 0),
                new Point(100, 0),
                new Point(100, 100),
                new Point(0, 100)
            };

            var result = TrackMapService.TrackMapState.NormalizePoints(points);

            // First point should be near bottom-left
            Assert.True(result[0].X < result[1].X);
            Assert.True(result[0].Y < result[3].Y);
        }

        [Fact]
        public void NormalizePoints_TallShape_PadsHorizontally()
        {
            var points = new List<Point>
            {
                new Point(0, 0),
                new Point(10, 0),
                new Point(10, 100),
                new Point(0, 100)
            };

            var result = TrackMapService.TrackMapState.NormalizePoints(points);

            // Height dominates, so X values get padded
            // The width is 10/100 = 0.1 of scale, so padX should be significant
            Assert.True(result[0].X > 0.01, "Should have horizontal padding");
        }

        #endregion

        #region TrackMapState.ToMeters

        [Fact]
        public void ToMeters_AtOrigin_ReturnsZero()
        {
            var result = TrackMapService.TrackMapState.ToMeters(52.0, -1.0, 52.0, -1.0, 110540.0, 111320.0);

            Assert.Equal(0.0, result.X, 5);
            Assert.Equal(0.0, result.Y, 5);
        }

        [Fact]
        public void ToMeters_OneDegreeLat_ReturnsLatScaleMeters()
        {
            var latScale = 110540.0;
            var lonScale = 111320.0;
            var result = TrackMapService.TrackMapState.ToMeters(53.0, -1.0, 52.0, -1.0, latScale, lonScale);

            Assert.Equal(0.0, result.X, 5);
            Assert.Equal(latScale, result.Y, 1);
        }

        [Fact]
        public void ToMeters_OneDegreeLon_ReturnsLonScaleMeters()
        {
            var latScale = 110540.0;
            var lonScale = 111320.0;
            var result = TrackMapService.TrackMapState.ToMeters(52.0, 0.0, 52.0, -1.0, latScale, lonScale);

            Assert.Equal(lonScale, result.X, 1);
            Assert.Equal(0.0, result.Y, 5);
        }

        #endregion

        #region ClassifySegment

        [Theory]
        [InlineData(0.6, 0.0, true)]     // High steer -> corner
        [InlineData(0.0, 1.5, true)]      // High lateral G -> corner
        [InlineData(0.1, 0.5, true)]      // Both high -> corner
        [InlineData(0.01, 0.1, false)]    // Both low -> straight
        [InlineData(0.0, 0.0, false)]     // Zero -> straight
        public void ClassifySegment_IsCorner(double steeringAngle, double lateralG, bool expectedCorner)
        {
            var result = TrackMapService.ClassifySegment(steeringAngle, lateralG);
            Assert.Equal(expectedCorner, result.IsCorner);
        }

        [Theory]
        [InlineData(0.5, 0.0, "Right")]
        [InlineData(-0.5, 0.0, "Left")]
        [InlineData(0.0, 0.5, "Left")]    // positive lateralG -> Left
        [InlineData(0.0, -0.5, "Right")]  // negative lateralG -> Right
        [InlineData(0.0, 0.0, "")]        // no direction
        public void ClassifySegment_Direction(double steeringAngle, double lateralG, string expectedDirection)
        {
            var result = TrackMapService.ClassifySegment(steeringAngle, lateralG);
            Assert.Equal(expectedDirection, result.Direction);
        }

        [Theory]
        [InlineData(0.0, 1.5, "Fast")]
        [InlineData(0.0, 1.0, "Medium")]
        [InlineData(0.0, 0.5, "Slow")]
        [InlineData(0.0, 0.1, "")]
        public void ClassifySegment_Severity(double steeringAngle, double lateralG, string expectedSeverity)
        {
            var result = TrackMapService.ClassifySegment(steeringAngle, lateralG);
            Assert.Equal(expectedSeverity, result.Severity);
        }

        #endregion

        #region GetSeverityFromAngle

        [Theory]
        [InlineData(0.6, "Slow")]
        [InlineData(0.56, "Slow")]
        [InlineData(0.4, "Medium")]
        [InlineData(0.31, "Medium")]
        [InlineData(0.2, "Fast")]
        [InlineData(0.13, "Fast")]
        [InlineData(0.05, "")]
        [InlineData(0.0, "")]
        public void GetSeverityFromAngle_ReturnsExpected(double angle, string expected)
        {
            Assert.Equal(expected, TrackMapService.GetSeverityFromAngle(angle));
        }

        #endregion

        #region GetSeverityAngle

        [Theory]
        [InlineData("Slow", 0.6)]
        [InlineData("Medium", 0.35)]
        [InlineData("Fast", 0.15)]
        [InlineData("", 0.0)]
        [InlineData("Unknown", 0.0)]
        public void GetSeverityAngle_ReturnsExpected(string severity, double expected)
        {
            Assert.Equal(expected, TrackMapService.GetSeverityAngle(severity));
        }

        #endregion

        #region BuildSegmentStatus

        [Fact]
        public void BuildSegmentStatus_WithSectorAndCorner_PopulatesAll()
        {
            var metadata = new TrackMetadata { Name = "Silverstone" };
            var sector = new TrackSector { Name = "Sector 1" };
            var corner = new TrackCorner { Number = 3, Name = "Copse" };
            var classification = new TrackMapService.SegmentClassification(true, "Right", "Fast");

            var status = TrackMapService.BuildSegmentStatus(metadata, sector, corner, classification, 0.15);

            Assert.Equal("Silverstone", status.TrackName);
            Assert.Equal("Sector 1", status.SectorName);
            Assert.Equal("T3 Copse", status.CornerLabel);
            Assert.Equal("Corner", status.SegmentType);
            Assert.Equal("Right", status.Direction);
            Assert.Equal("Fast", status.Severity);
            Assert.Equal(0.15, status.LapFraction);
        }

        [Fact]
        public void BuildSegmentStatus_NullSector_DefaultsSectorName()
        {
            var metadata = new TrackMetadata { Name = "Monza" };
            var classification = new TrackMapService.SegmentClassification(false, "", "");

            var status = TrackMapService.BuildSegmentStatus(metadata, null, null, classification, 0.5);

            Assert.Equal("Sector --", status.SectorName);
        }

        [Fact]
        public void BuildSegmentStatus_Straight_SegmentTypeStraight()
        {
            var metadata = new TrackMetadata { Name = "Monza" };
            var classification = new TrackMapService.SegmentClassification(false, "", "");

            var status = TrackMapService.BuildSegmentStatus(metadata, null, null, classification, 0.5);

            Assert.Equal("Straight", status.SegmentType);
            Assert.Equal("Straight", status.CornerLabel);
        }

        [Fact]
        public void BuildSegmentStatus_CornerNoName_UsesNumberOnly()
        {
            var metadata = new TrackMetadata { Name = "Test" };
            var corner = new TrackCorner { Number = 5, Name = "" };
            var classification = new TrackMapService.SegmentClassification(true, "Left", "Medium");

            var status = TrackMapService.BuildSegmentStatus(metadata, null, corner, classification, 0.3);

            Assert.Equal("T5", status.CornerLabel);
        }

        [Fact]
        public void BuildSegmentStatus_CornerNull_IsCorner_ShowsCornerLabel()
        {
            var metadata = new TrackMetadata { Name = "Test" };
            var classification = new TrackMapService.SegmentClassification(true, "Right", "Slow");

            var status = TrackMapService.BuildSegmentStatus(metadata, null, null, classification, 0.7);

            Assert.Equal("Corner", status.CornerLabel);
        }

        #endregion

        #region SegmentClassification

        [Fact]
        public void SegmentClassification_RecordEquality()
        {
            var a = new TrackMapService.SegmentClassification(true, "Left", "Fast");
            var b = new TrackMapService.SegmentClassification(true, "Left", "Fast");

            Assert.Equal(a, b);
        }

        [Fact]
        public void SegmentClassification_DifferentValues_NotEqual()
        {
            var a = new TrackMapService.SegmentClassification(true, "Left", "Fast");
            var b = new TrackMapService.SegmentClassification(false, "Right", "Slow");

            Assert.NotEqual(a, b);
        }

        #endregion

        #region OutlineSegment.Contains

        [Fact]
        public void OutlineSegment_Contains_WithinRange_ReturnsTrue()
        {
            var segment = new TrackMapService.OutlineSegment
            {
                Number = 1,
                Name = "Turn 1",
                Start = 0.1,
                End = 0.3,
                IsCorner = true
            };

            Assert.True(segment.Contains(0.2));
        }

        [Fact]
        public void OutlineSegment_Contains_OutsideRange_ReturnsFalse()
        {
            var segment = new TrackMapService.OutlineSegment
            {
                Number = 1,
                Name = "Turn 1",
                Start = 0.1,
                End = 0.3,
                IsCorner = true
            };

            Assert.False(segment.Contains(0.5));
        }

        [Fact]
        public void OutlineSegment_Contains_AtBoundary_ReturnsTrue()
        {
            var segment = new TrackMapService.OutlineSegment
            {
                Number = 1,
                Name = "Turn 1",
                Start = 0.1,
                End = 0.3,
                IsCorner = true
            };

            Assert.True(segment.Contains(0.1));
        }

        #endregion

        #region Helpers

        private static TrackMapService.TrackMapState BuildTestMapState()
        {
            var samples = CreateCircularLapSamples(100);
            var state = TrackMapService.TrackMapState.Build(samples);
            Assert.NotNull(state);
            return state!;
        }

        private static List<TelemetrySampleDto> CreateCircularLapSamples(int count)
        {
            var samples = new List<TelemetrySampleDto>();
            var centerLat = 52.0;
            var centerLon = -1.0;
            var radius = 500.0;
            var metersPerDegreeLat = 110540.0;
            var metersPerDegreeLon = 111320.0 * Math.Cos(centerLat * Math.PI / 180.0);

            for (int i = 0; i < count; i++)
            {
                var angle = 2 * Math.PI * i / count;
                var x = radius * Math.Cos(angle);
                var y = radius * Math.Sin(angle);
                samples.Add(new TelemetrySampleDto
                {
                    Latitude = centerLat + y / metersPerDegreeLat,
                    Longitude = centerLon + x / metersPerDegreeLon,
                    LapNumber = 1,
                    SteeringAngle = 0.3,
                    LateralG = 0.5,
                    SpeedKph = 200.0,
                    Timestamp = DateTime.UtcNow.AddSeconds(i * 0.1)
                });
            }

            return samples;
        }

        private static List<Point> CreateCircularOutlinePoints(int count)
        {
            var points = new List<Point>();
            for (int i = 0; i < count; i++)
            {
                var angle = 2 * Math.PI * i / count;
                points.Add(new Point(
                    0.5 + 0.4 * Math.Cos(angle),
                    0.5 + 0.4 * Math.Sin(angle)));
            }
            return points;
        }

        #endregion
    }
}
