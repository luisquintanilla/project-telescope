using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Telescope.Extensions.AI.Events;

/// <summary>
/// Base type for all Telescope event kinds. Serializes to JSON wire-compatible with
/// the Rust <c>EventKind</c> enum using <c>#[serde(tag = "type", rename_all = "snake_case")]</c>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
// Agent
[JsonDerivedType(typeof(AgentDiscoveredEvent), "agent_discovered")]
[JsonDerivedType(typeof(AgentHeartbeatEvent), "agent_heartbeat")]
// Session
[JsonDerivedType(typeof(SessionStartedEvent), "session_started")]
[JsonDerivedType(typeof(SessionEndedEvent), "session_ended")]
[JsonDerivedType(typeof(SessionResumedEvent), "session_resumed")]
[JsonDerivedType(typeof(SessionMetadataUpdatedEvent), "session_metadata_updated")]
// Turn
[JsonDerivedType(typeof(UserMessageEvent), "user_message")]
[JsonDerivedType(typeof(TurnStartedEvent), "turn_started")]
[JsonDerivedType(typeof(TurnCompletedEvent), "turn_completed")]
[JsonDerivedType(typeof(TurnStreamingEvent), "turn_streaming")]
// Tool
[JsonDerivedType(typeof(ToolCallStartedEvent), "tool_call_started")]
[JsonDerivedType(typeof(ToolCallCompletedEvent), "tool_call_completed")]
// File
[JsonDerivedType(typeof(FileReadEvent), "file_read")]
[JsonDerivedType(typeof(FileWrittenEvent), "file_written")]
[JsonDerivedType(typeof(FileCreatedEvent), "file_created")]
[JsonDerivedType(typeof(FileDeletedEvent), "file_deleted")]
// Shell
[JsonDerivedType(typeof(ShellCommandStartedEvent), "shell_command_started")]
[JsonDerivedType(typeof(ShellCommandCompletedEvent), "shell_command_completed")]
// Sub-Agent
[JsonDerivedType(typeof(SubAgentSpawnedEvent), "sub_agent_spawned")]
[JsonDerivedType(typeof(SubAgentCompletedEvent), "sub_agent_completed")]
// Planning
[JsonDerivedType(typeof(PlanCreatedEvent), "plan_created")]
[JsonDerivedType(typeof(PlanStepCompletedEvent), "plan_step_completed")]
[JsonDerivedType(typeof(ThinkingBlockEvent), "thinking_block")]
// Context
[JsonDerivedType(typeof(ContextWindowSnapshotEvent), "context_window_snapshot")]
[JsonDerivedType(typeof(ContextPrunedEvent), "context_pruned")]
// Human-in-Loop
[JsonDerivedType(typeof(ApprovalRequestedEvent), "approval_requested")]
[JsonDerivedType(typeof(ApprovalGrantedEvent), "approval_granted")]
[JsonDerivedType(typeof(ApprovalDeniedEvent), "approval_denied")]
[JsonDerivedType(typeof(UserFeedbackEvent), "user_feedback")]
// Self-Report
[JsonDerivedType(typeof(IntentDeclaredEvent), "intent_declared")]
[JsonDerivedType(typeof(DecisionMadeEvent), "decision_made")]
[JsonDerivedType(typeof(ThoughtLoggedEvent), "thought_logged")]
[JsonDerivedType(typeof(FrustrationReportedEvent), "frustration_reported")]
[JsonDerivedType(typeof(OutcomeReportedEvent), "outcome_reported")]
[JsonDerivedType(typeof(ObservationLoggedEvent), "observation_logged")]
[JsonDerivedType(typeof(RecipeFollowedEvent), "recipe_followed")]
[JsonDerivedType(typeof(PathNotTakenEvent), "path_not_taken")]
[JsonDerivedType(typeof(ConfidenceAssessedEvent), "confidence_assessed")]
[JsonDerivedType(typeof(AssumptionMadeEvent), "assumption_made")]
// Model
[JsonDerivedType(typeof(ModelUsedEvent), "model_used")]
[JsonDerivedType(typeof(ModelSwitchedEvent), "model_switched")]
// Error
[JsonDerivedType(typeof(ErrorOccurredEvent), "error_occurred")]
[JsonDerivedType(typeof(RetryAttemptedEvent), "retry_attempted")]
// Code
[JsonDerivedType(typeof(SearchPerformedEvent), "search_performed")]
[JsonDerivedType(typeof(CodeChangeAppliedEvent), "code_change_applied")]
// Network
[JsonDerivedType(typeof(WebRequestMadeEvent), "web_request_made")]
[JsonDerivedType(typeof(McpServerConnectedEvent), "mcp_server_connected")]
// Cost
[JsonDerivedType(typeof(TokenUsageReportedEvent), "token_usage_reported")]
[JsonDerivedType(typeof(RateLimitHitEvent), "rate_limit_hit")]
// Git
[JsonDerivedType(typeof(GitCommitCreatedEvent), "git_commit_created")]
[JsonDerivedType(typeof(GitBranchCreatedEvent), "git_branch_created")]
[JsonDerivedType(typeof(PullRequestCreatedEvent), "pull_request_created")]
// Session Management
[JsonDerivedType(typeof(SessionModeChangedEvent), "session_mode_changed")]
[JsonDerivedType(typeof(CompactionStartedEvent), "compaction_started")]
[JsonDerivedType(typeof(CompactionCompletedEvent), "compaction_completed")]
// Hooks
[JsonDerivedType(typeof(HookStartedEvent), "hook_started")]
[JsonDerivedType(typeof(HookCompletedEvent), "hook_completed")]
[JsonDerivedType(typeof(SkillInvokedEvent), "skill_invoked")]
// Catch-all
[JsonDerivedType(typeof(CustomEvent), "custom")]
public abstract record EventKind;

