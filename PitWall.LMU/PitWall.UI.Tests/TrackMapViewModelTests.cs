using System.Collections.Generic;
using Avalonia;
using PitWall.UI.Models;
using PitWall.UI.ViewModels;
using Xunit;

namespace PitWall.UI.Tests
{
    public class TrackMapViewModelTests
    {
        [Fact]
        public void UpdateFrame_SetsTrackPoints()
        {
            var vm = new TrackMapViewModel();
            var points = new List<Point> { new(0.1, 0.2), new(0.3, 0.4) };
            var frame = new TrackMapFrame { TrackPoints = points };

            vm.UpdateFrame(frame);

            Assert.Equal(2, vm.TrackPoints.Count);
        }

        [Fact]
        public void UpdateFrame_SetsCurrentPoint()
        {
            var vm = new TrackMapViewModel();
            var expected = new Point(0.5, 0.6);
            var frame = new TrackMapFrame { CurrentPoint = expected };

            vm.UpdateFrame(frame);

            Assert.Equal(expected, vm.CurrentPoint);
        }

        [Fact]
        public void UpdateFrame_SetsVehicleMarkers()
        {
            var vm = new TrackMapViewModel();
            var markers = new List<CarMapMarker>
            {
                new CarMapMarker { VehicleId = 0, Position = new Point(0.1, 0.2), IsPlayer = true, Label = "P1" },
                new CarMapMarker { VehicleId = 1, Position = new Point(0.5, 0.6), IsPlayer = false, Label = "P2" }
            };
            var frame = new TrackMapFrame { VehicleMarkers = markers };

            vm.UpdateFrame(frame);

            Assert.Equal(2, vm.VehicleMarkers.Count);
            Assert.True(vm.VehicleMarkers[0].IsPlayer);
            Assert.Equal("P2", vm.VehicleMarkers[1].Label);
        }

        [Fact]
        public void UpdateFrame_EmptyVehicleMarkers_SetsEmptyList()
        {
            var vm = new TrackMapViewModel();
            var frame = new TrackMapFrame();

            vm.UpdateFrame(frame);

            Assert.Empty(vm.VehicleMarkers);
        }

        [Fact]
        public void UpdateFrame_SetsSegmentStatus()
        {
            var vm = new TrackMapViewModel();
            var frame = new TrackMapFrame
            {
                SegmentStatus = new TrackSegmentStatus
                {
                    TrackName = "Monza",
                    SectorName = "S1",
                    CornerLabel = "Variante del Rettifilo",
                    SegmentType = "Corner"
                }
            };

            vm.UpdateFrame(frame);

            Assert.Equal("Monza", vm.TrackName);
            Assert.Equal("S1", vm.SectorLabel);
            Assert.Equal("Variante del Rettifilo", vm.CornerLabel);
            Assert.Equal("Corner", vm.SegmentType);
        }

        [Fact]
        public void UpdateFrame_NullSegmentStatus_KeepsDefaults()
        {
            var vm = new TrackMapViewModel();
            var frame = new TrackMapFrame { SegmentStatus = null };

            vm.UpdateFrame(frame);

            Assert.Equal("TRACK", vm.TrackName);
            Assert.Equal("--", vm.SectorLabel);
        }

        [Fact]
        public void UpdateFrame_SetsMapImageUri()
        {
            var vm = new TrackMapViewModel();
            var frame = new TrackMapFrame { MapImageUri = "avares://PitWall.UI/Assets/track.png" };

            vm.UpdateFrame(frame);

            Assert.Equal("avares://PitWall.UI/Assets/track.png", vm.MapImageUri);
        }

        [Fact]
        public void InitialState_HasDefaults()
        {
            var vm = new TrackMapViewModel();

            Assert.Empty(vm.TrackPoints);
            Assert.Null(vm.CurrentPoint);
            Assert.Empty(vm.VehicleMarkers);
            Assert.Equal("TRACK", vm.TrackName);
            Assert.Equal("--", vm.SectorLabel);
            Assert.Equal("--", vm.CornerLabel);
            Assert.Equal("--", vm.SegmentType);
            Assert.Null(vm.MapImageUri);
        }
    }
}
