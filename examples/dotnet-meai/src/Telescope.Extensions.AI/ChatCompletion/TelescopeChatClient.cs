// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Telescope.Extensions.AI.Events;
using Telescope.Extensions.AI.Transport;

namespace Telescope.Extensions.AI.ChatCompletion;

/// <summary>
/// M.E.AI middleware that intercepts chat completions and emits
/// Telescope observability events via the named-pipe transport.
/// </summary>
/// <remarks>
/// If the Telescope service is unavailable the middleware silently
/// skips telemetry — it never breaks the inner <see cref="IChatClient"/> pipeline.
/// </remarks>
public sealed class TelescopeChatClient : DelegatingChatClient
{
    private static readonly ActivitySource s_activitySource = new("Telescope.Extensions.AI", "0.1.0");

    private readonly TelescopeChatClientOptions _options;
    private readonly ILogger _logger;
    private readonly TelescopeTransport _transport;

    // Session state — guarded by _stateLock.
    private readonly object _stateLock = new();
    private Guid _agentGuid;
    private Guid _sessionId;
    private uint _turnIndex;
    private bool _registered;
    private bool _registrationAttempted;

    public TelescopeChatClient(
        IChatClient innerClient,
        TelescopeChatClientOptions options,
        ILogger? logger = null)
        : base(innerClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger.Instance;
        _transport = new TelescopeTransport(options.Transport, _logger);

        _agentGuid = DeterministicGuid(options.AgentId);
        _sessionId = Guid.NewGuid();
    }

    // ── GetResponseAsync ────────────────────────────────────────────────────

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureRegisteredAsync(cancellationToken).ConfigureAwait(false);

        var turnId = Guid.NewGuid();
        uint turnIndex;
        Guid sessionId;
        lock (_stateLock)
        {
            turnIndex = _turnIndex++;
            sessionId = _sessionId;
        }

        // --- OTEL: Start turn activity ---
        using var turnActivity = _options.EmitOpenTelemetrySpans
            ? s_activitySource.StartActivity("gen_ai.chat", ActivityKind.Client)
            : null;

        if (turnActivity is not null)
        {
            turnActivity.SetTag("gen_ai.system", "telescope");
            turnActivity.SetTag("gen_ai.operation.name", "chat");
            turnActivity.SetTag("gen_ai.request.model", options?.ModelId);
            turnActivity.SetTag("telescope.session.id", sessionId.ToString());
            turnActivity.SetTag("telescope.turn.id", turnId.ToString());
            turnActivity.SetTag("telescope.turn.index", turnIndex);
            turnActivity.SetTag("telescope.agent.id", _options.AgentId);
            turnActivity.SetTag("telescope.agent.name", _options.AgentName);
        }

        // Emit user message + turn started.
        var lastUserMessage = GetLastUserMessage(messages);
        await EmitSafeAsync(new UserMessageEvent(
            SessionId: sessionId,
            TurnId: turnId,
            Content: _options.EnableSensitiveData ? lastUserMessage : null),
            cancellationToken).ConfigureAwait(false);

        await EmitSafeAsync(new TurnStartedEvent(
            SessionId: sessionId,
            TurnId: turnId,
            TurnIndex: turnIndex,
            ModelName: options?.ModelId),
            cancellationToken).ConfigureAwait(false);

        var sw = Stopwatch.StartNew();
        ChatResponse response;
        try
        {
            response = await base.GetResponseAsync(messages, options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            sw.Stop();

            // OTEL: Record error
            if (turnActivity is not null)
            {
                turnActivity.SetStatus(ActivityStatusCode.Error, ex.Message);
                turnActivity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
                {
                    { "exception.type", ex.GetType().FullName },
                    { "exception.message", ex.Message },
                }));
            }

            await EmitSafeAsync(new ErrorOccurredEvent(
                Message: ex.Message,
                TurnId: turnId,
                SessionId: sessionId,
                Category: "chat_completion"),
                cancellationToken).ConfigureAwait(false);

