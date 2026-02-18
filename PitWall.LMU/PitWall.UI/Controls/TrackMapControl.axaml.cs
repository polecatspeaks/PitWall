using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PitWall.UI.Models;

using System.Diagnostics.CodeAnalysis;

namespace PitWall.UI.Controls
{
    [ExcludeFromCodeCoverage]
    public partial class TrackMapControl : UserControl
    {
        public static readonly StyledProperty<IReadOnlyList<Point>> TrackPointsProperty =
            AvaloniaProperty.Register<TrackMapControl, IReadOnlyList<Point>>(nameof(TrackPoints), Array.Empty<Point>());

        public static readonly StyledProperty<Point?> CurrentPointProperty =
            AvaloniaProperty.Register<TrackMapControl, Point?>(nameof(CurrentPoint));

        public static new readonly StyledProperty<double> PaddingProperty =
            AvaloniaProperty.Register<TrackMapControl, double>(nameof(Padding), 8.0);

        public static readonly StyledProperty<string?> MapImageUriProperty =
            AvaloniaProperty.Register<TrackMapControl, string?>(nameof(MapImageUri));

        public static readonly StyledProperty<string?> CornerLabelProperty =
            AvaloniaProperty.Register<TrackMapControl, string?>(nameof(CornerLabel));

        public static readonly StyledProperty<IReadOnlyList<CarMapMarker>> VehicleMarkersProperty =
            AvaloniaProperty.Register<TrackMapControl, IReadOnlyList<CarMapMarker>>(nameof(VehicleMarkers), Array.Empty<CarMapMarker>());

        private readonly List<Ellipse> _vehicleEllipses = new();

        public TrackMapControl()
        {
            InitializeComponent();
            MapCanvas.SizeChanged += (_, _) => UpdateVisuals();
            PropertyChanged += (_, args) =>
            {
                if (args.Property == TrackPointsProperty || args.Property == CurrentPointProperty)
                {
                    UpdateVisuals();
                }

                if (args.Property == VehicleMarkersProperty)
                {
                    UpdateVehicleMarkers();
                }

                if (args.Property == MapImageUriProperty)
                {
                    UpdateMapImage();
                }

                if (args.Property == CornerLabelProperty)
                {
                    UpdateSegmentLabel();
                }
            };
        }

        public IReadOnlyList<Point> TrackPoints
        {
            get => GetValue(TrackPointsProperty);
            set => SetValue(TrackPointsProperty, value);
        }

        public Point? CurrentPoint
        {
            get => GetValue(CurrentPointProperty);
            set => SetValue(CurrentPointProperty, value);
        }

        public new double Padding
        {
            get => GetValue(PaddingProperty);
            set => SetValue(PaddingProperty, value);
        }

        public string? MapImageUri
        {
            get => GetValue(MapImageUriProperty);
            set => SetValue(MapImageUriProperty, value);
        }

        public string? CornerLabel
        {
            get => GetValue(CornerLabelProperty);
            set => SetValue(CornerLabelProperty, value);
        }

        public IReadOnlyList<CarMapMarker> VehicleMarkers
        {
            get => GetValue(VehicleMarkersProperty);
            set => SetValue(VehicleMarkersProperty, value);
        }

        private void UpdateVisuals()
        {
            UpdateTrackPath();
            UpdateCarMarker();
            UpdateSegmentLabel();
        }

        private void UpdateMapImage()
        {
            MapImage.Source = null;
            if (string.IsNullOrWhiteSpace(MapImageUri))
            {
                return;
            }

            try
            {
                if (MapImageUri.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(MapImageUri);
                    using var stream = AssetLoader.Open(uri);
                    MapImage.Source = new Bitmap(stream);
                    return;
                }

                if (File.Exists(MapImageUri))
                {
                    MapImage.Source = new Bitmap(MapImageUri);
                }
            }
            catch
            {
                MapImage.Source = null;
            }

            UpdateVisuals();
        }

        private void UpdateTrackPath()
        {
            if (TrackPoints == null || TrackPoints.Count < 2)
            {
                TrackPath.Data = null;
                return;
            }

            var bounds = MapCanvas.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                var first = ScalePoint(TrackPoints[0]);
                context.BeginFigure(first, false);
                for (var i = 1; i < TrackPoints.Count; i++)
                {
                    context.LineTo(ScalePoint(TrackPoints[i]));
                }
            }

            TrackPath.Data = geometry;
        }

        private void UpdateCarMarker()
        {
            if (CurrentPoint is null)
            {
                CarMarker.IsVisible = false;
                return;
            }

            var point = ScalePoint(CurrentPoint.Value);
            Canvas.SetLeft(CarMarker, point.X - CarMarker.Width / 2);
            Canvas.SetTop(CarMarker, point.Y - CarMarker.Height / 2);
            CarMarker.IsVisible = true;
        }