// ── Agent ───────────────────────────────────────────────────────────────────

public sealed record AgentDiscoveredEvent(
    [property: JsonPropertyName("agent_id")] Guid AgentId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("agent_type")] string AgentType,
    [property: JsonPropertyName("executable_path")] string? ExecutablePath = null,
    [property: JsonPropertyName("version")] string? Version = null
) : EventKind;

public sealed record AgentHeartbeatEvent(
    [property: JsonPropertyName("agent_id")] Guid AgentId
) : EventKind;

// ── Session ─────────────────────────────────────────────────────────────────

public sealed record SessionStartedEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("agent_id")] Guid AgentId,
    [property: JsonPropertyName("cwd")] string? Cwd = null,
    [property: JsonPropertyName("git_repo")] string? GitRepo = null,
    [property: JsonPropertyName("git_branch")] string? GitBranch = null
) : EventKind;

public sealed record SessionEndedEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("duration_ms")] uint? DurationMs = null
) : EventKind;

public sealed record SessionResumedEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId
) : EventKind;

public sealed record SessionMetadataUpdatedEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("metadata")] JsonNode? Metadata
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

public sealed record TurnStreamingEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("partial_content")] string? PartialContent = null,
    [property: JsonPropertyName("tokens_so_far")] ulong? TokensSoFar = null
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

// ── File ────────────────────────────────────────────────────────────────────

public sealed record FileReadEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("effect_id")] Guid? EffectId = null,
    [property: JsonPropertyName("parent_effect_id")] Guid? ParentEffectId = null
) : EventKind;

public sealed record FileWrittenEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("effect_id")] Guid? EffectId = null,
    [property: JsonPropertyName("parent_effect_id")] Guid? ParentEffectId = null
) : EventKind;

public sealed record FileCreatedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("effect_id")] Guid? EffectId = null,
    [property: JsonPropertyName("parent_effect_id")] Guid? ParentEffectId = null
) : EventKind;

