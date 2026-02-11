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
    public sealed class CarSpecStore
    {
        private const string CarManifestUri = "avares://PitWall.UI/Assets/Cars/lmu/manifest.json";
        private const string CarAliasUri = "avares://PitWall.UI/Assets/Cars/lmu/aliases.json";
        private const string CarAssetRoot = "avares://PitWall.UI/Assets/Cars/lmu/";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly List<CarSpec> _cars;
        private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<CarSpec>> _carsByManufacturer = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _manufacturerKeys = new();

        public CarSpecStore()
        {
            _cars = LoadCars();
            LoadAliases(_aliases);
            BuildManufacturerIndex();
        }

        public CarSpec? GetByName(string? carName)
        {
            if (string.IsNullOrWhiteSpace(carName))
            {
                return null;
            }

            var candidates = BuildCandidates(carName);
            foreach (var candidate in candidates)
            {
                var normalized = NormalizeKey(candidate);
                var slug = ToSlug(candidate);

                var match = _cars.FirstOrDefault(car =>
                    NormalizeKey(car.Name).Equals(normalized, StringComparison.OrdinalIgnoreCase)
                    || ToSlug(car.Name).Equals(slug, StringComparison.OrdinalIgnoreCase)
                    || car.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    return match;
                }

                match = _cars.FirstOrDefault(car =>
                    normalized.Contains(NormalizeKey(car.Name), StringComparison.OrdinalIgnoreCase)
                    || slug.Contains(ToSlug(car.Name), StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    return match;
                }
            }

            return TryGetByManufacturer(carName);
        }

        private static List<CarSpec> LoadCars()
        {
            try
            {
                if (!AssetLoader.Exists(new Uri(CarManifestUri)))
                {
                    return new List<CarSpec>();
                }

                using var stream = AssetLoader.Open(new Uri(CarManifestUri));
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var manifest = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);

                if (manifest == null || manifest.Count == 0)
                {
                    return new List<CarSpec>();
                }

                var cars = new List<CarSpec>();
                foreach (var fileName in manifest)
                {
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        continue;
                    }

                    var car = LoadCar(fileName.Trim());
                    if (car != null)
                    {
                        cars.Add(car);
                    }
                }

                return cars;
            }
            catch
            {
                return new List<CarSpec>();
            }
        }

        private static CarSpec? LoadCar(string fileName)
        {
            try
            {
                var assetUri = new Uri($"{CarAssetRoot}{fileName}");
                if (!AssetLoader.Exists(assetUri))
                {
                    return null;
                }

                using var stream = AssetLoader.Open(assetUri);
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                return JsonSerializer.Deserialize<CarSpec>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private static void LoadAliases(Dictionary<string, string> target)
        {
            try
            {
                if (!AssetLoader.Exists(new Uri(CarAliasUri)))
                {
                    return;
                }

                using var stream = AssetLoader.Open(new Uri(CarAliasUri));
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
                if (data == null || data.Count == 0)
                {
                    return;
                }

                foreach (var (key, value) in data)
                {
                    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    var normalizedKey = NormalizeKey(key);
                    if (!target.ContainsKey(normalizedKey))
                    {
                        target[normalizedKey] = value.Trim();
                    }

                    var slugKey = ToSlug(key);
                    if (!string.IsNullOrWhiteSpace(slugKey) && !target.ContainsKey(slugKey))
                    {
                        target[slugKey] = value.Trim();
                    }
                }
            }
            catch
            {
                return;
            }
        }

        private void BuildManufacturerIndex()
        {
            _carsByManufacturer.Clear();
            _manufacturerKeys.Clear();

            foreach (var car in _cars)
            {
                var key = GetManufacturerKey(car.Name);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var normalizedKey = NormalizeKey(key);
                if (!_carsByManufacturer.TryGetValue(normalizedKey, out var list))
                {
                    list = new List<CarSpec>();
                    _carsByManufacturer[normalizedKey] = list;
                }

                list.Add(car);
            }

            _manufacturerKeys.AddRange(_carsByManufacturer.Keys.OrderByDescending(key => key.Length));
        }

        private CarSpec? TryGetByManufacturer(string carName)
        {
            if (_manufacturerKeys.Count == 0)
            {
                return null;
            }

            var normalized = NormalizeKey(carName);
            foreach (var key in _manufacturerKeys)
            {
                if (!normalized.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (_carsByManufacturer.TryGetValue(key, out var matches) && matches.Count == 1)
                {
                    return matches[0];
                }
            }

            return null;
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

        private static string ToSlug(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (var character in NormalizeKey(value).ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
                else if (builder.Length == 0 || builder[^1] == '-')
                {
                    continue;
                }
                else
                {
                    builder.Append('-');
                }
            }

            return builder.ToString().Trim('-');
        }

        private List<string> BuildCandidates(string name)
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddCandidate(string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    candidates.Add(value.Trim());
                }
            }

            AddCandidate(name);
            AddCandidate(StripTeamSuffix(name));

            foreach (var candidate in candidates.ToArray())
            {
                if (TryGetAlias(candidate, out var aliasValue))
                {
                    AddCandidate(aliasValue);
                }
            }

            return candidates.ToList();
        }

        private bool TryGetAlias(string value, out string alias)
        {
            alias = string.Empty;
            if (_aliases.Count == 0)
            {
                return false;
            }

            var normalized = NormalizeKey(value);
            if (_aliases.TryGetValue(normalized, out var directMatch))
            {
                alias = directMatch;
                return true;
            }

            var slug = ToSlug(value);
            if (!string.IsNullOrWhiteSpace(slug) && _aliases.TryGetValue(slug, out var slugMatch))
            {
                alias = slugMatch;
                return true;
            }

            return false;
        }

        private static string StripTeamSuffix(string name)
        {
            var trimmed = name.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            var working = trimmed;
            var hashIndex = working.IndexOf(" #", StringComparison.Ordinal);
            if (hashIndex > 0)
            {
                working = working[..hashIndex].Trim();
            }

            var parenIndex = working.IndexOf("(", StringComparison.Ordinal);
            if (parenIndex > 0)
            {
                working = working[..parenIndex].Trim();
            }

            var dashIndex = working.IndexOf(" - ", StringComparison.Ordinal);
            if (dashIndex > 0)
            {
                working = working[..dashIndex].Trim();
            }

            var pipeIndex = working.IndexOf(" | ", StringComparison.Ordinal);
            if (pipeIndex > 0)
            {
                working = working[..pipeIndex].Trim();
            }

            var teamIndex = working.IndexOf(" Team", StringComparison.OrdinalIgnoreCase);
            if (teamIndex > 0)
            {
                working = working[..teamIndex].Trim();
            }

            return working;
        }

        private static string GetManufacturerKey(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var trimmed = name.Trim();
            if (trimmed.StartsWith("Aston Martin ", StringComparison.OrdinalIgnoreCase))
            {
                return "Aston Martin";
            }

            if (trimmed.StartsWith("Isotta Fraschini ", StringComparison.OrdinalIgnoreCase))
            {
                return "Isotta Fraschini";
            }

            if (trimmed.StartsWith("Mercedes-AMG ", StringComparison.OrdinalIgnoreCase))
            {
                return "Mercedes-AMG";
            }

            var spaceIndex = trimmed.IndexOf(' ');
            return spaceIndex > 0 ? trimmed[..spaceIndex] : trimmed;
        }
    }
}
