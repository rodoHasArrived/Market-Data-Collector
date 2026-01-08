using System.Buffers;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using MarketDataCollector.Application.Logging;
using Serilog;

namespace MarketDataCollector.Infrastructure.Performance;

/// <summary>
/// High-performance WebSocket processor using System.IO.Pipelines for zero-copy parsing.
/// Dramatically reduces memory allocations in the hot path compared to StringBuilder approach.
///
/// Based on: https://github.com/dotnet/runtime (System.IO.Pipelines) (MIT)
/// Reference: docs/open-source-references.md #12
/// </summary>
public sealed class PipelinesWebSocketProcessor : IAsyncDisposable
{
    private readonly ILogger _log = LoggingSetup.ForContext<PipelinesWebSocketProcessor>();
    private readonly Pipe _pipe;
    private readonly WebSocket _webSocket;
    private readonly CancellationTokenSource _cts;
    private readonly Task _readTask;
    private readonly Func<ReadOnlySequence<byte>, Task> _messageHandler;
    private readonly PipelinesWebSocketOptions _options;
    private bool _disposed;

    // Pool for buffer segments to reduce allocations
    private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;

    public PipelinesWebSocketProcessor(
        WebSocket webSocket,
        Func<ReadOnlySequence<byte>, Task> messageHandler,
        PipelinesWebSocketOptions? options = null)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        _options = options ?? PipelinesWebSocketOptions.Default;

        // Configure pipe with appropriate pause/resume thresholds
        var pipeOptions = new PipeOptions(
            pool: MemoryPool<byte>.Shared,
            readerScheduler: PipeScheduler.ThreadPool,
            writerScheduler: PipeScheduler.Inline,
            pauseWriterThreshold: _options.PauseWriterThreshold,
            resumeWriterThreshold: _options.ResumeWriterThreshold,
            minimumSegmentSize: _options.MinimumSegmentSize,
            useSynchronizationContext: false);

        _pipe = new Pipe(pipeOptions);
        _cts = new CancellationTokenSource();