public sealed record FileDeletedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("effect_id")] Guid? EffectId = null,
    [property: JsonPropertyName("parent_effect_id")] Guid? ParentEffectId = null
) : EventKind;

// ── Shell ───────────────────────────────────────────────────────────────────

public sealed record ShellCommandStartedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("effect_id")] Guid EffectId,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("parent_effect_id")] Guid? ParentEffectId = null,
    [property: JsonPropertyName("cwd")] string? Cwd = null
) : EventKind;

public sealed record ShellCommandCompletedEvent(
    [property: JsonPropertyName("effect_id")] Guid EffectId,
    [property: JsonPropertyName("exit_code")] int? ExitCode = null,
    [property: JsonPropertyName("duration_ms")] uint? DurationMs = null
) : EventKind;

// ── Sub-Agent ───────────────────────────────────────────────────────────────

public sealed record SubAgentSpawnedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("effect_id")] Guid EffectId,
    [property: JsonPropertyName("agent_type")] string AgentType,
    [property: JsonPropertyName("parent_effect_id")] Guid? ParentEffectId = null,
    [property: JsonPropertyName("prompt")] string? Prompt = null
) : EventKind;

public sealed record SubAgentCompletedEvent(
    [property: JsonPropertyName("effect_id")] Guid EffectId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("duration_ms")] uint? DurationMs = null
) : EventKind;

// ── Planning ────────────────────────────────────────────────────────────────

public sealed record PlanCreatedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("content")] string Content
) : EventKind;

public sealed record PlanStepCompletedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("step")] string Step
) : EventKind;

public sealed record ThinkingBlockEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("content")] string Content
) : EventKind;

// ── Context ─────────────────────────────────────────────────────────────────

public sealed record ContextWindowSnapshotEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("total_tokens")] ulong TotalTokens,
    [property: JsonPropertyName("max_tokens")] ulong? MaxTokens = null
) : EventKind;

public sealed record ContextPrunedEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("tokens_removed")] ulong TokensRemoved
) : EventKind;

// ── Human-in-Loop ───────────────────────────────────────────────────────────

public sealed record ApprovalRequestedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("action")] string Action
) : EventKind;

public sealed record ApprovalGrantedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId
) : EventKind;

public sealed record ApprovalDeniedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("reason")] string? Reason = null
) : EventKind;

public sealed record UserFeedbackEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("sentiment")] string? Sentiment = null
) : EventKind;

// ── Self-Report ─────────────────────────────────────────────────────────────

public sealed record IntentDeclaredEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("intent")] string Intent
) : EventKind;

public sealed record DecisionMadeEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("decision")] string Decision,
    [property: JsonPropertyName("reasoning")] string? Reasoning = null,
    [property: JsonPropertyName("alternatives")] IReadOnlyList<string>? Alternatives = null
) : EventKind;

public sealed record ThoughtLoggedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("category")] string? Category = null
) : EventKind;

public sealed record FrustrationReportedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("issue")] string Issue,
    [property: JsonPropertyName("severity")] string? Severity = null
) : EventKind;

public sealed record OutcomeReportedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("success")] bool Success
) : EventKind;

public sealed record ObservationLoggedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("observation")] string Observation
) : EventKind;

public sealed record RecipeFollowedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("recipe")] string Recipe
) : EventKind;

public sealed record PathNotTakenEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("reason")] string Reason
) : EventKind;

public sealed record ConfidenceAssessedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("level")] double Level
) : EventKind;

public sealed record AssumptionMadeEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("assumption")] string Assumption
) : EventKind;

// ── Model ───────────────────────────────────────────────────────────────────

public sealed record ModelUsedEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("provider")] string? Provider = null,
    [property: JsonPropertyName("tokens")] JsonNode? Tokens = null,
    [property: JsonPropertyName("cost")] JsonNode? Cost = null,
    [property: JsonPropertyName("invocation_count")] uint? InvocationCount = null
) : EventKind;

