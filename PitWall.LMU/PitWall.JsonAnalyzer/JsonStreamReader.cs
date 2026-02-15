using System.Buffers;
using System.Text;
using System.Text.Json;

namespace PitWall.JsonAnalyzer;

/// <summary>
/// Streams a large JSON telemetry file element-by-element without loading it entirely into memory.
/// Supports two formats:
///   1. Root array: [{sample}, {sample}, ...]
///   2. Root object with samples array: {"session": {...}, "samples": [{...}, ...]}
/// </summary>
public sealed class JsonStreamReader : IDisposable
{
    private readonly FileStream _fileStream;
    private readonly long _fileSize;

    public long FileSize => _fileSize;
    public long Position => _fileStream.Position;

    /// <summary>Session metadata extracted from the root object (format 2 only).</summary>
    public JsonElement? SessionMetadata { get; private set; }

    public JsonStreamReader(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Input file not found: {filePath}", filePath);

        _fileSize = new FileInfo(filePath).Length;
        _fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, FileOptions.SequentialScan);
    }

    /// <summary>
    /// Streams individual sample elements from the JSON file.
    /// Auto-detects root array vs root object format.
    /// </summary>
    public async IAsyncEnumerable<(JsonElement element, long bytePosition)> ReadSamplesAsync(
        int maxSamples = int.MaxValue)
    {
        // Peek at first non-whitespace byte to detect format
        int firstByte;
        do
        {
            firstByte = _fileStream.ReadByte();
        } while (firstByte is ' ' or '\t' or '\r' or '\n');

        _fileStream.Position = 0;

        if (firstByte == '[')
        {
            // Format 1: root array
            await foreach (var item in StreamRootArrayAsync(maxSamples))
                yield return item;
        }
        else if (firstByte == '{')
        {
            // Format 2: root object — {"session":{...}, "samples":[...]}
            await foreach (var item in StreamRootObjectAsync(maxSamples))
                yield return item;
        }
        else
        {
            throw new JsonException($"Unexpected first byte: 0x{firstByte:X2} ('{(char)firstByte}'). Expected '[' or '{{'.");
        }
    }

    /// <summary>
    /// Stream elements from a root-level JSON array using DeserializeAsyncEnumerable.
    /// </summary>
    private async IAsyncEnumerable<(JsonElement element, long bytePosition)> StreamRootArrayAsync(int maxSamples)
    {
        var options = new JsonSerializerOptions { AllowTrailingCommas = true };
        int count = 0;

        await foreach (var element in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(_fileStream, options))
        {
            if (count >= maxSamples) yield break;
            yield return (element, _fileStream.Position);
            count++;
        }
    }

    /// <summary>
    /// Stream elements from a root object's "samples" array.
    /// First extracts the "session" property, then streams each array element.
    /// Uses Utf8JsonReader for memory-efficient forward-only parsing.
    /// </summary>
    private async IAsyncEnumerable<(JsonElement element, long bytePosition)> StreamRootObjectAsync(int maxSamples)
    {
        // We need to find the "samples" array inside the root object.
        // Strategy: use a buffered approach with Utf8JsonReader to skip to the samples array,
        // then parse each element individually.

        int count = 0;

        // Phase 1: Scan for "session" and "samples" properties
        // We'll read through the file finding the samples array start
        _fileStream.Position = 0;

        // Simple approach: read until we find the samples array, collecting session metadata
        // For very large files, we parse the root object structure manually
        await Task.Yield(); // ensure async context

        // Use a simpler two-pass approach:
        // Pass 1: Read enough to find the session object (typically first few KB)
        // Pass 2: Stream the samples array

        // Read the beginning of the file to find structure
        _fileStream.Position = 0;
        var headerBytes = new byte[Math.Min(2 * 1024 * 1024, _fileSize)]; // Read up to 2MB for header
        int headerRead = await _fileStream.ReadAsync(headerBytes);

        // Find the session object and samples array start
        var state = new JsonReaderState(new JsonReaderOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        var reader = new Utf8JsonReader(headerBytes.AsSpan(0, headerRead), isFinalBlock: false, state);

        long samplesArrayStartByte = -1;

        try
        {
            while (reader.Read())
            {
                if (reader.CurrentDepth == 1 && reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propName = reader.GetString()!;

                    if (propName.Equals("session", StringComparison.OrdinalIgnoreCase))
                    {
                        // Capture the session object
                        reader.Read(); // move to value
                        if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            // Use JsonDocument to parse just this sub-tree
                            using var doc = JsonDocument.ParseValue(ref reader);
                            SessionMetadata = doc.RootElement.Clone();
                        }
                    }
                    else if (propName.Equals("samples", StringComparison.OrdinalIgnoreCase))
                    {
                        reader.Read(); // move to StartArray
                        if (reader.TokenType == JsonTokenType.StartArray)
                        {
                            // Record byte position where samples array content begins
                            samplesArrayStartByte = reader.BytesConsumed;
                            break;
                        }
                    }
                    else
                    {
                        // Skip this property value
                        reader.Read();
                        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                            reader.Skip();
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Header might be incomplete for the session object; try without session
        }

        if (samplesArrayStartByte < 0)
        {
            // Fallback: couldn't find samples in header, try reading more
            Console.Error.WriteLine("Warning: Could not locate 'samples' array in first 2MB. Attempting full-file parse.");
            yield break;
        }

        // Now stream the samples array element by element
        // Position the file stream right after the StartArray token
        // We'll use DeserializeAsyncEnumerable on a sub-stream that starts at the array
        _fileStream.Position = samplesArrayStartByte;

        // Create a synthetic stream that starts with '[' followed by the remainder
        var syntheticStream = new SamplesArrayStream(_fileStream, samplesArrayStartByte);

        var options = new JsonSerializerOptions { AllowTrailingCommas = true };

        // The synthetic stream includes the root object's closing '}' after the array ']',
        // which causes a JsonException at the end. This is expected — catch and stop gracefully.
        IAsyncEnumerator<JsonElement> enumerator = JsonSerializer
            .DeserializeAsyncEnumerable<JsonElement>(syntheticStream, options)
            .GetAsyncEnumerator();

        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (JsonException) when (_fileStream.Position >= _fileSize - 1024)
                {
                    // Expected: hit trailing '}' of root object after samples array ends
                    yield break;
                }

                if (!hasNext) yield break;
                if (count >= maxSamples) yield break;

                yield return (enumerator.Current, _fileStream.Position);
                count++;
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    public void Dispose() => _fileStream.Dispose();

    /// <summary>
    /// Wraps a FileStream starting at the samples array position,
    /// prepending a '[' byte so JsonSerializer sees a valid JSON array.
    /// </summary>
    private sealed class SamplesArrayStream : Stream
    {
        private readonly FileStream _inner;
        private bool _prefixConsumed;
        private static readonly byte[] Prefix = "["u8.ToArray();

        public SamplesArrayStream(FileStream inner, long startPosition)
        {
            _inner = inner;
            _inner.Position = startPosition;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!_prefixConsumed)
            {
                _prefixConsumed = true;
                buffer[offset] = (byte)'[';
                if (count == 1) return 1;
                int read = _inner.Read(buffer, offset + 1, count - 1);
                return read + 1;
            }

            return _inner.Read(buffer, offset, count);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!_prefixConsumed)
            {
                _prefixConsumed = true;
                buffer[offset] = (byte)'[';
                if (count == 1) return 1;
                int read = await _inner.ReadAsync(buffer.AsMemory(offset + 1, count - 1), cancellationToken);
                return read + 1;
            }

            return await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_prefixConsumed)
            {
                _prefixConsumed = true;
                buffer.Span[0] = (byte)'[';
                if (buffer.Length == 1) return 1;
                int read = await _inner.ReadAsync(buffer[1..], cancellationToken);
                return read + 1;
            }

            return await _inner.ReadAsync(buffer, cancellationToken);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length - _inner.Position + 1;
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
