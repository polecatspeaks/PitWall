using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Avalonia.Platform;
using PitWall.UI.Models;

namespace PitWall.UI.Services
{
    public sealed class TrackMetadataStore
    {
        private const string TrackAssetUri = "avares://PitWall.UI/Assets/Tracks/tracks.json";
        private const string OutlineAssetRoot = "avares://PitWall.UI/Assets/Tracks/outlines/";
        private const string MapAssetRoot = "avares://PitWall.UI/Assets/Tracks/maps/";
        private const string LovelyManifestUri = "avares://PitWall.UI/Assets/Tracks/lovely/lmu/manifest.json";
        private const string LovelyAssetRoot = "avares://PitWall.UI/Assets/Tracks/lovely/lmu/";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
        private readonly List<TrackMetadata> _tracks;

        public TrackMetadataStore()
        {
            _tracks = LoadTracks();
        }

        public TrackMetadata GetByName(string? trackName)
        {
            if (string.IsNullOrWhiteSpace(trackName))
            {
                return GetDefault();
            }

            var normalizedName = NormalizeKey(trackName);
            var normalizedSlug = ToSlug(trackName);

            var match = _tracks.FirstOrDefault(track =>
                !string.IsNullOrWhiteSpace(track.TrackId)
                && (NormalizeKey(track.TrackId!).Equals(normalizedName, StringComparison.OrdinalIgnoreCase)
                    || ToSlug(track.TrackId!).Equals(normalizedSlug, StringComparison.OrdinalIgnoreCase)));

            if (match != null)
            {
                return match;
            }

            match = _tracks.FirstOrDefault(track =>
                NormalizeKey(track.Name).Equals(normalizedName, StringComparison.OrdinalIgnoreCase)
                || ToSlug(track.Name).Equals(normalizedSlug, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                return match;
            }

            match = _tracks.FirstOrDefault(track =>
                normalizedName.Contains(NormalizeKey(track.Name), StringComparison.OrdinalIgnoreCase)
                || normalizedSlug.Contains(ToSlug(track.Name), StringComparison.OrdinalIgnoreCase));

            return match ?? GetDefault();
        }

        internal static string NormalizeKey(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);
            var buffer = new char[normalized.Length];
            var length = 0;

            foreach (var character in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    buffer[length++] = character;
                }
            }

            return new string(buffer, 0, length).Normalize(NormalizationForm.FormC).Trim();
        }

        private TrackMetadata GetDefault()
        {
            return _tracks.FirstOrDefault(track =>
                       track.Name.Equals("Default", StringComparison.OrdinalIgnoreCase))
                   ?? BuildDefault();
        }

        private static List<TrackMetadata> LoadTracks()
        {
            try
            {
                var baseTracks = LoadTrackList(TrackAssetUri);
                var lovelyTracks = LoadLovelyTracks();
                var tracks = MergeTracks(baseTracks, lovelyTracks);

                if (tracks.Count == 0)
                {
                    tracks.Add(BuildDefault());
                }

                ApplyOutlines(tracks);
                ApplyMapImages(tracks);
                return tracks;
            }
            catch
            {
                return new List<TrackMetadata> { BuildDefault() };
            }
        }

        [ExcludeFromCodeCoverage]
        private static List<TrackMetadata> LoadTrackList(string assetUri)
        {
            try
            {
                if (!AssetLoader.Exists(new Uri(assetUri)))
                {
                    return new List<TrackMetadata>();
                }

                using var stream = AssetLoader.Open(new Uri(assetUri));
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var tracks = JsonSerializer.Deserialize<List<TrackMetadata>>(json, JsonOptions);
                return tracks ?? new List<TrackMetadata>();
            }
            catch
            {
                return new List<TrackMetadata>();
            }
        }

        [ExcludeFromCodeCoverage]
        private static List<TrackMetadata> LoadLovelyTracks()
        {
            try
            {
                if (!AssetLoader.Exists(new Uri(LovelyManifestUri)))
                {
                    return new List<TrackMetadata>();
                }

                using var stream = AssetLoader.Open(new Uri(LovelyManifestUri));
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var manifest = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);

                if (manifest == null || manifest.Count == 0)
                {
                    return new List<TrackMetadata>();
                }

                var tracks = new List<TrackMetadata>();
                foreach (var fileName in manifest)
                {
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        continue;
                    }

                    var track = LoadLovelyTrack(fileName.Trim());
                    if (track != null)
                    {
                        tracks.Add(track);
                    }
                }

