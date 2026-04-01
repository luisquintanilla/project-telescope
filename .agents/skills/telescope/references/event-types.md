# Event Types Reference

Telescope captures 60+ structured event types organized by category. Each event is JSON with an internally-tagged `type` field and includes provenance metadata (source, confidence, capture method).

## Session Lifecycle

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `SessionStarted` | session_id, cwd, git_repo, git_branch | New conversation/task begins |
| `SessionEnded` | session_id, reason, duration_secs | Conversation ends |
| `SessionResumed` | session_id | Previously ended session resumes |
| `SessionMetadataUpdated` | session_id, metadata | Session context changes |
| `SessionModeChanged` | session_id, new_mode | Agent mode changes (e.g., plan → execute) |

## Turns (LLM Request/Response Cycles)

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `UserMessage` | session_id, turn_id, content | User input to the agent |
| `TurnStarted` | session_id, turn_id, turn_index, model_name | LLM call begins |
| `TurnCompleted` | session_id, turn_id, status, duration_ms, tokens | LLM call finishes |
| `TurnStreaming` | session_id, turn_id, chunk | Streaming token chunk |

## Tool Calls

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `ToolCallStarted` | session_id, turn_id, tool_name, tool_input | Agent invokes a tool |
| `ToolCallCompleted` | session_id, turn_id, tool_name, tool_result, duration_ms | Tool returns result |

**Debugging tip:** Compare `tool_input` (what the LLM sent) with `tool_result` (what came back). Mismatches here reveal tool implementation bugs.

## Errors

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `ErrorOccurred` | session_id, turn_id, message, category | Something went wrong |
| `RetryAttempted` | session_id, turn_id, attempt, reason | Agent retrying after failure |

## Token Usage

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `TokenUsageReported` | turn_id, input_tokens, output_tokens | Per-turn token consumption |
| `RateLimitHit` | model, retry_after | Rate limit encountered |

## File Operations

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `FileRead` | session_id, path, size_bytes | Agent reads a file |
| `FileWritten` | session_id, path, size_bytes | Agent writes/edits a file |
| `FileCreated` | session_id, path | Agent creates a new file |
| `FileDeleted` | session_id, path | Agent deletes a file |

## Shell Commands

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `ShellCommandStarted` | session_id, command, cwd | Agent runs a shell command |
| `ShellCommandCompleted` | session_id, command, exit_code, duration_ms | Command finishes |

## Sub-Agents

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `SubAgentSpawned` | session_id, sub_agent_id, task | Agent delegates to sub-agent |
| `SubAgentCompleted` | session_id, sub_agent_id, status | Sub-agent finishes |

## Planning

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `PlanCreated` | session_id, plan_id, steps | Agent creates an execution plan |
| `PlanStepCompleted` | session_id, plan_id, step_index, status | Plan step finishes |
| `ThinkingBlock` | session_id, content | Agent's internal reasoning |

## Context Management

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `ContextWindowSnapshot` | session_id, total_tokens, used_tokens | Context window state |
| `ContextPruned` | session_id, tokens_removed, strategy | Context was trimmed |

## Human-in-the-Loop

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `ApprovalRequested` | session_id, action, description | Agent asks for permission |
| `ApprovalGranted` | session_id, action | User approves |
| `ApprovalDenied` | session_id, action, reason | User denies |
| `UserFeedback` | session_id, feedback_type, content | User provides feedback |

## Agent Self-Report

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `IntentDeclared` | session_id, intent | Agent states what it plans to do |
| `DecisionMade` | session_id, decision, reasoning | Agent makes a choice |
| `ConfidenceAssessed` | session_id, task, confidence | Agent rates its confidence |
| `FrustrationReported` | session_id, reason | Agent reports difficulty |
| `AssumptionMade` | session_id, assumption | Agent makes an assumption |
| `ObservationLogged` | session_id, observation | Agent notes something |

## Model

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `ModelUsed` | session_id, model_name, provider | Which model is being used |
| `ModelSwitched` | session_id, from_model, to_model, reason | Agent switches models |

## Network

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `WebRequestMade` | session_id, url, method, status_code | HTTP request by agent |
| `McpServerConnected` | session_id, server_name | MCP server connected |

## Git

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `GitCommitCreated` | session_id, sha, message | Agent creates a commit |
| `GitBranchCreated` | session_id, branch_name | Agent creates a branch |
| `PullRequestCreated` | session_id, pr_number, title | Agent creates a PR |

## Code

| Event | Key Fields | Description |
|-------|-----------|-------------|
| `SearchPerformed` | session_id, query, results_count | Agent searches codebase |
| `CodeChangeApplied` | session_id, file, description | Agent applies a code change |

## Provenance

Every event includes provenance metadata:
- **Source:** `McpProxy`, `SessionLog`, `CopilotSdk`, `SelfReport`, `Manual`, etc.
- **Confidence:** 0.0–1.0 (MCP proxy: 0.95, self-report: 0.55, inferred: 0.30)
- **Capture method:** `LiveIntercept`, `PostHocLogParse`, `LiveSdkHook`, `Volunteered`, `Inferred`