        private void UpdateSegmentLabel()
        {
            if (CurrentPoint is null || string.IsNullOrWhiteSpace(CornerLabel))
            {
                SegmentLabel.IsVisible = false;
                return;
            }

            SegmentLabelText.Text = CornerLabel;
            SegmentLabel.IsVisible = true;

            // Measure the label so we can position it precisely.
            SegmentLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var labelWidth = SegmentLabel.DesiredSize.Width;
            var labelHeight = SegmentLabel.DesiredSize.Height;

            var point = ScalePoint(CurrentPoint.Value);
            var bounds = MapCanvas.Bounds;

            // If canvas is too small for the label, hide it to avoid positioning issues
            if (bounds.Width < labelWidth + 8 || bounds.Height < labelHeight + 8)
            {
                SegmentLabel.IsVisible = false;
                return;
            }

            // Place the label above the car marker with a small offset.
            var x = point.X - labelWidth / 2;
            var y = point.Y - CarMarker.Height / 2 - labelHeight - 4;

            // Clamp to canvas bounds with 4px margins
            x = Math.Clamp(x, 4, bounds.Width - labelWidth - 4);
            y = Math.Clamp(y, 4, bounds.Height - labelHeight - 4);

            Canvas.SetLeft(SegmentLabel, x);
            Canvas.SetTop(SegmentLabel, y);
        }

        private void UpdateVehicleMarkers()
        {
            // Remove old vehicle markers from canvas
            foreach (var ellipse in _vehicleEllipses)
            {
                MapCanvas.Children.Remove(ellipse);
            }
            _vehicleEllipses.Clear();

            var markers = VehicleMarkers;
            if (markers == null || markers.Count == 0)
            {
                return;
            }

            foreach (var marker in markers)
            {
                var point = ScalePoint(marker.Position);
                var size = marker.IsPlayer ? 8.0 : 6.0;
                var fill = marker.IsPlayer
                    ? Brushes.Gold
                    : GetClassBrush(marker.VehicleClass);

                var ellipse = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = fill,
                    Opacity = marker.IsPlayer ? 1.0 : 0.8
                };

                Canvas.SetLeft(ellipse, point.X - size / 2);
                Canvas.SetTop(ellipse, point.Y - size / 2);

                MapCanvas.Children.Add(ellipse);
                _vehicleEllipses.Add(ellipse);
            }
        }

        private static IBrush GetClassBrush(string vehicleClass)
        {
            // Color code by vehicle class — common sim racing convention
            return vehicleClass?.ToUpperInvariant() switch
            {
                "LMP1" or "HYPERCAR" or "GTP" => new SolidColorBrush(Color.Parse("#FF4444")),
                "LMP2" or "LMP3" => new SolidColorBrush(Color.Parse("#4488FF")),
                "GTE" or "LMGTE" or "GT3" => new SolidColorBrush(Color.Parse("#44FF44")),
                "GT4" => new SolidColorBrush(Color.Parse("#FF8844")),
                _ => new SolidColorBrush(Color.Parse("#AAAAAA"))
            };
        }

        private Point ScalePoint(Point point)
        {
            var bounds = MapCanvas.Bounds;
            var availableWidth = Math.Max(0, bounds.Width - (Padding * 2));
            var availableHeight = Math.Max(0, bounds.Height - (Padding * 2));

            var offsetX = Padding;
            var offsetY = Padding;
            double width, height;

            if (MapImage.Source is Bitmap bitmap && bitmap.PixelSize.Width > 0 && bitmap.PixelSize.Height > 0)
            {
                var scale = Math.Min(availableWidth / bitmap.PixelSize.Width, availableHeight / bitmap.PixelSize.Height);
                width = bitmap.PixelSize.Width * scale;
                height = bitmap.PixelSize.Height * scale;
                offsetX = Padding + (availableWidth - width) / 2;
                offsetY = Padding + (availableHeight - height) / 2;
            }
            else
            {
                // Use uniform scaling to preserve track aspect ratio.
                // Normalized points are already centered in 0-1 space.
                var uniformSize = Math.Min(availableWidth, availableHeight);
                width = uniformSize;
                height = uniformSize;
                offsetX = Padding + (availableWidth - uniformSize) / 2;
                offsetY = Padding + (availableHeight - uniformSize) / 2;
            }

            var x = offsetX + (point.X * width);
            var y = offsetY + ((1 - point.Y) * height);
            return new Point(x, y);
        }
    }
}
