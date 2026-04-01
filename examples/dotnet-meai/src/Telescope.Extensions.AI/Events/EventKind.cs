using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Telescope.Extensions.AI.Events;

/// <summary>
/// Base type for all Telescope event kinds. Serializes to JSON wire-compatible with
/// the Rust <c>EventKind</c> enum using <c>#[serde(tag = "type", rename_all = "snake_case")]</c>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
// Session
[JsonDerivedType(typeof(SessionStartedEvent), "session_started")]
// Turn
[JsonDerivedType(typeof(UserMessageEvent), "user_message")]
[JsonDerivedType(typeof(TurnStartedEvent), "turn_started")]
[JsonDerivedType(typeof(TurnCompletedEvent), "turn_completed")]
// Tool
[JsonDerivedType(typeof(ToolCallStartedEvent), "tool_call_started")]
[JsonDerivedType(typeof(ToolCallCompletedEvent), "tool_call_completed")]
// Cost
[JsonDerivedType(typeof(TokenUsageReportedEvent), "token_usage_reported")]
// Error
[JsonDerivedType(typeof(ErrorOccurredEvent), "error_occurred")]
public abstract record EventKind;

// ── Session ─────────────────────────────────────────────────────────────────

public sealed record SessionStartedEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("agent_id")] Guid AgentId,
    [property: JsonPropertyName("cwd")] string? Cwd = null,
    [property: JsonPropertyName("git_repo")] string? GitRepo = null,
    [property: JsonPropertyName("git_branch")] string? GitBranch = null
) : EventKind;

// ── Turn ────────────────────────────────────────────────────────────────────

public sealed record UserMessageEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("content")] string? Content = null
) : EventKind;

public sealed record TurnStartedEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("turn_index")] uint TurnIndex,
    [property: JsonPropertyName("model_name")] string? ModelName = null
) : EventKind;

public sealed record TurnCompletedEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("turn_index")] uint? TurnIndex = null,
    [property: JsonPropertyName("user_message")] string? UserMessage = null,
    [property: JsonPropertyName("assistant_response")] string? AssistantResponse = null,
    [property: JsonPropertyName("model_name")] string? ModelName = null,
    [property: JsonPropertyName("tokens")] JsonNode? Tokens = null,
    [property: JsonPropertyName("duration_ms")] uint? DurationMs = null
) : EventKind;

// ── Tool ────────────────────────────────────────────────────────────────────

public sealed record ToolCallStartedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("effect_id")] Guid EffectId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] JsonNode? Arguments = null,
    [property: JsonPropertyName("session_id")] Guid? SessionId = null
) : EventKind;

public sealed record ToolCallCompletedEvent(
    [property: JsonPropertyName("effect_id")] Guid EffectId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("result")] JsonNode? Result = null,
    [property: JsonPropertyName("duration_ms")] uint? DurationMs = null
) : EventKind;

// ── Cost ────────────────────────────────────────────────────────────────────

public sealed record TokenUsageReportedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("input_tokens")] ulong? InputTokens = null,
    [property: JsonPropertyName("output_tokens")] ulong? OutputTokens = null,
    [property: JsonPropertyName("cache_read_tokens")] ulong? CacheReadTokens = null
) : EventKind;

// ── Error ───────────────────────────────────────────────────────────────────

public sealed record ErrorOccurredEvent(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("turn_id")] Guid? TurnId = null,
    [property: JsonPropertyName("session_id")] Guid? SessionId = null,
    [property: JsonPropertyName("category")] string? Category = null
) : EventKind;
