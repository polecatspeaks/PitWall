using System;
using System.Collections.Generic;
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

            var match = _tracks.FirstOrDefault(track =>
                NormalizeKey(track.Name).Equals(normalizedName, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                return match;
            }

            match = _tracks.FirstOrDefault(track =>
                normalizedName.Contains(NormalizeKey(track.Name), StringComparison.OrdinalIgnoreCase));

            return match ?? GetDefault();
        }

        private static string NormalizeKey(string value)
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
                if (!AssetLoader.Exists(new Uri(TrackAssetUri)))
                {
                    return new List<TrackMetadata> { BuildDefault() };
                }

                using var stream = AssetLoader.Open(new Uri(TrackAssetUri));
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var tracks = JsonSerializer.Deserialize<List<TrackMetadata>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (tracks is { Count: > 0 })
                {
                    ApplyOutlines(tracks);
                    ApplyMapImages(tracks);
                    return tracks;
                }

                return new List<TrackMetadata> { BuildDefault() };
            }
            catch
            {
                return new List<TrackMetadata> { BuildDefault() };
            }
        }

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

        private static string ToSlug(string value)
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
    }
}