            await EmitSafeAsync(new TurnCompletedEvent(
                SessionId: sessionId,
                TurnId: turnId,
                Status: "error",
                TurnIndex: turnIndex,
                DurationMs: (uint)sw.ElapsedMilliseconds),
                cancellationToken).ConfigureAwait(false);

            throw;
        }

        sw.Stop();

        // Inspect response messages for tool calls / results.
        await EmitToolEventsAsync(response.Messages, turnId, sessionId, cancellationToken)
            .ConfigureAwait(false);

        // Token usage.
        if (response.Usage is { } usage)
        {
            // OTEL: Add token usage attributes
            if (turnActivity is not null)
            {
                if (usage.InputTokenCount.HasValue)
                    turnActivity.SetTag("gen_ai.usage.input_tokens", usage.InputTokenCount.Value);
                if (usage.OutputTokenCount.HasValue)
                    turnActivity.SetTag("gen_ai.usage.output_tokens", usage.OutputTokenCount.Value);
                if (usage.TotalTokenCount.HasValue)
                    turnActivity.SetTag("gen_ai.usage.total_tokens", usage.TotalTokenCount.Value);
            }

            await EmitSafeAsync(new TokenUsageReportedEvent(
                TurnId: turnId,
                InputTokens: usage.InputTokenCount.HasValue ? (ulong)usage.InputTokenCount.Value : null,
                OutputTokens: usage.OutputTokenCount.HasValue ? (ulong)usage.OutputTokenCount.Value : null),
                cancellationToken).ConfigureAwait(false);
        }

        // Turn completed.
        var assistantText = _options.EnableSensitiveData
            ? GetAssistantText(response.Messages)
            : null;

        var tokensNode = BuildTokensNode(response.Usage);

        // OTEL: Set response model and status
        if (turnActivity is not null)
        {
            turnActivity.SetTag("gen_ai.response.model", response.ModelId);
            turnActivity.SetStatus(ActivityStatusCode.Ok);
        }

        await EmitSafeAsync(new TurnCompletedEvent(
            SessionId: sessionId,
            TurnId: turnId,
            Status: response.FinishReason?.ToString()?.ToLowerInvariant() ?? "completed",
            TurnIndex: turnIndex,
            UserMessage: _options.EnableSensitiveData ? lastUserMessage : null,
            AssistantResponse: assistantText,
            ModelName: response.ModelId,
            Tokens: tokensNode,
            DurationMs: (uint)sw.ElapsedMilliseconds),
            cancellationToken).ConfigureAwait(false);

        return response;
    }

    // ── GetStreamingResponseAsync ───────────────────────────────────────────

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureRegisteredAsync(cancellationToken).ConfigureAwait(false);

        var turnId = Guid.NewGuid();
        uint turnIndex;
        Guid sessionId;
        lock (_stateLock)
        {
            turnIndex = _turnIndex++;
            sessionId = _sessionId;
        }

        var lastUserMessage = GetLastUserMessage(messages);

        // --- OTEL: Start turn activity for streaming ---
        var turnActivity = _options.EmitOpenTelemetrySpans
            ? s_activitySource.StartActivity("gen_ai.chat", ActivityKind.Client)
            : null;

        if (turnActivity is not null)
        {
            turnActivity.SetTag("gen_ai.system", "telescope");
            turnActivity.SetTag("gen_ai.operation.name", "chat");
            turnActivity.SetTag("gen_ai.request.model", options?.ModelId);
            turnActivity.SetTag("telescope.session.id", sessionId.ToString());
            turnActivity.SetTag("telescope.turn.id", turnId.ToString());
            turnActivity.SetTag("telescope.turn.index", turnIndex);
            turnActivity.SetTag("telescope.agent.id", _options.AgentId);
            turnActivity.SetTag("telescope.agent.name", _options.AgentName);
        }

        await EmitSafeAsync(new UserMessageEvent(
            SessionId: sessionId,
            TurnId: turnId,
            Content: _options.EnableSensitiveData ? lastUserMessage : null),
            cancellationToken).ConfigureAwait(false);

        await EmitSafeAsync(new TurnStartedEvent(
            SessionId: sessionId,
            TurnId: turnId,
            TurnIndex: turnIndex,
            ModelName: options?.ModelId),
            cancellationToken).ConfigureAwait(false);

        var sw = Stopwatch.StartNew();
        string? modelId = null;
        ChatFinishReason? finishReason = null;
        long? totalInputTokens = null;
        long? totalOutputTokens = null;
        bool errorOccurred = false;

        IAsyncEnumerable<ChatResponseUpdate> stream;
        try
        {
            stream = base.GetStreamingResponseAsync(messages, options, cancellationToken);
        }
        catch (Exception ex)
        {
            sw.Stop();

            // OTEL: Record error
            if (turnActivity is not null)
            {
                turnActivity.SetStatus(ActivityStatusCode.Error, ex.Message);
                turnActivity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
                {
                    { "exception.type", ex.GetType().FullName },
                    { "exception.message", ex.Message },
                }));
                turnActivity.Dispose();
            }

            await EmitSafeAsync(new ErrorOccurredEvent(
                Message: ex.Message,
                TurnId: turnId,
                SessionId: sessionId,
                Category: "chat_completion_stream"),
                cancellationToken).ConfigureAwait(false);

            await EmitSafeAsync(new TurnCompletedEvent(
                SessionId: sessionId,
                TurnId: turnId,
                Status: "error",
                TurnIndex: turnIndex,
                DurationMs: (uint)sw.ElapsedMilliseconds),
                cancellationToken).ConfigureAwait(false);

            throw;
        }

        await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            ChatResponseUpdate update;
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    break;
                }

                update = enumerator.Current;
            }
            catch (Exception ex)
            {
                errorOccurred = true;
                sw.Stop();

                // OTEL: Record streaming error
                if (turnActivity is not null)
                {
                    turnActivity.SetStatus(ActivityStatusCode.Error, ex.Message);
                    turnActivity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
                    {
                        { "exception.type", ex.GetType().FullName },
                        { "exception.message", ex.Message },
                    }));
                    turnActivity.Dispose();
                }

                await EmitSafeAsync(new ErrorOccurredEvent(
                    Message: ex.Message,
                    TurnId: turnId,
                    SessionId: sessionId,
                    Category: "chat_completion_stream"),
                    cancellationToken).ConfigureAwait(false);

                await EmitSafeAsync(new TurnCompletedEvent(
                    SessionId: sessionId,
                    TurnId: turnId,
                    Status: "error",
                    TurnIndex: turnIndex,
                    DurationMs: (uint)sw.ElapsedMilliseconds),
                    cancellationToken).ConfigureAwait(false);

                throw;
            }

            // Track metadata from the stream.
            modelId ??= update.ModelId;
            finishReason = update.FinishReason ?? finishReason;

            // Extract token usage from UsageContent in streaming updates.
            foreach (var content in update.Contents)
            {
                if (content is UsageContent usageContent && usageContent.Details is { } usage)
                {
                    if (usage.InputTokenCount.HasValue)
                        totalInputTokens = (totalInputTokens ?? 0) + usage.InputTokenCount.Value;
                    if (usage.OutputTokenCount.HasValue)
                        totalOutputTokens = (totalOutputTokens ?? 0) + usage.OutputTokenCount.Value;
                }
            }

            // Emit tool call events from streaming content.
            foreach (var content in update.Contents)
            {
                if (content is FunctionCallContent fcc)
                {
                    var effectId = DeterministicGuid(fcc.CallId ?? Guid.NewGuid().ToString());
                    var argsNode = fcc.Arguments is { Count: > 0 }
                        ? JsonSerializer.SerializeToNode(fcc.Arguments, EventSerializer.Options)
                        : null;

                    await EmitSafeAsync(new ToolCallStartedEvent(
                        TurnId: turnId,
                        EffectId: effectId,
                        Name: fcc.Name ?? "unknown",
                        Arguments: argsNode,
                        SessionId: sessionId),
                        cancellationToken).ConfigureAwait(false);
                }
                else if (content is FunctionResultContent frc)
                {
                    var effectId = DeterministicGuid(frc.CallId ?? Guid.NewGuid().ToString());
                    var resultNode = frc.Result is not null
                        ? JsonSerializer.SerializeToNode(frc.Result.ToString(), EventSerializer.Options)
                        : null;

                    await EmitSafeAsync(new ToolCallCompletedEvent(
                        EffectId: effectId,
                        Status: "completed",
                        Result: resultNode),
                        cancellationToken).ConfigureAwait(false);
                }
            }

            yield return update;
        }

        if (!errorOccurred)
        {
            sw.Stop();

            if (totalInputTokens.HasValue || totalOutputTokens.HasValue)
            {
                // OTEL: Add token usage attributes for streaming
                if (turnActivity is not null)
                {
                    if (totalInputTokens.HasValue)
                        turnActivity.SetTag("gen_ai.usage.input_tokens", totalInputTokens.Value);
                    if (totalOutputTokens.HasValue)
                        turnActivity.SetTag("gen_ai.usage.output_tokens", totalOutputTokens.Value);
                }

                await EmitSafeAsync(new TokenUsageReportedEvent(
                    TurnId: turnId,
                    InputTokens: totalInputTokens.HasValue ? (ulong)totalInputTokens.Value : null,
                    OutputTokens: totalOutputTokens.HasValue ? (ulong)totalOutputTokens.Value : null),
                    cancellationToken).ConfigureAwait(false);
            }

            // OTEL: Set response model and status for streaming
            if (turnActivity is not null)
            {
                turnActivity.SetTag("gen_ai.response.model", modelId);
                turnActivity.SetStatus(ActivityStatusCode.Ok);
                turnActivity.Dispose();
            }

            await EmitSafeAsync(new TurnCompletedEvent(
                SessionId: sessionId,
                TurnId: turnId,
                Status: finishReason?.ToString()?.ToLowerInvariant() ?? "completed",
                TurnIndex: turnIndex,
                ModelName: modelId,
                Tokens: BuildTokensNode(totalInputTokens, totalOutputTokens),
                DurationMs: (uint)sw.ElapsedMilliseconds),
                cancellationToken).ConfigureAwait(false);
        }
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                _transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error disposing Telescope transport");
            }
        }

        base.Dispose(disposing);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task EnsureRegisteredAsync(CancellationToken cancellationToken)
    {
        if (_registered)
        {
            return;
        }

        lock (_stateLock)
        {
            if (_registrationAttempted)
            {
                return;
            }

            _registrationAttempted = true;
        }

        try
        {
            await _transport.ConnectAndRegisterAsync(
                collectorName: _options.CollectorName,
                version: _options.CollectorVersion,
                description: $"M.E.AI middleware for {_options.AgentName}",
                agentId: _agentGuid.ToString(),
                agentName: _options.AgentName,
                agentType: _options.AgentType,
                agentVersion: _options.AgentVersion,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Emit session started.
            await EmitDirectAsync(new SessionStartedEvent(
                SessionId: _sessionId,
                AgentId: _agentGuid,
                Cwd: Environment.CurrentDirectory),
                cancellationToken).ConfigureAwait(false);

            _registered = true;
            _logger.LogDebug("Telescope middleware registered. SessionId={SessionId}", _sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Telescope service — telemetry will be skipped");
        }
    }

    /// <summary>
    /// Emits an event, swallowing any transport errors so the AI pipeline is never broken.
    /// </summary>
    private async Task EmitSafeAsync(EventKind eventKind, CancellationToken cancellationToken)
    {
        if (!_registered)
        {
            return;
        }

        try
        {
            await EmitDirectAsync(eventKind, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to emit Telescope event {EventType}", eventKind.GetType().Name);
        }
    }

    /// <summary>
    /// Serializes and submits a single event via the transport.
    /// </summary>
    private async Task EmitDirectAsync(EventKind eventKind, CancellationToken cancellationToken)
    {
        var json = EventSerializer.Serialize(eventKind);
        var node = JsonNode.Parse(json)!;
        await _transport.SubmitEventsAsync([node], cancellationToken).ConfigureAwait(false);
    }

    private async Task EmitToolEventsAsync(
        IList<ChatMessage> responseMessages,
        Guid turnId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        foreach (var msg in responseMessages)
        {
            foreach (var content in msg.Contents)
            {
                if (content is FunctionCallContent fcc)
                {
                    var effectId = DeterministicGuid(fcc.CallId ?? Guid.NewGuid().ToString());
                    var toolName = fcc.Name ?? "unknown";
                    var argsNode = fcc.Arguments is { Count: > 0 }
                        ? JsonSerializer.SerializeToNode(fcc.Arguments, EventSerializer.Options)
                        : null;

                    // OTEL: Start tool call activity
                    using var toolActivity = _options.EmitOpenTelemetrySpans
                        ? s_activitySource.StartActivity($"gen_ai.tool.{toolName}", ActivityKind.Internal)
                        : null;

                    if (toolActivity is not null)
                    {
                        toolActivity.SetTag("gen_ai.tool.name", toolName);
                        toolActivity.SetTag("telescope.effect.id", effectId.ToString());
                    }

                    await EmitSafeAsync(new ToolCallStartedEvent(
                        TurnId: turnId,
                        EffectId: effectId,
                        Name: toolName,
                        Arguments: argsNode,
                        SessionId: sessionId),
                        cancellationToken).ConfigureAwait(false);
                }
                else if (content is FunctionResultContent frc)
                {
                    var effectId = DeterministicGuid(frc.CallId ?? Guid.NewGuid().ToString());
                    var resultNode = frc.Result is not null
                        ? JsonSerializer.SerializeToNode(frc.Result.ToString(), EventSerializer.Options)
                        : null;

                    await EmitSafeAsync(new ToolCallCompletedEvent(
                        EffectId: effectId,
                        Status: "completed",
                        Result: resultNode),
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private static string? GetLastUserMessage(IEnumerable<ChatMessage> messages)
    {
        string? last = null;
        foreach (var msg in messages)
        {
            if (msg.Role == ChatRole.User)
            {
                last = msg.Text;
            }
        }

        return last;
    }

    private static string? GetAssistantText(IList<ChatMessage> messages)
    {
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Role == ChatRole.Assistant && messages[i].Text is { Length: > 0 } text)
            {
                return text;
            }
        }

        return null;
    }

    private static JsonNode? BuildTokensNode(UsageDetails? usage)
    {
        if (usage is null)
        {
            return null;
        }

        return BuildTokensNode(
            usage.InputTokenCount.HasValue ? (long)usage.InputTokenCount.Value : null,
            usage.OutputTokenCount.HasValue ? (long)usage.OutputTokenCount.Value : null);
    }

    private static JsonNode? BuildTokensNode(long? inputTokens, long? outputTokens)
    {
        if (inputTokens is null && outputTokens is null)
        {
            return null;
        }

        var obj = new JsonObject();
        if (inputTokens.HasValue)
            obj["input"] = inputTokens.Value;
        if (outputTokens.HasValue)
            obj["output"] = outputTokens.Value;
        return obj;
    }

    /// <summary>
    /// Generates a deterministic GUID (v5-style) from a string identifier,
    /// using a fixed namespace UUID for Telescope.
    /// </summary>
    private static Guid DeterministicGuid(string input)
    {
        // Use a fixed namespace for Telescope-generated UUIDs.
        ReadOnlySpan<byte> ns = [0x6b, 0xa7, 0xb8, 0x10, 0x9d, 0xad, 0x11, 0xd1,
                                 0x80, 0xb4, 0x00, 0xc0, 0x4f, 0xd4, 0x30, 0xc8];
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);

        var hashInput = new byte[ns.Length + inputBytes.Length];
        ns.CopyTo(hashInput);
        inputBytes.CopyTo(hashInput, ns.Length);

        var hash = System.Security.Cryptography.SHA1.HashData(hashInput);

        // Set version (5) and variant bits.
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new Guid(hash.AsSpan(0, 16));
    }
}
