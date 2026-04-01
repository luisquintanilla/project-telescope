---
name: telescope
description: "Use this skill when the user is debugging an AI agent application and needs to inspect agent behavior, trace tool calls, analyze token usage, or diagnose failures using Project Telescope. Use it when they mention Telescope sessions, turns, tool calls, agent traces, MCP proxy, or local AI observability. Also use it when investigating why an agent made wrong decisions, used too many tokens, called the wrong tool, or returned unexpected results — even without explicitly naming Telescope. Do not use it for non-AI application debugging, generic OTEL trace analysis without agent context, or production monitoring systems."
---

# Telescope Skill

Use this skill when the task is about debugging AI agent behavior using Project Telescope — a local-first observability tool that captures rich, structured events from AI agent sessions.

## Use this skill for

- Inspecting AI agent sessions, turns, and tool calls via `tele` CLI
- Diagnosing why an agent called the wrong tool or returned wrong results
- Analyzing token usage and identifying context bloat
- Tracing errors and failures in agent execution
- Setting up Telescope for a new AI agent project
- Instrumenting MCP servers with `tele proxy` for transparent capture
- Understanding the agent event hierarchy (session → turn → tool call)

## Do not use this skill for

- Non-AI application debugging (use standard debugging tools)
- Generic OpenTelemetry trace analysis without AI agent context
- Production monitoring or alerting (Telescope is local-first)
- Container orchestration or infrastructure management

## Default workflow

When a user reports an AI agent issue, follow this investigation order:

1. **Check service status:** `tele status` to confirm Telescope is running.
2. **Find the session:** `tele sessions list` to find the relevant session by agent name or timestamp.
3. **Inspect turns:** `tele turns list <session-id>` to see the sequence of turns, token usage, and tool calls.
4. **Drill into details:** `tele sessions get <session-id>` for full session metadata.
5. **Cross-reference code:** Match tool call events (names, arguments, results) against the agent's source code.
6. **Identify the root cause:** Look for mismatched tool names, wrong arguments, unexpected results, excessive tokens, or error events.
7. **Suggest a fix:** Propose specific code changes based on trace evidence.

## Key commands

```bash
tele status                         # Service overview — is Telescope running?
tele sessions list                  # List recent agent sessions
tele sessions get <session-id>      # Full session details
tele turns list <session-id>        # Turns with tool calls, tokens, timing
tele agents list                    # List known agents
tele collectors list                # List active collectors
```

Add `--json` to any command for machine-readable output that's easier to parse.

## Common debugging patterns

### Agent returns wrong results
1. `tele turns list <session-id>` — find the turn with wrong output
2. Look at `ToolCallCompleted` events — is the tool returning unexpected data?
3. Check the tool arguments — did the LLM pass the right inputs?
4. Cross-reference with the tool implementation in code

### Agent calls the wrong tool
1. `tele turns list <session-id>` — find `ToolCallStarted` events
2. Check which tool was selected vs which should have been selected
3. Inspect tool `[Description]` attributes — are they clear enough for the LLM?
4. Check if the right tools are registered in `ChatOptions.Tools`

### Excessive token usage
1. `tele turns list <session-id>` — look at per-turn token counts
2. Identify turns with disproportionate usage
3. Check for: large tool outputs, repeated tool calls, context window bloat
4. Suggest: output truncation, conversation windowing, tool result summarization

### Agent errors
1. `tele sessions list` — find sessions with errors
2. `tele turns list <session-id>` — look for `ErrorOccurred` events
3. Check error category and message
4. Cross-reference with exception handling in code

## MCP proxy instrumentation

Telescope can transparently capture MCP server traffic:

```bash
tele setup                          # Auto-instruments all MCP configs
tele setup --undo                   # Restore original configs
tele proxy stdio -- <command>       # Manual proxy for a single server
```

See [references/mcp-proxy.md](references/mcp-proxy.md) for details.

## Event hierarchy

Telescope models agent behavior as a hierarchy:

```
Agent
  └── Session (conversation or task)
        └── Turn (one LLM request/response cycle)
              ├── UserMessage
              ├── ToolCallStarted → ToolCallCompleted
              ├── TokenUsageReported
              └── ErrorOccurred (if any)
```

See [references/event-types.md](references/event-types.md) for the full list of 60+ event types.

## References

- For CLI command details, see [references/cli-commands.md](references/cli-commands.md).
- For the full event type reference, see [references/event-types.md](references/event-types.md).
- For systematic debugging workflows, see [references/debugging-workflows.md](references/debugging-workflows.md).
- For MCP proxy instrumentation, see [references/mcp-proxy.md](references/mcp-proxy.md).