                return tracks;
            }
            catch
            {
                return new List<TrackMetadata>();
            }
        }

        [ExcludeFromCodeCoverage]
        private static TrackMetadata? LoadLovelyTrack(string fileName)
        {
            try
            {
                var assetUri = new Uri($"{LovelyAssetRoot}{fileName}");
                if (!AssetLoader.Exists(assetUri))
                {
                    return null;
                }

                using var stream = AssetLoader.Open(assetUri);
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var data = JsonSerializer.Deserialize<LovelyTrackData>(json, JsonOptions);
                if (data == null)
                {
                    return null;
                }

                var name = data.Name?.Trim();
                var trackId = data.TrackId?.Trim();

                var track = new TrackMetadata
                {
                    Name = string.IsNullOrWhiteSpace(name) ? (trackId ?? "Unknown") : name,
                    TrackId = trackId,
                    Sectors = BuildSectors(data),
                    Corners = BuildCorners(data)
                };

                if (track.Sectors.Count == 0)
                {
                    track.Sectors = BuildDefault().Sectors;
                }

                return track;
            }
            catch
            {
                return null;
            }
        }

        internal static List<TrackSector> BuildSectors(LovelyTrackData data)
        {
            if (data.Sector == null || data.Sector.Count == 0)
            {
                return new List<TrackSector>();
            }

            var ordered = data.Sector
                .Where(sector => sector.Marker.HasValue)
                .OrderBy(sector => sector.Marker!.Value)
                .ToList();

            if (ordered.Count == 0)
            {
                return new List<TrackSector>();
            }

            var sectors = new List<TrackSector>();
            var start = 0.0;
            var index = 1;

            foreach (var sector in ordered)
            {
                var end = ClampFraction(sector.Marker!.Value);
                if (end < 0.0001)
                {
                    continue;
                }

                if (end <= start)
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(sector.Name)
                    ? $"Sector {index}"
                    : sector.Name.Trim();

                sectors.Add(new TrackSector
                {
                    Name = name,
                    Start = start,
                    End = end
                });

                start = end;
                index++;
            }

            if (start < 1.0 && sectors.Count > 0)
            {
                sectors.Add(new TrackSector
                {
                    Name = $"Sector {index}",
                    Start = start,
                    End = 1.0
                });
            }

            return sectors;
        }

        internal static List<TrackCorner> BuildCorners(LovelyTrackData data)
        {
            if (data.Turn == null || data.Turn.Count == 0)
            {
                return new List<TrackCorner>();
            }

            var corners = new List<TrackCorner>();

            foreach (var turn in data.Turn.OrderBy(t => t.Number ?? int.MaxValue))
            {
                var number = turn.Number ?? (corners.Count + 1);
                var name = string.IsNullOrWhiteSpace(turn.Name)
                    ? $"Turn {number}"
                    : turn.Name.Trim();

                var start = ClampFraction(turn.Start ?? 0.0);
                var end = ClampFraction(turn.End ?? 0.0);

                corners.Add(new TrackCorner
                {
                    Number = number,
                    Name = name,
                    Direction = MapDirection(turn.Direction),
                    Severity = MapSeverity(turn.Scale),
                    Start = start,
                    End = end
                });
            }

            return corners;
        }

        internal static List<TrackMetadata> MergeTracks(List<TrackMetadata> baseTracks, List<TrackMetadata> lovelyTracks)
        {
            var merged = baseTracks.Where(track => !string.Equals(track.Name, "Default", StringComparison.OrdinalIgnoreCase)).ToList();
            var defaultTrack = baseTracks.FirstOrDefault(track =>
                string.Equals(track.Name, "Default", StringComparison.OrdinalIgnoreCase)) ?? BuildDefault();

            foreach (var lovely in lovelyTracks)
            {
                var index = merged.FindIndex(track => IsSameTrack(track, lovely));
                if (index >= 0)
                {
                    merged[index] = lovely;
                }
                else
                {
                    merged.Add(lovely);
                }
            }

            merged.Insert(0, defaultTrack);
            return merged;
        }

        internal static bool IsSameTrack(TrackMetadata left, TrackMetadata right)
        {
            var leftTrackId = NormalizeLookup(left.TrackId);
            var rightTrackId = NormalizeLookup(right.TrackId);
            var leftTrackSlug = NormalizeSlug(left.TrackId);
            var rightTrackSlug = NormalizeSlug(right.TrackId);

            if (!string.IsNullOrWhiteSpace(leftTrackId) && leftTrackId.Equals(rightTrackId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(leftTrackSlug) && leftTrackSlug.Equals(rightTrackSlug, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var leftName = NormalizeLookup(left.Name);
            var rightName = NormalizeLookup(right.Name);
            var leftNameSlug = NormalizeSlug(left.Name);
            var rightNameSlug = NormalizeSlug(right.Name);

            return !string.IsNullOrWhiteSpace(leftName)
                && (leftName.Equals(rightName, StringComparison.OrdinalIgnoreCase)
                    || leftNameSlug.Equals(rightNameSlug, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeLookup(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : NormalizeKey(value);
        }

        private static string NormalizeSlug(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : ToSlug(value);
        }

        internal static string MapDirection(int? direction)
        {
            return direction switch
            {
                0 => "Left",
                1 => "Right",
                _ => string.Empty
            };
        }

        internal static string MapSeverity(int? scale)
        {
            return scale switch
            {
                1 or 2 => "Slow",
                3 or 4 => "Medium",
                5 or 6 => "Fast",
                _ => string.Empty
            };
        }

        internal static double ClampFraction(double value)
        {
            if (value < 0)
            {
                return 0.0;
            }

            return value > 1.0 ? 1.0 : value;
        }

        [ExcludeFromCodeCoverage]
        private static void ApplyOutlines(List<TrackMetadata> tracks)
        {
            foreach (var track in tracks)
            {
                var outline = LoadOutline(track.Name);
                if (outline is { Count: > 0 })
                {
                    track.Outline = outline;
                }
            }
        }

        [ExcludeFromCodeCoverage]
        private static void ApplyMapImages(List<TrackMetadata> tracks)
        {
            foreach (var track in tracks)
            {
                var mapImageUri = ResolveMapImageUri(track.Name);
                if (!string.IsNullOrWhiteSpace(mapImageUri))
                {
                    track.MapImageUri = mapImageUri;
                }
            }
        }

        [ExcludeFromCodeCoverage]
        private static List<TrackOutlinePoint>? LoadOutline(string trackName)
        {
            try
            {
                var slug = ToSlug(trackName);
                if (string.IsNullOrWhiteSpace(slug))
                {
                    return null;
                }

                var outlineUri = new Uri($"{OutlineAssetRoot}{slug}.json");
                if (!AssetLoader.Exists(outlineUri))
                {
                    return LoadOutlineFromDisk(slug);
                }

                using var stream = AssetLoader.Open(outlineUri);
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();

                return JsonSerializer.Deserialize<List<TrackOutlinePoint>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return LoadOutlineFromDisk(ToSlug(trackName));
            }
        }

        [ExcludeFromCodeCoverage]
        private static List<TrackOutlinePoint>? LoadOutlineFromDisk(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            try
            {
                var baseDir = AppContext.BaseDirectory;
                var path = Path.Combine(baseDir, "Assets", "Tracks", "outlines", $"{slug}.json");
                if (!File.Exists(path))
                {
                    return null;
                }

                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<TrackOutlinePoint>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }

        internal static string ToSlug(string value)
        {
            var normalized = NormalizeKey(value).ToLowerInvariant();
            var builder = new StringBuilder(normalized.Length);
            var lastDash = false;

            foreach (var ch in normalized)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                    lastDash = false;
                }
                else if (!lastDash)
                {
                    builder.Append('-');
                    lastDash = true;
                }
            }

            var slug = builder.ToString().Trim('-');
            return slug;
        }

        [ExcludeFromCodeCoverage]
        private static string? ResolveMapImageUri(string trackName)
        {
            var slug = ToSlug(trackName);
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            var extensions = new[] { ".png", ".jpg", ".jpeg" };
            foreach (var ext in extensions)
            {
                var candidate = new Uri($"{MapAssetRoot}{slug}{ext}");
                if (AssetLoader.Exists(candidate))
                {
                    return candidate.ToString();
                }
            }

            return null;
        }

        private static TrackMetadata BuildDefault()
        {
            return new TrackMetadata
            {
                Name = "Default",
                Sectors = new List<TrackSector>
                {
                    new TrackSector { Name = "Sector 1", Start = 0.0, End = 0.333 },
                    new TrackSector { Name = "Sector 2", Start = 0.333, End = 0.666 },
                    new TrackSector { Name = "Sector 3", Start = 0.666, End = 1.0 }
                }
            };
        }

        internal sealed class LovelyTrackData
        {
            public string? Name { get; set; }
            public string? TrackId { get; set; }
            public List<LovelyTurn>? Turn { get; set; }
            public List<LovelySector>? Sector { get; set; }
        }

        internal sealed class LovelyTurn
        {
            public int? Number { get; set; }
            public string? Name { get; set; }
            public double? Start { get; set; }
            public double? End { get; set; }
            public int? Direction { get; set; }
            public int? Scale { get; set; }
        }

        internal sealed class LovelySector
        {
            public string? Name { get; set; }
            public double? Marker { get; set; }
        }
    }
}
