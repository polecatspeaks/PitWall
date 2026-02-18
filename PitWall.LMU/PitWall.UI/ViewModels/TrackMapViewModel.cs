using System.Collections.Generic;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using PitWall.UI.Models;

namespace PitWall.UI.ViewModels
{
    public partial class TrackMapViewModel : ViewModelBase
    {
        [ObservableProperty]
        private IReadOnlyList<Point> trackPoints = System.Array.Empty<Point>();

        [ObservableProperty]
        private Point? currentPoint;

        [ObservableProperty]
        private IReadOnlyList<CarMapMarker> vehicleMarkers = System.Array.Empty<CarMapMarker>();

        [ObservableProperty]
        private string trackName = "TRACK";

        [ObservableProperty]
        private string sectorLabel = "--";

        [ObservableProperty]
        private string cornerLabel = "--";

        [ObservableProperty]
        private string segmentType = "--";

        [ObservableProperty]
        private string? mapImageUri;

        public void UpdateFrame(TrackMapFrame frame)
        {
            TrackPoints = frame.TrackPoints;
            CurrentPoint = frame.CurrentPoint;
            VehicleMarkers = frame.VehicleMarkers;
            MapImageUri = frame.MapImageUri;

            if (frame.SegmentStatus != null)
            {
                TrackName = frame.SegmentStatus.TrackName;
                SectorLabel = frame.SegmentStatus.SectorName;
                CornerLabel = frame.SegmentStatus.CornerLabel;
                SegmentType = frame.SegmentStatus.SegmentType;
            }
        }
    }
}
