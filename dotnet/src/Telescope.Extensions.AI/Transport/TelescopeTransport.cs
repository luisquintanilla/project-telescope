using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Telescope.Extensions.AI.Transport;

/// <summary>
/// Connects to the Telescope service over named pipes and implements
/// the length-prefixed JSON-RPC-inspired protocol.
/// </summary>
public sealed class TelescopeTransport : IAsyncDisposable
{
    private const int MaxFrameSize = 16 * 1024 * 1024; // 16 MiB
    private const int LengthPrefixSize = 4;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly TelescopeTransportOptions _options;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private NamedPipeClientStream? _pipe;
    private string? _collectorId;
    private bool _disposed;

    public TelescopeTransport(TelescopeTransportOptions? options = null, ILogger? logger = null)
    {
        _options = options ?? new TelescopeTransportOptions();
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// The collector ID assigned by the Telescope service after registration.
    /// </summary>
    public string? CollectorId => _collectorId;

    /// <summary>
    /// Whether the transport is currently connected to the service.
    /// </summary>
    public bool IsConnected => _pipe is { IsConnected: true };

    /// <summary>
    /// Connects to the Telescope service via named pipe and registers this collector.
    /// </summary>
    public async Task ConnectAndRegisterAsync(
        string collectorName,
        string version,
        string description,
        string agentId,
        string agentName,
        string agentType,
        string? agentVersion = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await ConnectWithRetryAsync(cancellationToken).ConfigureAwait(false);

        var registrationParams = new JsonObject
        {
            ["name"] = collectorName,
            ["version"] = version,
            ["description"] = description,
            ["agent"] = new JsonObject
            {
                ["agent_id"] = agentId,
                ["name"] = agentName,
                ["agent_type"] = agentType,
                ["version"] = agentVersion,
            },
            ["pid"] = Environment.ProcessId,
            ["expected_interval_secs"] = null,
        };

        var response = await SendRequestAsync("collector.register", registrationParams, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsError)
        {
            throw new InvalidOperationException(
                $"Registration failed: [{response.Error!.Code}] {response.Error.Message}");
        }

        _collectorId = response.Result?["collector_id"]?.GetValue<string>();

        var maxBatch = response.Result?["max_batch_size"]?.GetValue<int>();
        if (maxBatch.HasValue && maxBatch.Value < _options.MaxBatchSize)
        {
            _options.MaxBatchSize = maxBatch.Value;
        }

        _logger.LogInformation(
            "Registered with Telescope service. CollectorId={CollectorId}, MaxBatchSize={MaxBatchSize}",
            _collectorId, _options.MaxBatchSize);
    }

    /// <summary>
    /// Submits events to the Telescope service, batching automatically if needed.
    /// Returns the total number of accepted events.
    /// </summary>
    public async Task<int> SubmitEventsAsync(
        IReadOnlyList<JsonNode> events,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (events.Count == 0)
        {
            return 0;
        }

        int totalAccepted = 0;

        for (int offset = 0; offset < events.Count; offset += _options.MaxBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int count = Math.Min(_options.MaxBatchSize, events.Count - offset);
            var batch = new JsonArray();
            for (int i = 0; i < count; i++)
            {
                // DeepClone to avoid "node already has a parent" errors
                batch.Add(events[offset + i].DeepClone());
            }

            var submitParams = new JsonObject
            {
                ["events"] = batch,
            };

            var response = await SendRequestAsync("collector.submit", submitParams, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsError)
            {
                _logger.LogWarning(
                    "Submit failed: [{Code}] {Message}",
                    response.Error!.Code, response.Error.Message);
                throw new InvalidOperationException(
                    $"Submit failed: [{response.Error.Code}] {response.Error.Message}");
            }

            int accepted = response.Result?["accepted"]?.GetValue<int>() ?? 0;
            totalAccepted += accepted;

            // Respect backpressure from the service
            int delayHintMs = response.Result?["delay_hint_ms"]?.GetValue<int>() ?? 0;
            if (delayHintMs > 0)
            {
                _logger.LogDebug("Backpressure: delaying {DelayMs}ms before next batch", delayHintMs);
                await Task.Delay(delayHintMs, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogDebug("Submitted {Accepted}/{Total} events", totalAccepted, events.Count);
        return totalAccepted;
    }

    /// <summary>
    /// Sends a heartbeat to the Telescope service.
    /// </summary>
    public async Task HeartbeatAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var response = await SendRequestAsync("collector.heartbeat", null, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsError)
        {
            _logger.LogWarning(
                "Heartbeat failed: [{Code}] {Message}",
                response.Error!.Code, response.Error.Message);
        }
    }

    /// <summary>
    /// Deregisters from the Telescope service.
    /// </summary>
    public async Task DeregisterAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected)
        {
            return;
        }

        try
        {
            var response = await SendRequestAsync("collector.deregister", null, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsError)
            {
                _logger.LogWarning(
                    "Deregister failed: [{Code}] {Message}",
                    response.Error!.Code, response.Error.Message);
            }
            else
            {
                _logger.LogInformation("Deregistered from Telescope service");
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            _logger.LogDebug(ex, "Pipe disconnected during deregister (expected during shutdown)");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            await DeregisterAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error during deregister on dispose");
        }

        if (_pipe is not null)
        {
            await _pipe.DisposeAsync().ConfigureAwait(false);
            _pipe = null;
        }

        _sendLock.Dispose();
    }

    private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        int attempt = 0;
        int delayMs = 100;

        while (true)
        {
            attempt++;
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _pipe = new NamedPipeClientStream(
                    serverName: ".",
                    pipeName: _options.PipeName,
                    direction: PipeDirection.InOut,
                    options: PipeOptions.Asynchronous);

                await _pipe.ConnectAsync((int)_options.ConnectTimeout.TotalMilliseconds, cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogDebug("Connected to pipe '{PipeName}' on attempt {Attempt}", _options.PipeName, attempt);
                return;
            }
            catch (TimeoutException) when (attempt < _options.MaxConnectRetries)
            {
                _logger.LogDebug(
                    "Connection attempt {Attempt}/{Max} timed out, retrying in {Delay}ms",
                    attempt, _options.MaxConnectRetries, delayMs);

                if (_pipe is not null)
                {
                    await _pipe.DisposeAsync().ConfigureAwait(false);
                    _pipe = null;
                }

                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                delayMs = Math.Min(delayMs * 2, 5000); // exponential backoff, cap at 5s
            }
        }
    }

    /// <summary>
    /// Sends a JSON-RPC-style request and reads the response.
    /// Thread-safe: uses a semaphore to serialize pipe access.
    /// </summary>
    private async Task<IpcResponse> SendRequestAsync(
        string method,
        JsonNode? parameters,
        CancellationToken cancellationToken)
    {
        var request = new IpcRequest
        {
            Method = method,
            Params = parameters,
        };

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(request, s_jsonOptions);

        if (payload.Length > MaxFrameSize)
        {
            throw new InvalidOperationException(
                $"Request payload ({payload.Length} bytes) exceeds maximum frame size ({MaxFrameSize} bytes)");
        }

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_pipe is null || !_pipe.IsConnected)
            {
                throw new InvalidOperationException("Not connected to the Telescope service");
            }

            // Write length-prefixed frame
            byte[] lengthPrefix = new byte[LengthPrefixSize];
            BinaryPrimitives.WriteInt32LittleEndian(lengthPrefix, payload.Length);

            await _pipe.WriteAsync(lengthPrefix, cancellationToken).ConfigureAwait(false);
            await _pipe.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await _pipe.FlushAsync(cancellationToken).ConfigureAwait(false);

            // Read response frame
            byte[] responseLengthBuf = new byte[LengthPrefixSize];
            await ReadExactAsync(_pipe, responseLengthBuf, cancellationToken).ConfigureAwait(false);

            int responseLength = BinaryPrimitives.ReadInt32LittleEndian(responseLengthBuf);

            if (responseLength <= 0 || responseLength > MaxFrameSize)
            {
                throw new InvalidOperationException(
                    $"Invalid response frame length: {responseLength}");
            }

            byte[] responsePayload = new byte[responseLength];
            await ReadExactAsync(_pipe, responsePayload, cancellationToken).ConfigureAwait(false);

            var response = JsonSerializer.Deserialize<IpcResponse>(responsePayload, s_jsonOptions);
            return response ?? throw new InvalidOperationException("Received null response from Telescope service");
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Reads exactly <paramref name="buffer"/>.Length bytes from the stream.
    /// </summary>
    private static async Task ReadExactAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int bytesRead = await stream.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead),
                cancellationToken).ConfigureAwait(false);

            if (bytesRead == 0)
            {
                throw new EndOfStreamException(
                    $"Telescope service closed the connection (read {totalRead}/{buffer.Length} bytes)");
            }

            totalRead += bytesRead;
        }
    }
}
