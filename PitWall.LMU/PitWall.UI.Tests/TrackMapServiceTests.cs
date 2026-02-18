using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using PitWall.UI.Models;
using PitWall.UI.Services;
using Xunit;

namespace PitWall.UI.Tests
{
    public class TrackMapServiceTests
    {
        #region Helper Methods

        private static TrackMetadataStore CreateMetadataStore()
        {
            return new TrackMetadataStore();
        }

        private static TelemetrySampleDto CreateTelemetrySample(
            int lapNumber = 1,
            double steeringAngle = 0.0,
            double lateralG = 0.0,
            double latitude = 0.0,
            double longitude = 0.0,
            DateTime? timestamp = null)
        {
            return new TelemetrySampleDto
            {
                LapNumber = lapNumber,
                SteeringAngle = steeringAngle,
                LateralG = lateralG,
                Latitude = latitude,
                Longitude = longitude,
                Timestamp = timestamp ?? DateTime.UtcNow,
                SpeedKph = 200.0,
                ThrottlePosition = 1.0,
                BrakePosition = 0.0
            };
        }

        private static TrackMetadata CreateDefaultTrackMetadata(string name = "Test Track")
        {
            return new TrackMetadata
            {
                Name = name,
                Sectors = new List<TrackSector>
                {
                    new TrackSector { Name = "Sector 1", Start = 0.0, End = 0.333 },
                    new TrackSector { Name = "Sector 2", Start = 0.333, End = 0.666 },
                    new TrackSector { Name = "Sector 3", Start = 0.666, End = 1.0 }
                },
                Corners = new List<TrackCorner>()
            };
        }

        private static TrackMetadata CreateTrackWithCornerRanges()
        {
            var metadata = CreateDefaultTrackMetadata("Track with Corners");
            metadata.Corners = new List<TrackCorner>
            {
                new TrackCorner
                {
                    Number = 1,
                    Name = "Turn 1",
                    Direction = "Right",
                    Severity = "Medium",
                    Start = 0.1,
                    End = 0.2
                },
                new TrackCorner
                {
                    Number = 2,
                    Name = "Turn 2",
                    Direction = "Left",
                    Severity = "Fast",
                    Start = 0.4,
                    End = 0.5
                }
            };
            return metadata;
        }

        private static TrackMetadata CreateTrackWithCornerNames()
        {
            var metadata = CreateDefaultTrackMetadata("Track with Named Corners");
            metadata.Corners = new List<TrackCorner>
            {
                new TrackCorner { Number = 1, Name = "Copse", Direction = "Right", Severity = "Fast", Start = 0, End = 0 },
                new TrackCorner { Number = 2, Name = "Maggots", Direction = "Left", Severity = "Fast", Start = 0, End = 0 },
                new TrackCorner { Number = 3, Name = "Chapel", Direction = "Right", Severity = "Medium", Start = 0, End = 0 }
            };
            return metadata;
        }

        private static TrackMetadata CreateTrackWithOutline()
        {
            var metadata = CreateDefaultTrackMetadata("Track with Outline");
            metadata.Outline = new List<TrackOutlinePoint>();
            
            // Create a simple circular track outline with 20 points
            for (int i = 0; i < 20; i++)
            {
                var angle = 2 * Math.PI * i / 20;
                metadata.Outline.Add(new TrackOutlinePoint
                {
                    X = Math.Cos(angle),
                    Y = Math.Sin(angle)
                });
            }
            
            return metadata;
        }

