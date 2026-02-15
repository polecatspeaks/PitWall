using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LMUMemoryReader;

public sealed class TelemetryFileWriter : IAsyncDisposable
{
    private readonly FileStream _stream;
    private readonly Utf8JsonWriter _writer;
    private readonly JsonSerializerOptions _options;
    private bool _isClosed;

    public TelemetryFileWriter(string path, SessionMetadata metadata, JsonSerializerOptions options)
    {
        _options = options;
        _stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
        _writer = new Utf8JsonWriter(_stream, new JsonWriterOptions { Indented = false });

        _writer.WriteStartObject();
        _writer.WritePropertyName("session");
        JsonSerializer.Serialize(_writer, metadata, _options);
        _writer.WritePropertyName("samples");
        _writer.WriteStartArray();
        _writer.Flush();
    }

    public async Task WriteSampleAsync(TelemetryLogEntry entry, CancellationToken token)
    {
        if (_isClosed)
        {
            return;
        }

        JsonSerializer.Serialize(_writer, entry, _options);
        await _writer.FlushAsync(token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;
        _writer.WriteEndArray();
        _writer.WriteEndObject();
        await _writer.FlushAsync();
        _writer.Dispose();
        await _stream.DisposeAsync();
    }
}
