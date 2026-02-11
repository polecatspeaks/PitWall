using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public sealed class TrackMapService
    {
        private const double MinLatLon = 0.000001;
        private const double MinPointSpacingMeters = 1.0;
        private const int MinTrackPoints = 50;
        private const int MinOutlinePoints = 12;
        private const int SegmentSmoothingWindow = 8;
        private const double OutlineCornerAngleThreshold = 0.08;
        private const int OutlineMinCornerPoints = 3;

        private readonly TelemetryBuffer _buffer;
        private readonly TrackMetadataStore _metadataStore;
        private TrackMapState? _mapState;
        private int _mapLapNumber = -1;
        private string _currentTrackName = "Default";
        private readonly List<TrackCorner> _autoCorners = new();
        private readonly List<OutlineSegment> _outlineSegments = new();
        private bool _inCorner;
        private bool _mapUsesOutline;

        public TrackMapService(TelemetryBuffer buffer, TrackMetadataStore metadataStore)
        {
            _buffer = buffer;
            _metadataStore = metadataStore;
        }

        public void Reset(string? trackName)
        {
            _currentTrackName = string.IsNullOrWhiteSpace(trackName) ? "Default" : trackName.Trim();
            _mapState = null;
            _mapLapNumber = -1;
            _autoCorners.Clear();
            _outlineSegments.Clear();
            _inCorner = false;
            _mapUsesOutline = false;
        }

        public TrackMapFrame Update(TelemetrySampleDto telemetry, string? trackName)
        {
            var metadata = _metadataStore.GetByName(trackName ?? _currentTrackName);
            _currentTrackName = metadata.Name;

            var usesCornerNameList = metadata.Corners.Count > 0
                && !metadata.Corners.Any(corner => corner.Start > 0 || corner.End > 0);
            var usesCornerRanges = metadata.Corners.Count > 0 && !usesCornerNameList;

            TryBuildMapIfNeeded(telemetry.LapNumber, metadata);

            if (_mapState == null)
            {
                return new TrackMapFrame
                {
                    TrackPoints = Array.Empty<Point>(),
                    CurrentPoint = null,
                    MapImageUri = metadata.MapImageUri,
                    SegmentStatus = new TrackSegmentStatus
                    {
                        TrackName = metadata.Name,
                        SectorName = metadata.Sectors.FirstOrDefault()?.Name ?? "--",
                        CornerLabel = "--",
                        SegmentType = "Unknown"
                    }
                };
            }

            var lapFraction = _mapUsesOutline
                ? _buffer.GetLapFraction(telemetry.LapNumber, telemetry)
                : _mapState.GetLapFraction(telemetry.Latitude, telemetry.Longitude);
            if (!lapFraction.HasValue)
            {
                return new TrackMapFrame
                {
                    TrackPoints = _mapState.NormalizedPoints,
                    CurrentPoint = null,
                    MapImageUri = metadata.MapImageUri,
                    SegmentStatus = new TrackSegmentStatus
                    {
                        TrackName = metadata.Name,
                        SectorName = metadata.Sectors.FirstOrDefault()?.Name ?? "--",
                        CornerLabel = "--",
                        SegmentType = "Unknown"
                    }
                };
            }

            var segmentType = ClassifySegment(telemetry.LapNumber, telemetry);
            TrackCorner? corner = null;

            if (_mapUsesOutline && _outlineSegments.Count > 0 && !usesCornerRanges)
            {
                var outlineSegment = FindOutlineSegment(lapFraction.Value);
                if (outlineSegment != null)
                {
                    segmentType = new SegmentClassification(outlineSegment.IsCorner, outlineSegment.Direction, outlineSegment.Severity);
                    corner = null;
                }
            }
            else
            {
                UpdateAutoCorners(lapFraction.Value, segmentType.IsCorner, segmentType.Direction, segmentType.Severity, metadata, usesCornerNameList);
                corner = usesCornerRanges
                    ? metadata.FindCorner(lapFraction.Value)
                    : FindAutoCorner(lapFraction.Value);
            }

            var sector = metadata.FindSector(lapFraction.Value);

            var segmentStatus = BuildSegmentStatus(metadata, sector, corner, segmentType, lapFraction.Value);

            var currentPoint = _mapUsesOutline
                ? _mapState.GetNormalizedPointByFraction(lapFraction.Value)
                : _mapState.GetNormalizedPoint(telemetry.Latitude, telemetry.Longitude);

            return new TrackMapFrame
            {
                TrackPoints = _mapState.NormalizedPoints,
                CurrentPoint = currentPoint,
                SegmentStatus = segmentStatus,
                MapImageUri = metadata.MapImageUri
            };
        }

        private void TryBuildMapIfNeeded(int lapNumber, TrackMetadata metadata)
        {
            if (lapNumber < 0)
            {
                return;
            }

            if (_mapState != null)
            {
                return;
            }

            if (metadata.Outline.Count < MinOutlinePoints && !string.IsNullOrWhiteSpace(metadata.MapImageUri))
            {
                // Image-only map; skip GPS-derived outline to avoid spaghetti paths.
                return;
            }

            if (metadata.Outline.Count >= MinOutlinePoints)
            {
                var outline = BuildMapFromOutline(metadata);
                if (outline != null)
                {
                    _mapState = outline;
                    _mapLapNumber = lapNumber;
                    _mapUsesOutline = true;
                    BuildOutlineSegments(metadata);
                    _autoCorners.Clear();
                    _inCorner = false;
                    return;
                }
            }

            var lapToBuild = lapNumber > 1 ? lapNumber - 1 : lapNumber;
            var map = BuildMapFromLap(lapToBuild) ?? (lapNumber > 0 ? BuildMapFromLap(lapNumber) : null);
            if (map != null)
            {
                _mapState = map;
                _mapLapNumber = lapNumber;
                _mapUsesOutline = false;
                _autoCorners.Clear();
                _inCorner = false;
            }
        }

        private static TrackMapState? BuildMapFromOutline(TrackMetadata metadata)
        {
            if (metadata.Outline.Count < MinOutlinePoints)
            {
                return null;
            }

            var points = metadata.Outline
                .Select(point => new Point(point.X, point.Y))
                .ToList();

            return TrackMapState.BuildFromNormalized(points);
        }

        private TrackMapState? BuildMapFromLap(int lapNumber)
        {
            var samples = _buffer.GetLapData(lapNumber);
            if (samples.Length == 0)
            {
                return null;
            }

            var valid = samples
                .Where(sample => Math.Abs(sample.Latitude) > MinLatLon && Math.Abs(sample.Longitude) > MinLatLon)
                .ToList();

            if (valid.Count < MinTrackPoints)
            {
                return null;
            }

            return TrackMapState.Build(valid);
        }

        private SegmentClassification ClassifySegment(int lapNumber, TelemetrySampleDto telemetry)
        {
            var (steeringAngle, lateralG) = GetSmoothedInputs(lapNumber, telemetry);
            return ClassifySegment(steeringAngle, lateralG);
        }

        private static SegmentClassification ClassifySegment(double steeringAngle, double lateralG)
        {
            var absSteer = Math.Abs(steeringAngle);
            var absLatG = Math.Abs(lateralG);

            var isCorner = absSteer > 0.08 || absLatG > 0.35;
            var direction = steeringAngle > 0.01
                ? "Right"
                : steeringAngle < -0.01
                    ? "Left"
                    : lateralG > 0.01
                        ? "Left"
                        : lateralG < -0.01
                            ? "Right"
                            : string.Empty;

            var severity = absLatG switch
            {
                > 1.2 => "Fast",
                > 0.8 => "Medium",
                > 0.35 => "Slow",
                _ => string.Empty
            };

            return new SegmentClassification(isCorner, direction, severity);
        }

        private (double SteeringAngle, double LateralG) GetSmoothedInputs(int lapNumber, TelemetrySampleDto telemetry)
        {
            var samples = _buffer.GetAll();
            if (samples.Length == 0)
            {
                return (telemetry.SteeringAngle, telemetry.LateralG);
            }

            var sampleIndex = Array.FindIndex(samples, s => ReferenceEquals(s, telemetry));
            if (sampleIndex < 0 && telemetry.Timestamp.HasValue)
            {
                var timestamp = telemetry.Timestamp.Value;
                sampleIndex = Array.FindIndex(
                    samples,
                    s => s.LapNumber == lapNumber && s.Timestamp.HasValue && s.Timestamp.Value == timestamp);
            }

            if (sampleIndex < 0)
            {
                sampleIndex = samples.Length - 1;
            }

            var sumSteer = 0.0;
            var sumLatG = 0.0;
            var count = 0;

            for (var i = sampleIndex; i >= 0 && count < SegmentSmoothingWindow; i--)
            {
                if (samples[i].LapNumber != lapNumber)
                {
                    continue;
                }

                sumSteer += samples[i].SteeringAngle;
                sumLatG += samples[i].LateralG;
                count++;
            }

            if (count == 0)
            {
                return (telemetry.SteeringAngle, telemetry.LateralG);
            }

            return (sumSteer / count, sumLatG / count);
        }

        private void UpdateAutoCorners(double lapFraction, bool isCorner, string direction, string severity, TrackMetadata metadata, bool usesCornerNameList)
        {
            if (_mapUsesOutline && _outlineSegments.Count > 0)
            {
                return;
            }

            if (metadata.Corners.Count > 0 && !usesCornerNameList)
            {
                return;
            }

            if (isCorner && !_inCorner)
            {
                _inCorner = true;
                var nextIndex = _autoCorners.Count;
                var template = usesCornerNameList && nextIndex < metadata.Corners.Count
                    ? metadata.Corners[nextIndex]
                    : null;
                var nextNumber = template?.Number > 0 ? template.Number : nextIndex + 1;
                var nextName = string.IsNullOrWhiteSpace(template?.Name)
                    ? $"Turn {nextNumber}"
                    : template!.Name;
                var cornerDirection = !string.IsNullOrWhiteSpace(template?.Direction)
                    ? template!.Direction
                    : direction;
                var cornerSeverity = !string.IsNullOrWhiteSpace(template?.Severity)
                    ? template!.Severity
                    : severity;
                _autoCorners.Add(new TrackCorner
                {
                    Number = nextNumber,
                    Name = nextName,
                    Direction = cornerDirection,
                    Severity = cornerSeverity,
                    Start = lapFraction,
                    End = lapFraction
                });
                return;
            }

            if (!isCorner && _inCorner)
            {
                _inCorner = false;
                if (_autoCorners.Count > 0)
                {
                    _autoCorners[^1].End = lapFraction;
                }
                return;
            }

            if (isCorner && _inCorner && _autoCorners.Count > 0)
            {
                _autoCorners[^1].End = lapFraction;
                if (!string.IsNullOrWhiteSpace(direction))
                {
                    _autoCorners[^1].Direction = direction;
                }

                if (!string.IsNullOrWhiteSpace(severity))
                {
                    _autoCorners[^1].Severity = severity;
                }
            }
        }

        private TrackCorner? FindAutoCorner(double lapFraction)
        {
            return _autoCorners.FirstOrDefault(corner => corner.Contains(lapFraction));
        }

        private OutlineSegment? FindOutlineSegment(double lapFraction)
        {
            return _outlineSegments.FirstOrDefault(segment => segment.Contains(lapFraction));
        }

        private static TrackSegmentStatus BuildSegmentStatus(
            TrackMetadata metadata,
            TrackSector? sector,
            TrackCorner? corner,
            SegmentClassification segmentType,
            double lapFraction)
        {
            var sectorName = sector?.Name ?? "Sector --";
            var cornerLabel = corner == null
                ? segmentType.IsCorner
                    ? "Corner"
                    : "Straight"
                : string.IsNullOrWhiteSpace(corner.Name)
                    ? $"T{corner.Number}"
                    : $"T{corner.Number} {corner.Name}";

            var segmentLabel = segmentType.IsCorner
                ? cornerLabel
                : "Straight";

            return new TrackSegmentStatus
            {
                TrackName = metadata.Name,
                SectorName = sectorName,
                CornerLabel = segmentLabel,
                SegmentType = segmentType.IsCorner ? "Corner" : "Straight",
                Direction = segmentType.Direction,
                Severity = segmentType.Severity,
                LapFraction = lapFraction
            };
        }

        private void BuildOutlineSegments(TrackMetadata metadata)
        {
            _outlineSegments.Clear();

            if (_mapState == null || _mapState.PointCount < MinOutlinePoints)
            {
                return;
            }

            var cornerIndex = 0;
            var segmentStart = -1;
            var segmentDirection = string.Empty;
            var segmentSeverity = string.Empty;

            for (var i = 1; i < _mapState.PointCount - 1; i++)
            {
                var prev = _mapState.GetPointAt(i - 1);
                var current = _mapState.GetPointAt(i);
                var next = _mapState.GetPointAt(i + 1);

                var v1 = new Point(current.X - prev.X, current.Y - prev.Y);
                var v2 = new Point(next.X - current.X, next.Y - current.Y);

                var mag1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
                var mag2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);
                if (mag1 < 0.0001 || mag2 < 0.0001)
                {
                    continue;
                }

                var dot = (v1.X * v2.X + v1.Y * v2.Y) / (mag1 * mag2);
                dot = Math.Clamp(dot, -1.0, 1.0);
                var angle = Math.Acos(dot);
                var cross = v1.X * v2.Y - v1.Y * v2.X;

                var isCorner = angle >= OutlineCornerAngleThreshold;
                if (isCorner)
                {
                    if (segmentStart < 0)
                    {
                        segmentStart = i;
                        segmentDirection = cross >= 0 ? "Left" : "Right";
                        segmentSeverity = GetSeverityFromAngle(angle);
                    }
                    else
                    {
                        segmentDirection = cross >= 0 ? "Left" : "Right";
                        segmentSeverity = GetSeverityFromAngle(Math.Max(angle, GetSeverityAngle(segmentSeverity)));
                    }
                }
                else if (segmentStart >= 0)
                {
                    AddOutlineSegment(metadata, segmentStart, i, cornerIndex, segmentDirection, segmentSeverity);
                    cornerIndex++;
                    segmentStart = -1;
                    segmentDirection = string.Empty;
                    segmentSeverity = string.Empty;
                }
            }

            if (segmentStart >= 0)
            {
                AddOutlineSegment(metadata, segmentStart, _mapState.PointCount - 1, cornerIndex, segmentDirection, segmentSeverity);
            }
        }

        private void AddOutlineSegment(TrackMetadata metadata, int startIndex, int endIndex, int cornerIndex, string direction, string severity)
        {
            if (_mapState == null)
            {
                return;
            }

            if (endIndex - startIndex < OutlineMinCornerPoints)
            {
                return;
            }

            var startFraction = _mapState.GetFractionForIndex(startIndex);
            var endFraction = _mapState.GetFractionForIndex(endIndex);

            var number = cornerIndex + 1;
            var name = metadata.Corners.Count > cornerIndex
                ? metadata.Corners[cornerIndex].Name
                : string.Empty;

            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Turn {number}";
            }

            _outlineSegments.Add(new OutlineSegment
            {
                Number = number,
                Name = name,
                Direction = direction,
                Severity = severity,
                Start = startFraction,
                End = endFraction,
                IsCorner = true
            });
        }

        private static string GetSeverityFromAngle(double angle)
        {
            return angle switch
            {
                > 0.55 => "Slow",
                > 0.3 => "Medium",
                > 0.12 => "Fast",
                _ => string.Empty
            };
        }

        private static double GetSeverityAngle(string severity)
        {
            return severity switch
            {
                "Slow" => 0.6,
                "Medium" => 0.35,
                "Fast" => 0.15,
                _ => 0.0
            };
        }

        private sealed record SegmentClassification(bool IsCorner, string Direction, string Severity);

        private sealed class OutlineSegment
        {
            public int Number { get; init; }
            public string Name { get; init; } = string.Empty;
            public string Direction { get; init; } = string.Empty;
            public string Severity { get; init; } = string.Empty;
            public double Start { get; init; }
            public double End { get; init; }
            public bool IsCorner { get; init; }

            public bool Contains(double lapFraction)
            {
                return TrackRange.ContainsFraction(Start, End, lapFraction);
            }
        }

        private sealed class TrackMapState
        {
            public IReadOnlyList<Point> NormalizedPoints { get; init; } = Array.Empty<Point>();
            private IReadOnlyList<Point> RawPoints { get; init; } = Array.Empty<Point>();
            private IReadOnlyList<double> CumulativeDistances { get; init; } = Array.Empty<double>();
            private double TotalDistance { get; init; }
            private double OriginLat { get; init; }
            private double OriginLon { get; init; }
            private double LatScale { get; init; }
            private double LonScale { get; init; }
            private bool IsOutline { get; init; }

            public static TrackMapState? Build(IReadOnlyList<TelemetrySampleDto> samples)
            {
                var avgLat = samples.Average(sample => sample.Latitude);
                var avgLon = samples.Average(sample => sample.Longitude);
                var latScale = 110540.0;
                var lonScale = 111320.0 * Math.Cos(avgLat * Math.PI / 180.0);

                var rawPoints = new List<Point>();
                Point? lastPoint = null;

                foreach (var sample in samples)
                {
                    var point = ToMeters(sample.Latitude, sample.Longitude, avgLat, avgLon, latScale, lonScale);
                    if (lastPoint.HasValue)
                    {
                        var dx = point.X - lastPoint.Value.X;
                        var dy = point.Y - lastPoint.Value.Y;
                        if (Math.Sqrt(dx * dx + dy * dy) < MinPointSpacingMeters)
                        {
                            continue;
                        }
                    }

                    rawPoints.Add(point);
                    lastPoint = point;
                }

                if (rawPoints.Count < MinTrackPoints)
                {
                    return null;
                }

                var cumulative = new List<double>(rawPoints.Count);
                var total = 0.0;
                cumulative.Add(0.0);
                for (var i = 1; i < rawPoints.Count; i++)
                {
                    var dx = rawPoints[i].X - rawPoints[i - 1].X;
                    var dy = rawPoints[i].Y - rawPoints[i - 1].Y;
                    total += Math.Sqrt(dx * dx + dy * dy);
                    cumulative.Add(total);
                }

                var bounds = GetBounds(rawPoints);
                var width = Math.Max(1.0, bounds.maxX - bounds.minX);
                var height = Math.Max(1.0, bounds.maxY - bounds.minY);

                var normalized = rawPoints
                    .Select(point => new Point(
                        (point.X - bounds.minX) / width,
                        (point.Y - bounds.minY) / height))
                    .ToList();

                return new TrackMapState
                {
                    RawPoints = rawPoints,
                    NormalizedPoints = normalized,
                    CumulativeDistances = cumulative,
                    TotalDistance = total,
                    OriginLat = avgLat,
                    OriginLon = avgLon,
                    LatScale = latScale,
                    LonScale = lonScale,
                    IsOutline = false
                };
            }

            public static TrackMapState? BuildFromNormalized(IReadOnlyList<Point> normalizedPoints)
            {
                if (normalizedPoints.Count < MinOutlinePoints)
                {
                    return null;
                }

                var normalized = NormalizePoints(normalizedPoints);
                var cumulative = new List<double>(normalized.Count);
                var total = 0.0;
                cumulative.Add(0.0);

                for (var i = 1; i < normalized.Count; i++)
                {
                    var dx = normalized[i].X - normalized[i - 1].X;
                    var dy = normalized[i].Y - normalized[i - 1].Y;
                    total += Math.Sqrt(dx * dx + dy * dy);
                    cumulative.Add(total);
                }

                return new TrackMapState
                {
                    RawPoints = normalized,
                    NormalizedPoints = normalized,
                    CumulativeDistances = cumulative,
                    TotalDistance = total,
                    OriginLat = 0,
                    OriginLon = 0,
                    LatScale = 1,
                    LonScale = 1,
                    IsOutline = true
                };
            }

            public int PointCount => NormalizedPoints.Count;

            public Point GetPointAt(int index)
            {
                return NormalizedPoints[index];
            }

            public double GetFractionForIndex(int index)
            {
                if (TotalDistance <= 0 || index < 0 || index >= CumulativeDistances.Count)
                {
                    return 0.0;
                }

                return CumulativeDistances[index] / TotalDistance;
            }

            public double? GetLapFraction(double latitude, double longitude)
            {
                if (Math.Abs(latitude) < MinLatLon || Math.Abs(longitude) < MinLatLon)
                {
                    return null;
                }

                if (RawPoints.Count == 0 || TotalDistance <= 0)
                {
                    return null;
                }

                var meters = ToMeters(latitude, longitude, OriginLat, OriginLon, LatScale, LonScale);
                var nearestIndex = FindNearestIndex(meters);
                if (nearestIndex < 0)
                {
                    return null;
                }

                return CumulativeDistances[nearestIndex] / TotalDistance;
            }

            public Point? GetNormalizedPoint(double latitude, double longitude)
            {
                if (IsOutline)
                {
                    return null;
                }

                if (Math.Abs(latitude) < MinLatLon || Math.Abs(longitude) < MinLatLon)
                {
                    return null;
                }

                var meters = ToMeters(latitude, longitude, OriginLat, OriginLon, LatScale, LonScale);
                var nearestIndex = FindNearestIndex(meters);
                if (nearestIndex < 0)
                {
                    return null;
                }

                return NormalizedPoints[nearestIndex];
            }

            public Point? GetNormalizedPointByFraction(double lapFraction)
            {
                if (NormalizedPoints.Count == 0 || TotalDistance <= 0)
                {
                    return null;
                }

                var targetDistance = Math.Clamp(lapFraction, 0.0, 1.0) * TotalDistance;
                var index = 0;
                while (index < CumulativeDistances.Count && CumulativeDistances[index] < targetDistance)
                {
                    index++;
                }

                if (index <= 0)
                {
                    return NormalizedPoints[0];
                }

                if (index >= NormalizedPoints.Count)
                {
                    return NormalizedPoints[^1];
                }

                var prevDistance = CumulativeDistances[index - 1];
                var nextDistance = CumulativeDistances[index];
                var span = Math.Max(0.0001, nextDistance - prevDistance);
                var t = (targetDistance - prevDistance) / span;

                var prev = NormalizedPoints[index - 1];
                var next = NormalizedPoints[index];

                return new Point(
                    prev.X + (next.X - prev.X) * t,
                    prev.Y + (next.Y - prev.Y) * t);
            }

            private int FindNearestIndex(Point point)
            {
                var bestIndex = -1;
                var bestDistance = double.MaxValue;

                for (var i = 0; i < RawPoints.Count; i++)
                {
                    var dx = point.X - RawPoints[i].X;
                    var dy = point.Y - RawPoints[i].Y;
                    var dist = dx * dx + dy * dy;
                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        bestIndex = i;
                    }
                }

                return bestIndex;
            }

            private static (double minX, double minY, double maxX, double maxY) GetBounds(IReadOnlyList<Point> points)
            {
                var minX = double.MaxValue;
                var minY = double.MaxValue;
                var maxX = double.MinValue;
                var maxY = double.MinValue;

                foreach (var point in points)
                {
                    minX = Math.Min(minX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxX = Math.Max(maxX, point.X);
                    maxY = Math.Max(maxY, point.Y);
                }

                return (minX, minY, maxX, maxY);
            }

            private static List<Point> NormalizePoints(IReadOnlyList<Point> points)
            {
                var bounds = GetBounds(points);
                var width = Math.Max(1.0, bounds.maxX - bounds.minX);
                var height = Math.Max(1.0, bounds.maxY - bounds.minY);

                return points
                    .Select(point => new Point(
                        (point.X - bounds.minX) / width,
                        (point.Y - bounds.minY) / height))
                    .ToList();
            }

            private static Point ToMeters(double latitude, double longitude, double originLat, double originLon, double latScale, double lonScale)
            {
                var x = (longitude - originLon) * lonScale;
                var y = (latitude - originLat) * latScale;
                return new Point(x, y);
            }
        }
    }
}