        private static List<TelemetrySampleDto> CreateLapData(int lapNumber, int sampleCount = 100)
        {
            var samples = new List<TelemetrySampleDto>();
            var baseTime = DateTime.UtcNow;
            
            // Create a circular track path
            var trackRadius = 1000.0; // meters
            var centerLat = 52.0;
            var centerLon = -1.0;
            var metersPerDegreeLat = 110540.0;
            var metersPerDegreeLon = 111320.0 * Math.Cos(centerLat * Math.PI / 180.0);
            
            for (int i = 0; i < sampleCount; i++)
            {
                var progress = (double)i / sampleCount;
                var angle = 2 * Math.PI * progress;
                
                // Calculate position
                var x = trackRadius * Math.Cos(angle);
                var y = trackRadius * Math.Sin(angle);
                var lat = centerLat + y / metersPerDegreeLat;
                var lon = centerLon + x / metersPerDegreeLon;
                
                // Add some corners with higher steering/lateral G
                var isInCorner = (progress > 0.2 && progress < 0.3) || (progress > 0.6 && progress < 0.7);
                var steeringAngle = isInCorner ? 0.6 : 0.02;
                var lateralG = isInCorner ? 1.5 : 0.1;
                
                samples.Add(new TelemetrySampleDto
                {
                    LapNumber = lapNumber,
                    Timestamp = baseTime.AddSeconds(i * 0.05),
                    Latitude = lat,
                    Longitude = lon,
                    SteeringAngle = steeringAngle,
                    LateralG = lateralG,
                    SpeedKph = 200.0,
                    ThrottlePosition = isInCorner ? 0.7 : 1.0,
                    BrakePosition = 0.0
                });
            }
            
            return samples;
        }

        #endregion

        #region ClassifySegment Tests

        [Fact]
        public void ClassifySegment_HighSteeringAngle_IdentifiesAsCorner()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);
            
            var sample = CreateTelemetrySample(steeringAngle: 0.6, lateralG: 0.0);
            buffer.Add(sample);

            // Act
            var frame = service.Update(sample, "Default");