public sealed record ModelSwitchedEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("from_model")] string FromModel,
    [property: JsonPropertyName("to_model")] string ToModel
) : EventKind;

// ── Error ───────────────────────────────────────────────────────────────────

public sealed record ErrorOccurredEvent(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("turn_id")] Guid? TurnId = null,
    [property: JsonPropertyName("session_id")] Guid? SessionId = null,
    [property: JsonPropertyName("category")] string? Category = null
) : EventKind;

public sealed record RetryAttemptedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("attempt")] uint Attempt,
    [property: JsonPropertyName("reason")] string? Reason = null
) : EventKind;

// ── Code ────────────────────────────────────────────────────────────────────

public sealed record SearchPerformedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("effect_id")] Guid? EffectId = null,
    [property: JsonPropertyName("parent_effect_id")] Guid? ParentEffectId = null,
    [property: JsonPropertyName("result_count")] uint? ResultCount = null
) : EventKind;

public sealed record CodeChangeAppliedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("change_type")] string ChangeType
) : EventKind;

// ── Network ─────────────────────────────────────────────────────────────────

public sealed record WebRequestMadeEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("effect_id")] Guid? EffectId = null,
    [property: JsonPropertyName("parent_effect_id")] Guid? ParentEffectId = null,
    [property: JsonPropertyName("method")] string? Method = null,
    [property: JsonPropertyName("status_code")] ushort? StatusCode = null
) : EventKind;

public sealed record McpServerConnectedEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("server_name")] string ServerName
) : EventKind;

// ── Cost ────────────────────────────────────────────────────────────────────

public sealed record TokenUsageReportedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("input_tokens")] ulong? InputTokens = null,
    [property: JsonPropertyName("output_tokens")] ulong? OutputTokens = null,
    [property: JsonPropertyName("cache_read_tokens")] ulong? CacheReadTokens = null
) : EventKind;

public sealed record RateLimitHitEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("retry_after_secs")] uint? RetryAfterSecs = null
) : EventKind;

// ── Git ─────────────────────────────────────────────────────────────────────

public sealed record GitCommitCreatedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("message")] string? Message = null
) : EventKind;

public sealed record GitBranchCreatedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("branch")] string Branch
) : EventKind;

public sealed record PullRequestCreatedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("identifier")] string Identifier,
    [property: JsonPropertyName("title")] string? Title = null
) : EventKind;

// ── Session Management ─────────────────────────────────────────────────────

public sealed record SessionModeChangedEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("previous_mode")] string PreviousMode,
    [property: JsonPropertyName("new_mode")] string NewMode
) : EventKind;

public sealed record CompactionStartedEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId
) : EventKind;

public sealed record CompactionCompletedEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("pre_compaction_tokens")] ulong? PreCompactionTokens = null,
    [property: JsonPropertyName("checkpoint_number")] uint? CheckpointNumber = null,
    [property: JsonPropertyName("compaction_tokens_used")] JsonNode? CompactionTokensUsed = null
) : EventKind;

// ── Hooks ───────────────────────────────────────────────────────────────────

public sealed record HookStartedEvent(
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("hook_id")] Guid HookId,
    [property: JsonPropertyName("hook_type")] string HookType,
    [property: JsonPropertyName("tool_name")] string? ToolName = null
) : EventKind;

public sealed record HookCompletedEvent(
    [property: JsonPropertyName("hook_id")] Guid HookId,
    [property: JsonPropertyName("success")] bool Success
) : EventKind;

public sealed record SkillInvokedEvent(
    [property: JsonPropertyName("turn_id")] Guid TurnId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("path")] string? Path = null
) : EventKind;

// ── Catch-all ───────────────────────────────────────────────────────────────

public sealed record CustomEvent(
    [property: JsonPropertyName("event_type")] string EventType,
    [property: JsonPropertyName("data")] JsonNode? Data
) : EventKind;
