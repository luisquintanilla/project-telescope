using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Telescope.Extensions.AI.Events;
using Telescope.Extensions.AI.Transport;

namespace Telescope.Extensions.AI.Embeddings;

/// <summary>
/// Delegating embedding generator that reports telemetry events to the Telescope service.
/// If the Telescope service is unavailable, the embedding pipeline continues unaffected.
/// </summary>
public sealed class TelescopeEmbeddingGenerator : DelegatingEmbeddingGenerator<string, Embedding<float>>
{
    private readonly TelescopeEmbeddingGeneratorOptions _options;
    private readonly TelescopeTransport _transport;
    private readonly Guid _sessionId = Guid.NewGuid();
    private bool _initialized;

    public TelescopeEmbeddingGenerator(
        IEmbeddingGenerator<string, Embedding<float>> innerGenerator,
        TelescopeEmbeddingGeneratorOptions options)
        : base(innerGenerator)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _transport = new TelescopeTransport(options.Transport);
    }

    public override async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var turnId = Guid.NewGuid();
        var inputValues = values as IList<string> ?? values.ToList();
        var metadata = this.GetService<EmbeddingGeneratorMetadata>();
        var modelName = options?.ModelId ?? metadata?.DefaultModelId ?? "unknown";
        var stopwatch = Stopwatch.StartNew();

        await TrySubmitEventAsync(new ModelUsedEvent(
            SessionId: _sessionId,
            Name: modelName,
            Provider: metadata?.ProviderUri?.ToString()
        ), cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await base.GenerateAsync(inputValues, options, cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();

            if (result.Usage is { } usage)
            {
                await TrySubmitEventAsync(new TokenUsageReportedEvent(
                    TurnId: turnId,
                    InputTokens: usage.InputTokenCount.HasValue
                        ? (ulong)usage.InputTokenCount.Value
                        : null
                ), cancellationToken).ConfigureAwait(false);
            }

            var dimensions = result.Count > 0 ? result[0].Vector.Length : 0;
            var data = new JsonObject
            {
                ["input_count"] = inputValues.Count,
                ["embedding_count"] = result.Count,
                ["dimensions"] = dimensions,
                ["model"] = modelName,
                ["duration_ms"] = stopwatch.ElapsedMilliseconds,
            };

            await TrySubmitEventAsync(new CustomEvent(
                EventType: "embedding_generated",
                Data: data
            ), cancellationToken).ConfigureAwait(false);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();

            await TrySubmitEventAsync(new ErrorOccurredEvent(
                Message: ex.Message,
                SessionId: _sessionId,
                Category: "embedding_generation"
            ), cancellationToken).ConfigureAwait(false);

            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            await _transport.ConnectAndRegisterAsync(
                collectorName: _options.CollectorName,
                version: _options.CollectorVersion,
                description: "Microsoft.Extensions.AI Embedding Generator Telescope middleware",
                agentId: _options.AgentId,
                agentName: _options.AgentName,
                agentType: _options.AgentType,
                agentVersion: _options.AgentVersion,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            _initialized = true;
        }
        catch
        {
            // Telescope unavailable — do not break the embedding pipeline.
        }
    }

    private async Task TrySubmitEventAsync(EventKind eventKind, CancellationToken cancellationToken)
    {
        if (!_transport.IsConnected)
        {
            return;
        }

        try
        {
            var json = EventSerializer.Serialize(eventKind);
            var node = JsonNode.Parse(json)!;
            await _transport.SubmitEventsAsync([node], cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Telescope errors must not break the embedding pipeline.
        }
    }
}