        // Start the read loop
        _readTask = Task.Run(() => ProcessMessagesAsync(_cts.Token));
    }

    /// <summary>
    /// Fill the pipe with data from the WebSocket.
    /// </summary>
    public async Task FillPipeAsync(CancellationToken ct)
    {
        var writer = _pipe.Writer;
        var combinedCt = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token).Token;

        try
        {
            while (!combinedCt.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                // Get memory from the pipe writer (zero-copy)
                var memory = writer.GetMemory(_options.MinimumSegmentSize);

                try
                {
                    var result = await _webSocket.ReceiveAsync(memory, combinedCt).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _log.Information("WebSocket closed by remote endpoint");
                        break;
                    }

                    if (result.Count == 0)
                        continue;

                    // Tell the writer how much data was written
                    writer.Advance(result.Count);

                    if (result.EndOfMessage)
                    {
                        // Flush to make data available to the reader
                        var flushResult = await writer.FlushAsync(combinedCt).ConfigureAwait(false);

                        if (flushResult.IsCompleted || flushResult.IsCanceled)
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    _log.Warning("WebSocket connection closed prematurely");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error in WebSocket fill pipe loop");
        }
        finally
        {
            await writer.CompleteAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Process messages from the pipe.
    /// </summary>
    private async Task ProcessMessagesAsync(CancellationToken ct)
    {
        var reader = _pipe.Reader;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(ct).ConfigureAwait(false);
                var buffer = result.Buffer;

                try
                {
                    // Process complete JSON messages (newline-delimited)
                    while (TryReadMessage(ref buffer, out var message))
                    {
                        await _messageHandler(message).ConfigureAwait(false);
                    }
                }
                finally
                {
                    // Tell the reader how much data was consumed
                    reader.AdvanceTo(buffer.Start, buffer.End);
                }

                if (result.IsCompleted)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error processing WebSocket messages");
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Try to read a complete message from the buffer.
    /// </summary>
    private static bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> message)
    {
        // Look for newline delimiter (for JSONL or newline-delimited messages)
        var position = buffer.PositionOf((byte)'\n');

        if (position == null)
        {
            // Also check for complete JSON objects without newline
            if (buffer.Length > 0 && IsCompleteJsonObject(buffer))
            {
                message = buffer;
                buffer = buffer.Slice(buffer.End);
                return true;
            }

            message = default;
            return false;
        }

        // Extract the message up to the newline
        message = buffer.Slice(0, position.Value);
        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        return true;
    }

    /// <summary>
    /// Check if the buffer contains a complete JSON object.
    /// </summary>
    private static bool IsCompleteJsonObject(ReadOnlySequence<byte> buffer)
    {
        if (buffer.Length < 2) return false;

        var reader = new SequenceReader<byte>(buffer);

        // Skip whitespace
        while (reader.TryPeek(out var b) && (b == ' ' || b == '\t' || b == '\r' || b == '\n'))
            reader.Advance(1);

        if (!reader.TryPeek(out var first)) return false;

        if (first == '{')
        {
            // Count braces
            int depth = 0;
            bool inString = false;
            bool escape = false;

            while (reader.TryRead(out var c))
            {
                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (!inString)
                {
                    if (c == '{') depth++;
                    else if (c == '}') depth--;

                    if (depth == 0) return true;
                }
            }
        }
        else if (first == '[')
        {
            // Count brackets
            int depth = 0;
            bool inString = false;
            bool escape = false;

            while (reader.TryRead(out var c))
            {
                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (c == '\\' && inString)
                {
                    escape = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (!inString)
                {
                    if (c == '[') depth++;
                    else if (c == ']') depth--;

                    if (depth == 0) return true;
                }
            }
        }

        return false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();

        try
        {
            await _readTask.ConfigureAwait(false);
        }
        catch
        {
            // Ignore disposal errors
        }

        _cts.Dispose();
    }
}

/// <summary>
/// High-performance JSON parser for WebSocket messages using Utf8JsonReader.
/// </summary>
public static class PipelinesJsonParser
{
    /// <summary>
    /// Parse a JSON message from a ReadOnlySequence without allocating strings.
    /// </summary>
    public static T? Parse<T>(ReadOnlySequence<byte> buffer) where T : class
    {
        var reader = new Utf8JsonReader(buffer);
        return JsonSerializer.Deserialize<T>(ref reader);
    }

    /// <summary>
    /// Parse a JSON message and extract specific fields without full deserialization.
    /// </summary>
    public static bool TryGetProperty(
        ReadOnlySequence<byte> buffer,
        ReadOnlySpan<byte> propertyName,
        out JsonElement value)
    {
        var reader = new Utf8JsonReader(buffer);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            value = default;
            return false;
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals(propertyName))
                {
                    reader.Read();
                    using var doc = JsonDocument.ParseValue(ref reader);
                    value = doc.RootElement.Clone();
                    return true;
                }

                // Skip the value
                reader.Skip();
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Extract the message type from an Alpaca-style message (T field).
    /// </summary>
    public static bool TryGetMessageType(ReadOnlySequence<byte> buffer, out string? messageType)
    {
        var propertyName = "T"u8;

        if (TryGetProperty(buffer, propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            messageType = value.GetString();
            return true;
        }

        messageType = null;
        return false;
    }

    /// <summary>
    /// Extract the symbol from a market data message (S field).
    /// </summary>
    public static bool TryGetSymbol(ReadOnlySequence<byte> buffer, out string? symbol)
    {
        var propertyName = "S"u8;

        if (TryGetProperty(buffer, propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            symbol = value.GetString();
            return true;
        }

        symbol = null;
        return false;
    }
}

/// <summary>
/// Configuration options for the pipelines WebSocket processor.
/// </summary>
public sealed class PipelinesWebSocketOptions
{
    /// <summary>
    /// Threshold at which the pipe writer pauses.
    /// </summary>
    public long PauseWriterThreshold { get; init; } = 65536; // 64KB

    /// <summary>
    /// Threshold at which the pipe writer resumes.
    /// </summary>
    public long ResumeWriterThreshold { get; init; } = 32768; // 32KB

    /// <summary>
    /// Minimum segment size for buffer allocation.
    /// </summary>
    public int MinimumSegmentSize { get; init; } = 4096; // 4KB

    public static PipelinesWebSocketOptions Default => new();

    public static PipelinesWebSocketOptions HighThroughput => new()
    {
        PauseWriterThreshold = 262144, // 256KB
        ResumeWriterThreshold = 131072, // 128KB
        MinimumSegmentSize = 16384 // 16KB
    };

    public static PipelinesWebSocketOptions LowLatency => new()
    {
        PauseWriterThreshold = 16384, // 16KB
        ResumeWriterThreshold = 8192, // 8KB
        MinimumSegmentSize = 1024 // 1KB
    };
}

/// <summary>
/// Pooled buffer for zero-allocation message handling.
/// </summary>
public sealed class PooledMessageBuffer : IDisposable
{
    private byte[]? _buffer;
    private int _length;

    public PooledMessageBuffer(int capacity = 4096)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(capacity);
        _length = 0;
    }

    public Span<byte> Span => _buffer.AsSpan(0, _length);
    public Memory<byte> Memory => _buffer.AsMemory(0, _length);
    public int Length => _length;

    public void Write(ReadOnlySequence<byte> data)
    {
        var required = (int)data.Length;
        EnsureCapacity(required);
        data.CopyTo(_buffer.AsSpan(_length));
        _length += required;
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        EnsureCapacity(data.Length);
        data.CopyTo(_buffer.AsSpan(_length));
        _length += data.Length;
    }

    public void Reset()
    {
        _length = 0;
    }

    private void EnsureCapacity(int additional)
    {
        var required = _length + additional;
        if (_buffer == null || _buffer.Length < required)
        {
            var newSize = Math.Max(required, (_buffer?.Length ?? 0) * 2);
            var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);

            if (_buffer != null)
            {
                _buffer.AsSpan(0, _length).CopyTo(newBuffer);
                ArrayPool<byte>.Shared.Return(_buffer);
            }

            _buffer = newBuffer;
        }
    }

    public void Dispose()
    {
        if (_buffer != null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }
    }
}