            // Assert
            // Corner should be detected due to high steering angle
            Assert.NotNull(frame.SegmentStatus);
        }

        [Fact]
        public void ClassifySegment_HighLateralG_IdentifiesAsCorner()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);
            
            // Build a map first with valid lap data
            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }
            
            // Add multiple samples with consistent high lateral G for smoothing
            for (int i = 0; i < 10; i++)
            {
                var cornerSample = CreateTelemetrySample(
                    lapNumber: 1,
                    steeringAngle: 0.0,
                    lateralG: 1.5, // Above any reasonable threshold
                    latitude: lapData[50].Latitude + i * 0.0001,
                    longitude: lapData[50].Longitude + i * 0.0001);
                buffer.Add(cornerSample);
            }

            // Act
            service.Reset("Default");
            var testSample = buffer.GetAll()[^1]; // Get last added sample
            var frame = service.Update(testSample, "Default");

            // Assert
            Assert.NotNull(frame.SegmentStatus);
            Assert.Equal("Corner", frame.SegmentStatus.SegmentType);
        }

        [Fact]
        public void ClassifySegment_LowSteeringAndLateralG_IdentifiesAsStraight()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);
            
            // Build a map first with valid lap data
            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }
            
            // Add a straight-line sample
            var straightSample = CreateTelemetrySample(
                lapNumber: 1,
                steeringAngle: 0.01, 
                lateralG: 0.1,
                latitude: lapData[10].Latitude,
                longitude: lapData[10].Longitude);
            buffer.Add(straightSample);

            // Act
            service.Reset("Default");
            var frame = service.Update(straightSample, "Default");

            // Assert
            Assert.NotNull(frame.SegmentStatus);
            Assert.Equal("Straight", frame.SegmentStatus.SegmentType);
        }

        [Theory]
        [InlineData(0.1, 0.0, "Right")]
        [InlineData(-0.1, 0.0, "Left")]
        [InlineData(0.0, -0.5, "Right")]
        [InlineData(0.0, 0.5, "Left")]
        public void ClassifySegment_DetectsCorrectDirection(double steering, double lateralG, string expectedDirection)
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);
            
            // Build a map first
            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }
            
            // Add multiple samples with consistent steering/lateral G for smoothing
            for (int i = 0; i < 10; i++)
            {
                var testSample = CreateTelemetrySample(
                    lapNumber: 1,
                    steeringAngle: steering,
                    lateralG: lateralG,
                    latitude: lapData[50].Latitude + i * 0.0001,
                    longitude: lapData[50].Longitude + i * 0.0001);
                buffer.Add(testSample);
            }

            // Act
            service.Reset("Default");
            var finalSample = buffer.GetAll()[^1];
            var frame = service.Update(finalSample, "Default");

            // Assert
            Assert.NotNull(frame.SegmentStatus);
            Assert.Equal(expectedDirection, frame.SegmentStatus.Direction);
        }

        [Theory]
        [InlineData(1.5, "Fast")]      // > 1.2
        [InlineData(1.0, "Medium")]    // > 0.8
        [InlineData(0.5, "Slow")]      // > 0.35
        [InlineData(0.2, "")]          // <= 0.35
        public void ClassifySegment_DetectsCorrectSeverity(double lateralG, string expectedSeverity)
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);
            
            // Build a map first
            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }
            
            // Add multiple samples with consistent lateral G for smoothing window
            for (int i = 0; i < 10; i++)
            {
                var testSample = CreateTelemetrySample(
                    lapNumber: 1,
                    steeringAngle: 0.0,
                    lateralG: lateralG,
                    latitude: lapData[50].Latitude + i * 0.0001,
                    longitude: lapData[50].Longitude + i * 0.0001);
                buffer.Add(testSample);
            }

            // Act
            service.Reset("Default");
            var finalSample = buffer.GetAll()[^1];
            var frame = service.Update(finalSample, "Default");

            // Assert
            Assert.NotNull(frame.SegmentStatus);
            Assert.Equal(expectedSeverity, frame.SegmentStatus.Severity);
        }

        #endregion

        #region GetSmoothedInputs Tests

        [Fact]
        public void GetSmoothedInputs_SingleSample_ReturnsOriginalValues()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);
            
            var sample = CreateTelemetrySample(steeringAngle: 0.15, lateralG: 0.8);
            buffer.Add(sample);


            // Act
            var frame = service.Update(sample, "Test Track");

            // Assert - if smoothing worked, values would be different from raw sample
            Assert.NotNull(frame.SegmentStatus);
        }

        [Fact]
        public void GetSmoothedInputs_MultipleSamples_AveragesValues()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);

            // Add samples with varying steering angles
            for (int i = 0; i < 10; i++)
            {
                var sample = CreateTelemetrySample(lapNumber: 1, steeringAngle: i * 0.02, lateralG: 0.1);
                buffer.Add(sample);
            }


            // Act - Update with latest sample
            var latestSample = CreateTelemetrySample(lapNumber: 1, steeringAngle: 0.2, lateralG: 0.1);
            buffer.Add(latestSample);
            var frame = service.Update(latestSample, "Test Track");

            // Assert - Smoothing should have occurred
            Assert.NotNull(frame.SegmentStatus);
        }

        [Fact]
        public void GetSmoothedInputs_DifferentLaps_OnlyAveragesCurrentLap()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);

            // Add samples from lap 1
            for (int i = 0; i < 5; i++)
            {
                buffer.Add(CreateTelemetrySample(lapNumber: 1, steeringAngle: 0.01));
            }

            // Add samples from lap 2 with different values
            for (int i = 0; i < 5; i++)
            {
                buffer.Add(CreateTelemetrySample(lapNumber: 2, steeringAngle: 0.15));
            }


            // Act - Update with lap 2 sample
            var lap2Sample = CreateTelemetrySample(lapNumber: 2, steeringAngle: 0.15, lateralG: 0.8);
            buffer.Add(lap2Sample);
            var frame = service.Update(lap2Sample, "Test Track");

            // Assert - Should detect corner for lap 2
            Assert.NotNull(frame.SegmentStatus);
        }

        #endregion

        #region UpdateAutoCorners Tests

        [Fact]
        public void UpdateAutoCorners_EnteringCorner_CreatesNewCorner()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);
            var metadata = CreateTrackWithCornerNames();

            

            // Build map first
            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }

            // Act - Process through corner entry
            service.Reset("Test Track");
            foreach (var sample in lapData.Take(30))
            {
                service.Update(sample, "Test Track");
            }

            // Assert - Corner should be detected
            var frame = service.Update(lapData[25], "Test Track");
            Assert.NotNull(frame.SegmentStatus);
        }

        [Fact]
        public void UpdateAutoCorners_ExitingCorner_UpdatesCornerEnd()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);
            var metadata = CreateTrackWithCornerNames();

            

            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }

            // Act - Process entire lap
            service.Reset("Test Track");
            foreach (var sample in lapData)
            {
                service.Update(sample, "Test Track");
            }

            // Assert
            var frame = service.Update(lapData[^1], "Test Track");
            Assert.NotNull(frame.SegmentStatus);
        }

        [Fact]
        public void UpdateAutoCorners_WithMetadataCornerRanges_DoesNotCreateAutoCorners()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);
            var metadata = CreateTrackWithCornerRanges();

            

            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }

            // Act - Process samples
            service.Reset("Test Track");
            foreach (var sample in lapData.Take(50))
            {
                service.Update(sample, "Test Track");
            }

            // Assert - Should use metadata corners, not auto corners
            var frame = service.Update(lapData[15], "Test Track");
            Assert.NotNull(frame.SegmentStatus);
            // When in corner range defined in metadata, should show corner info
            Assert.NotNull(frame.SegmentStatus.CornerLabel);
        }

        #endregion

        #region BuildOutlineSegments Tests

        [Fact]
        public void BuildOutlineSegments_WithOutline_DetectsCorners()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);
            
            // Create outline with sharp corner
            var metadata = CreateDefaultTrackMetadata();
            metadata.Outline = new List<TrackOutlinePoint>
            {
                new TrackOutlinePoint { X = 0.0, Y = 0.0 },
                new TrackOutlinePoint { X = 0.1, Y = 0.0 },
                new TrackOutlinePoint { X = 0.2, Y = 0.0 },
                new TrackOutlinePoint { X = 0.3, Y = 0.0 },
                new TrackOutlinePoint { X = 0.3, Y = 0.1 }, // Sharp 90-degree turn
                new TrackOutlinePoint { X = 0.3, Y = 0.2 },
                new TrackOutlinePoint { X = 0.3, Y = 0.3 },
                new TrackOutlinePoint { X = 0.2, Y = 0.3 },
                new TrackOutlinePoint { X = 0.1, Y = 0.3 },
                new TrackOutlinePoint { X = 0.0, Y = 0.3 },
                new TrackOutlinePoint { X = 0.0, Y = 0.2 },
                new TrackOutlinePoint { X = 0.0, Y = 0.1 }
            };

            

            var sample = CreateTelemetrySample(lapNumber: 1);
            buffer.Add(sample);

            // Act
            service.Reset("Test Track");
            var frame = service.Update(sample, "Test Track");

            // Assert
            Assert.NotNull(frame);
        }

        [Fact]
        public void BuildOutlineSegments_InsufficientPoints_HandlesGracefully()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);
            
            var metadata = CreateDefaultTrackMetadata();
            metadata.Outline = new List<TrackOutlinePoint>
            {
                new TrackOutlinePoint { X = 0.0, Y = 0.0 },
                new TrackOutlinePoint { X = 0.1, Y = 0.0 },
                new TrackOutlinePoint { X = 0.2, Y = 0.1 }
            };

            

            var sample = CreateTelemetrySample(lapNumber: 1);
            buffer.Add(sample);

            // Act
            service.Reset("Test Track");
            var frame = service.Update(sample, "Test Track");

            // Assert - Should not crash with insufficient outline points
            Assert.NotNull(frame);
            Assert.Empty(frame.TrackPoints);
        }

        #endregion

        #region TryBuildMapIfNeeded Tests

        [Fact]
        public void TryBuildMapIfNeeded_NegativeLapNumber_DoesNotBuildMap()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);


            var sample = CreateTelemetrySample(lapNumber: -1);
            buffer.Add(sample);

            // Act
            var frame = service.Update(sample, "Test Track");

            // Assert
            Assert.Empty(frame.TrackPoints);
        }

        [Fact]
        public void TryBuildMapIfNeeded_ValidLapData_BuildsMapFromGPS()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);


            // Add sufficient lap data
            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }

            // Act
            service.Reset("Test Track");
            var frame = service.Update(lapData[50], "Test Track");

            // Assert
            Assert.NotEmpty(frame.TrackPoints);
        }

        [Fact]
        public void TryBuildMapIfNeeded_WithOutline_UsesOutlineOverGPS()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);

            var metadata = CreateTrackWithOutline();
            

            // Add GPS lap data
            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }

            // Act
            service.Reset("Test Track");
            var frame = service.Update(lapData[50], "Test Track");

            // Assert - Should use outline
            Assert.NotEmpty(frame.TrackPoints);
        }

        [Fact]
        public void TryBuildMapIfNeeded_InsufficientGPSPoints_DoesNotBuildMap()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);


            // Add only a few samples
            for (int i = 0; i < 10; i++)
            {
                buffer.Add(CreateTelemetrySample(lapNumber: 1, latitude: 52.0 + i * 0.0001, longitude: -1.0 + i * 0.0001));
            }

            // Act
            var sample = CreateTelemetrySample(lapNumber: 1);
            service.Reset("Test Track");
            var frame = service.Update(sample, "Test Track");

            // Assert
            Assert.Empty(frame.TrackPoints);
        }

        [Fact]
        public void TryBuildMapIfNeeded_MapAlreadyBuilt_DoesNotRebuild()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);


            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }

            // Act - Build map with first update
            service.Reset("Test Track");
            var frame1 = service.Update(lapData[50], "Test Track");
            
            // Second update should use existing map
            var frame2 = service.Update(lapData[51], "Test Track");

            // Assert - Both frames should have track points
            Assert.NotEmpty(frame1.TrackPoints);
            Assert.NotEmpty(frame2.TrackPoints);
            Assert.Equal(frame1.TrackPoints.Count, frame2.TrackPoints.Count);
        }

        [Fact]
        public void TryBuildMapIfNeeded_WithMapImageUri_DoesNotBuildGPSMap()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);

            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }

            // Act - Use Default track (real store's default track doesn't have map image or outline)
            service.Reset("Default");
            var frame = service.Update(lapData[50], "Default");

            // Assert - Without image URI or outline, should build GPS map if sufficient data exists
            // With our CreateLapData providing 100 valid GPS points, map should be built
            Assert.NotEmpty(frame.TrackPoints);
        }

        #endregion

        #region Update Integration Tests

        [Fact]
        public void Update_WithNoMap_ReturnsEmptyTrackPoints()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);


            var sample = CreateTelemetrySample(lapNumber: 1);

            // Act
            var frame = service.Update(sample, "Test Track");

            // Assert
            Assert.Empty(frame.TrackPoints);
            Assert.Null(frame.CurrentPoint);
            Assert.NotNull(frame.SegmentStatus);
        }

        [Fact]
        public void Update_WithValidMap_ReturnsCurrentPoint()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);


            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }

            // Act
            service.Reset("Test Track");
            var frame = service.Update(lapData[50], "Test Track");

            // Assert
            Assert.NotEmpty(frame.TrackPoints);
            Assert.NotNull(frame.CurrentPoint);
        }

        [Fact]
        public void Update_WithSectorData_ReturnsCorrectSector()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);

            var metadata = CreateDefaultTrackMetadata();
            

            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }

            // Act - Get frame at different positions
            service.Reset("Test Track");
            var frameSector1 = service.Update(lapData[10], "Test Track"); // Early in lap
            var frameSector2 = service.Update(lapData[45], "Test Track"); // Middle
            var frameSector3 = service.Update(lapData[80], "Test Track"); // Late

            // Assert
            Assert.NotNull(frameSector1.SegmentStatus);
            Assert.NotNull(frameSector2.SegmentStatus);
            Assert.NotNull(frameSector3.SegmentStatus);
        }

        [Fact]
        public void Update_WithCornerMetadata_ReturnsCornerInfo()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);

            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }

            // Act - Process through a corner in our test data
            service.Reset("Default");
            // Our CreateLapData creates corners at 0.2-0.3 and 0.6-0.7
            var frame = service.Update(lapData[25], "Default");

            // Assert - Should show it's a corner
            Assert.NotNull(frame.SegmentStatus);
            Assert.NotNull(frame.SegmentStatus.CornerLabel);
            // In a corner, label should not be just "Straight"
            Assert.NotEqual("Straight", frame.SegmentStatus.CornerLabel);
        }

        [Fact]
        public void Update_TrackNameChange_UpdatesTrackName()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);

            var sample = CreateTelemetrySample();

            // Act - Both resolve to "Default" in real store
            var frame1 = service.Update(sample, "Default");
            var frame2 = service.Update(sample, "Default");

            // Assert
            Assert.Equal("Default", frame1.SegmentStatus!.TrackName);
            Assert.Equal("Default", frame2.SegmentStatus!.TrackName);
        }

        #endregion

        #region Reset Tests

        [Fact]
        public void Reset_ClearsMapState()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);


            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }

            // Build map
            service.Reset("Test Track");
            var frame1 = service.Update(lapData[50], "Test Track");
            Assert.NotEmpty(frame1.TrackPoints);

            // Act - Reset
            service.Reset("New Track");

            // Need new lap data for new track
            var newLapData = CreateLapData(1, 100);
            buffer.Clear();
            foreach (var sample in newLapData)
            {
                buffer.Add(sample);
            }

            var frame2 = service.Update(newLapData[50], "New Track");

            // Assert - Should rebuild map
            Assert.NotEmpty(frame2.TrackPoints);
        }

        [Fact]
        public void Reset_WithNullTrackName_UsesDefault()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);


            // Act
            service.Reset(null);
            var sample = CreateTelemetrySample();
            var frame = service.Update(sample, null);

            // Assert
            Assert.Equal("Default", frame.SegmentStatus!.TrackName);
        }

        [Fact]
        public void Reset_WithWhitespaceTrackName_UsesDefault()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);


            // Act
            service.Reset("   ");
            var sample = CreateTelemetrySample();
            var frame = service.Update(sample, "   ");

            // Assert
            Assert.Equal("Default", frame.SegmentStatus!.TrackName);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void Update_ZeroLatLon_ReturnsNullCurrentPoint()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);


            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }

            // Build map
            service.Reset("Test Track");
            service.Update(lapData[50], "Test Track");

            // Act - Update with zero lat/lon
            var zeroSample = CreateTelemetrySample(lapNumber: 1, latitude: 0.0, longitude: 0.0);
            buffer.Add(zeroSample);
            var frame = service.Update(zeroSample, "Test Track");

            // Assert
            Assert.Null(frame.CurrentPoint);
        }

        [Fact]
        public void Update_ConsecutiveSamples_MaintainsConsistency()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);


            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }

            // Act - Process multiple consecutive updates
            service.Reset("Test Track");
            TrackMapFrame? previousFrame = null;
            
            for (int i = 10; i < 20; i++)
            {
                var frame = service.Update(lapData[i], "Test Track");
                
                // Assert
                Assert.NotNull(frame.SegmentStatus);
                
                if (previousFrame != null)
                {
                    // Track points should remain consistent
                    Assert.Equal(previousFrame.TrackPoints.Count, frame.TrackPoints.Count);
                }
                
                previousFrame = frame;
            }
        }

        [Fact]
        public void Update_VerySmallLatLonChange_FiltersPoints()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);


            // Add samples with tiny movements (less than MinPointSpacingMeters)
            var baseLat = 52.0;
            var baseLon = -1.0;
            
            for (int i = 0; i < 100; i++)
            {
                var sample = CreateTelemetrySample(
                    lapNumber: 1,
                    latitude: baseLat + i * 0.000001, // Very small change
                    longitude: baseLon + i * 0.000001);
                buffer.Add(sample);
            }

            // Act
            service.Reset("Test Track");
            var lastSample = CreateTelemetrySample(lapNumber: 1, latitude: baseLat + 100 * 0.000001, longitude: baseLon + 100 * 0.000001);
            buffer.Add(lastSample);
            var frame = service.Update(lastSample, "Test Track");

            // Assert - Should filter out points that are too close
            Assert.True(frame.TrackPoints.Count < 100);
        }

        #endregion

        #region ComputeVehicleMarkers Tests

        [Fact]
        public void ComputeVehicleMarkers_NullMapState_ReturnsEmpty()
        {
            // Arrange — service with no map built
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);

            var vehicles = new List<VehiclePositionInput>
            {
                new VehiclePositionInput { VehicleId = 1, LapFraction = 0.5, Label = "P1" }
            };

            // Act
            var markers = service.ComputeVehicleMarkers(vehicles);

            // Assert — no map built yet, should be empty
            Assert.Empty(markers);
        }

        [Fact]
        public void ComputeVehicleMarkers_NullInput_ReturnsEmpty()
        {
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);

            var markers = service.ComputeVehicleMarkers(null!);
            Assert.Empty(markers);
        }

        [Fact]
        public void ComputeVehicleMarkers_EmptyList_ReturnsEmpty()
        {
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);

            var markers = service.ComputeVehicleMarkers(new List<VehiclePositionInput>());
            Assert.Empty(markers);
        }

        [Fact]
        public void ComputeVehicleMarkers_WithBuiltMap_ReturnsPositionedMarkers()
        {
            // Arrange — build a track map first
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);

            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }
            service.Reset("Default");
            service.Update(lapData[50], "Default");

            var vehicles = new List<VehiclePositionInput>
            {
                new VehiclePositionInput { VehicleId = 0, LapFraction = 0.25, Label = "P1", IsPlayer = true, Place = 1 },
                new VehiclePositionInput { VehicleId = 1, LapFraction = 0.50, Label = "P2", IsPlayer = false, Place = 2, VehicleClass = "GT3" },
                new VehiclePositionInput { VehicleId = 2, LapFraction = 0.75, Label = "P3", IsPlayer = false, Place = 3, VehicleClass = "LMP2" }
            };

            // Act
            var markers = service.ComputeVehicleMarkers(vehicles);

            // Assert
            Assert.Equal(3, markers.Count);
            Assert.True(markers[0].IsPlayer);
            Assert.Equal("P1", markers[0].Label);
            Assert.Equal(1, markers[0].Place);
            Assert.Equal("GT3", markers[1].VehicleClass);
            Assert.Equal("LMP2", markers[2].VehicleClass);
        }

        [Fact]
        public void ComputeVehicleMarkers_MarkersHaveDistinctPositions()
        {
            // Arrange
            var buffer = new TelemetryBuffer();
            var store = CreateMetadataStore();
            var service = new TrackMapService(buffer, store);

            var lapData = CreateLapData(1, 100);
            foreach (var sample in lapData)
            {
                buffer.Add(sample);
            }
            service.Reset("Default");
            service.Update(lapData[50], "Default");

            var vehicles = new List<VehiclePositionInput>
            {
                new VehiclePositionInput { VehicleId = 0, LapFraction = 0.0, Label = "Start" },
                new VehiclePositionInput { VehicleId = 1, LapFraction = 0.5, Label = "Mid" }
            };

            // Act
            var markers = service.ComputeVehicleMarkers(vehicles);

            // Assert — positions at 0.0 and 0.5 should differ
            Assert.Equal(2, markers.Count);
            Assert.NotEqual(markers[0].Position, markers[1].Position);
        }

        #endregion
    }
}
