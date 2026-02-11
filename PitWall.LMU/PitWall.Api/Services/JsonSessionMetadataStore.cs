using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Api.Models;

namespace PitWall.Api.Services
{
    public class JsonSessionMetadataStore : ISessionMetadataStore
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private readonly string _filePath;
        private readonly ILogger<JsonSessionMetadataStore> _logger;
        private readonly object _lock = new();
        private Dictionary<int, SessionMetadata> _cache = new();
        private bool _loaded;

        public JsonSessionMetadataStore(string filePath, ILogger<JsonSessionMetadataStore>? logger = null)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _logger = logger ?? NullLogger<JsonSessionMetadataStore>.Instance;
        }

        public Task<IReadOnlyDictionary<int, SessionMetadata>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            EnsureLoaded();
            return Task.FromResult((IReadOnlyDictionary<int, SessionMetadata>)new Dictionary<int, SessionMetadata>(_cache));
        }

        public Task<SessionMetadata?> GetAsync(int sessionId, CancellationToken cancellationToken = default)
        {
            EnsureLoaded();
            _cache.TryGetValue(sessionId, out var metadata);
            return Task.FromResult(metadata);
        }

        public Task SetAsync(int sessionId, SessionMetadata metadata, CancellationToken cancellationToken = default)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            EnsureLoaded();

            lock (_lock)
            {
                _cache[sessionId] = metadata;
                Persist();
            }

            return Task.CompletedTask;
        }

        private void EnsureLoaded()
        {
            if (_loaded)
                return;

            lock (_lock)
            {
                if (_loaded)
                    return;

                if (!File.Exists(_filePath))
                {
                    _cache = new Dictionary<int, SessionMetadata>();
                    _loaded = true;
                    return;
                }

                try
                {
                    var json = File.ReadAllText(_filePath);
                    var data = JsonSerializer.Deserialize<Dictionary<int, SessionMetadata>>(json, Options);
                    _cache = data ?? new Dictionary<int, SessionMetadata>();
                    _loaded = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load session metadata from {FilePath}.", _filePath);
                    _cache = new Dictionary<int, SessionMetadata>();
                    _loaded = true;
                }
            }
        }

        private void Persist()
        {
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(_cache, Options);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist session metadata to {FilePath}.", _filePath);
            }
        }
    }
}
